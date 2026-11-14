using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

public class ProgressInfo {
    public double SpeedBytes = -1;   // >0 时直接用作显示速度（字节/秒）
    public long EtaBytes = -1;       // >0 时用 ETA = EtaBytes / SpeedBytes
    public string Phase;
    public string Detail;
    public long Done;
    public long Total;
    public bool Indeterminate;
}

public class JobOptions {
    public string IsoPath;
    public string UimgPath;
    public int DiskNumber;
    public int P1SizeMb = 16384;
    public bool RemoveVerify = true;
    public string PatchFile;
    public string OemDir;
    public bool DryRun;
}

public class JobResult {
    public bool Ok;
    public bool DryRun;
    public string P1;
    public string P2;
    public int Files;
    public long Bytes;
    public string Summary;
}

public class MakeJob {
    public Action<ProgressInfo> OnProgress;
    public Action<string> OnLog;
    public Func<bool> Cancelled;

    const string PatchTarget = "scripts/init-bottom/deepin-squashfs-verify.sh";
    const string PatchMarker = "no-verify patch";

    long _done, _total;

    void Prog(string phase, string detail, bool indeterminate) {
        var p = new ProgressInfo();
        p.Phase = phase; p.Detail = detail; p.Done = _done; p.Total = _total; p.Indeterminate = indeterminate;
        if (OnProgress != null) OnProgress(p);
    }

    void Log(string msg) { if (OnLog != null) OnLog(msg); }

    void Check() { if (Cancelled != null && Cancelled()) throw new OperationCanceledException(); }

    public JobResult Run(JobOptions o) {
        var res = new JobResult();

        // ---------- 1. 打开 ISO ----------
        Prog("检查镜像", "打开 ISO …", true);
        Log("打开 ISO：" + o.IsoPath);
        IsoImage iso = IsoImage.Open(o.IsoPath);
        Log(string.Format("ISO 卷标 {0}，{1} 个文件 / {2:N2} GB", iso.VolumeId, iso.FileCount, iso.TotalBytes / 1073741824.0));
        var prof = IsoProfile.Detect(iso, Path.GetFileName(o.IsoPath));
        Log("镜像类型：" + prof.Describe());
        if (!prof.FitsFat32) {
            throw new Exception(string.Format("这个镜像里有超过 4 GB 的单文件（{0}，{1:N2} GB），FAT32 分区放不下。请改用 Rufus 的 DD 模式写入。", prof.LargestPath, prof.LargestFile / 1073741824.0));
        }
        if (!prof.HasEfiBoot) Log("  ⚠ 这个镜像没有 EFI 引导文件，做出来的盘可能无法 UEFI 启动");

        long uimgLen = 0;
        if (!string.IsNullOrEmpty(o.UimgPath)) {
            var fi = new FileInfo(o.UimgPath);
            if (!fi.Exists) throw new Exception("备份镜像不存在：" + o.UimgPath);
            uimgLen = fi.Length;
            Log(string.Format("备份镜像 {0}  {1:N2} GB", Path.GetFileName(o.UimgPath), uimgLen / 1073741824.0));
        }

        // ---------- 2. 目标盘 ----------
        var disks = Drives.ListUsb();
        UsbDisk target = null;
        foreach (var d in disks) if (d.Number == o.DiskNumber) target = d;
        if (target == null) throw new Exception("找不到 Disk " + o.DiskNumber + "（可能已拔出，点刷新重试）");
        if (target.IsSystem) throw new Exception("Disk " + o.DiskNumber + " 是系统盘，不能使用");

        bool dual = uimgLen > 0;
        long needMb = (long)Math.Ceiling(iso.TotalBytes / 1048576.0 * 1.02) + 400;
        if (target.Size / 1048576L < needMb + (dual ? uimgLen / 1048576L : 0))
            throw new Exception(string.Format("目标盘 {0} GB 容量不够（至少需要 {1:N0} MB）", target.SizeGB, needMb));

        _total = iso.TotalBytes + uimgLen;
        _done = 0;

        var plan = new System.Text.StringBuilder();
        plan.AppendLine("select disk " + o.DiskNumber);
        plan.AppendLine("clean");
        plan.AppendLine("convert mbr");
        if (dual) {
            plan.AppendLine("create partition primary size=" + o.P1SizeMb);
            plan.AppendLine("set id=0c");
            plan.AppendLine("active");
            plan.AppendLine("create partition primary");
            plan.AppendLine("format fs=ntfs quick label=UOSIMG");
        } else {
            plan.AppendLine("create partition primary");
            plan.AppendLine("set id=0c");
            plan.AppendLine("active");
        }

        if (o.DryRun) {
            Log("【演练】diskpart 脚本：");
            foreach (var line in plan.ToString().Split('\n')) if (line.Trim().Length > 0) Log("    " + line.Trim());
            res.Ok = true; res.DryRun = true; res.Files = iso.FileCount;
            res.Bytes = iso.TotalBytes; res.Summary = "演练完成，未改动磁盘";
            iso.Dispose();
            return res;
        }

        // ---------- 3. 摘盘符 ----------
        Prog("准备磁盘", "释放盘符 …", true);
        try {
            var det = new System.Text.StringBuilder();
            det.AppendLine("select disk " + o.DiskNumber);
            int idx = 1;
            foreach (var d in disks) {
                if (d.Number != o.DiskNumber) continue;
                foreach (var L in d.Letters) {
                    det.AppendLine("select partition " + idx);
                    det.AppendLine("remove letter=" + L.TrimEnd(':'));
                }
            }
            if (det.ToString().Split('\n').Length > 1) { Diskpart.Run(det.ToString()); Thread.Sleep(800); }
        } catch { }

        // ---------- 4. 分区 ----------
        Prog("重建分区", "清空并重建分区表 …", true);
        Log("清空并重建分区表 …");
        bool ok = false;
        string last = "";
        for (int attempt = 1; attempt <= 3; attempt++) {
            try { Diskpart.RunChecked(plan.ToString()); ok = true; break; }
            catch (Exception ex) {
                last = ex.Message;
                Log(string.Format("  第 {0} 次失败：{1}", attempt, ex.Message));
                if (attempt < 3) { Thread.Sleep(2500); try { Diskpart.Run("rescan"); } catch { } }
            }
        }
        if (!ok) throw new Exception("清盘失败：" + last);

        // 显式挂盘符：按分区号挂，避免沿用旧盘符（旧卷的字母还没释放时，写进去的会是上一块盘/上一个分区）
        try {
            var asg = new System.Text.StringBuilder();
            asg.AppendLine("select disk " + o.DiskNumber);
            asg.AppendLine("select partition 1");
            asg.AppendLine("assign");
            if (dual) { asg.AppendLine("select partition 2"); asg.AppendLine("assign"); }
            Diskpart.Run(asg.ToString());
        } catch { }
        try { Diskpart.Run("rescan"); } catch { }

        string L1 = null, L2 = null;
        for (int i = 0; i < 60; i++) {
            L1 = Drives.LetterOfPartition(o.DiskNumber, 1);
            if (dual) L2 = Drives.LetterOfPartition(o.DiskNumber, 2);
            if (L1 != null && (!dual || L2 != null)) break;
            if (i == 15 || i == 30) { try { Diskpart.Run("rescan"); } catch { } }
            Thread.Sleep(500);
        }
        if (L1 == null) throw new Exception("分区后没有拿到盘符，请重试");
        if (dual && L2 == null) throw new Exception("第二个分区没有拿到盘符，请重试");
        L1 = L1.TrimEnd(':');
        if (L2 != null) L2 = L2.TrimEnd(':');
        string root1 = L1 + ":\\";
        string root2 = L2 == null ? null : L2 + ":\\";
        Log("  分区盘符：" + L1 + ":" + (L2 == null ? "" : " / " + L2 + ":"));

        // ---------- 5. 自写 FAT32 ----------
        Prog("格式化", "写入 FAT32 引导扇区 …", true);
        Log("自写 FAT32（" + L1 + ":）…");
        string fmt = null;
        string fmtErr = "";
        for (int i = 0; i < 12; i++) {
            try { fmt = Fat32Fmt.Format(L1, "DEEPINOS"); break; }
            catch (Exception ex) { fmtErr = ex.Message; Thread.Sleep(1200); }
        }
        if (fmt == null) throw new Exception("FAT32 格式化失败：" + fmtErr);
        Log("  " + fmt);
        // 让 Windows 重新识别新 FAT32：摘盘符再挂回
        for (int round = 0; round < 3 && !Directory.Exists(root1); round++) {
            try {
                var rm = new System.Text.StringBuilder();
                rm.AppendLine("select volume " + L1.TrimEnd(':'));
                rm.AppendLine("remove letter=" + L1.TrimEnd(':'));
                rm.AppendLine("assign letter=" + L1.TrimEnd(':'));
                Diskpart.Run(rm.ToString());
            } catch { }
            for (int i = 0; i < 24 && !Directory.Exists(root1); i++) Thread.Sleep(250);
            if (!Directory.Exists(root1)) { try { Diskpart.Run("rescan"); } catch { } Thread.Sleep(1000); }
        }
        if (!Directory.Exists(root1)) throw new Exception("格式化后 " + L1 + ": 仍不可访问（可在资源管理器里手动刷新一次）");

        // ---------- 6. 解压 ----------
        Prog("写入安装文件", "开始解压 …", false);
        Log("解压 ISO 到 " + root1 + " …");
        iso.ExtractTo(root1,
            delegate (string path, long done, long total) {
                _done = done;
                Prog("写入安装文件", path, false);
                Check();
            },
            Cancelled,
            delegate (string s) { Log("  跳过：" + s); });
        iso.Dispose();
        Log("解压完成");

        // ---------- 7. 去校验 ----------
        if (o.RemoveVerify && !string.IsNullOrEmpty(o.PatchFile) && File.Exists(o.PatchFile)) {
            string patch = File.ReadAllText(o.PatchFile);
            Check();
            Prog("检测补丁兼容性", "读取 initrd", true);
            Log("检测镜像补丁兼容性 …");
            bool compat = false;
            try { compat = IsoCheck.Compatible(o.IsoPath, patch); } catch (Exception ex) { Log("  自检出错：" + ex.Message); }
            if (!compat) {
                Log("  ✗ 这套镜像不支持「移除校验」，本次已跳过（盘照常可用）");
                o.RemoveVerify = false;
            } else {
                Log("  ✓ 兼容，开始打补丁");
            }
        }
        if (o.RemoveVerify && !string.IsNullOrEmpty(o.PatchFile) && File.Exists(o.PatchFile)) {
            string patch = File.ReadAllText(o.PatchFile);
            foreach (var name in new[] { "initrd.lz", "initrd-5.10.0.lz" }) {
                Check();
                string f = Path.Combine(root1, "live", name);
                if (!File.Exists(f)) { Log("  " + name + " 不存在，跳过"); continue; }
                Prog("移除镜像校验", name, true);
                Log("处理 " + name + " …");
                var t0 = DateTime.Now;
                var ir = InitrdFile.Load(f);
                int[] loc = Cpio.Locate(ir.CpioBody, PatchTarget);
                if (loc[0] < 0) { Log("  找不到校验脚本，跳过"); continue; }
                string old = Cpio.GetText(ir.CpioBody, PatchTarget);
                if (old.IndexOf(PatchMarker, StringComparison.Ordinal) >= 0) { Log("  已是补丁版本"); continue; }
                byte[] data = System.Text.Encoding.UTF8.GetBytes(patch);
                if (data.Length > loc[1]) throw new Exception("补丁脚本比原槽位长，无法等长覆盖");
                Cpio.ReplaceAt(ir.CpioBody, loc[0], loc[1], data);

                string bak = f + ".orig";
                if (!File.Exists(bak)) File.Copy(f, bak, true);
                ir.Save(f);
                bool synced = Md5Util.Sync(root1, "live/" + Path.GetFileName(f), f);
                Log(string.Format("  已打补丁  {0:N0} B  ({1:N1}s){2}", new FileInfo(f).Length, (DateTime.Now - t0).TotalSeconds, synced ? "，md5sum.txt 已同步" : ""));
            }
            // oem 覆盖项
            try {
                string oemDir = Path.Combine(root1, "oem");
                string ini = Path.Combine(oemDir, "settings.ini");
                if (File.Exists(ini)) {
                    string c = File.ReadAllText(ini);
                    string line = "DI_DEEPIN_SQUASHFS_VERIFY=\"/bin/true\"";
                    if (c.IndexOf("DI_DEEPIN_SQUASHFS_VERIFY", StringComparison.Ordinal) >= 0)
                        c = System.Text.RegularExpressions.Regex.Replace(c, "DI_DEEPIN_SQUASHFS_VERIFY\\s*=\\s*\"[^\"]*\"", line);
                    else c = c + "\r\n" + line + "\r\n";
                    File.WriteAllText(ini, c);
                    Log("  oem\\settings.ini 已写入覆盖项");
                }
                // 整棵 oem 目录（hooks / 天融信安装包）铺到介质根目录
                if (!string.IsNullOrEmpty(o.OemDir) && Directory.Exists(o.OemDir)) {
                    int n = CopyTree(o.OemDir, oemDir);
                    if (n > 0) Log(string.Format("  oem 钩子/资源已部署 {0} 个文件", n));
                }
            } catch { }
        }

        // ---------- 8. 镜像 ----------
        if (dual) {
            Prog("写入备份镜像", Path.GetFileName(o.UimgPath), false);
            Log("复制备份镜像到 " + L2 + ":\\clone …");
            string cloneDir = Path.Combine(root2, "clone");
            Directory.CreateDirectory(cloneDir);
            string dst = Path.Combine(cloneDir, Path.GetFileName(o.UimgPath));
            using (var src = new FileStream(o.UimgPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            using (var dstFs = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20)) {
                byte[] buf = new byte[1 << 20];
                while (true) {
                    Check();
                    int r = src.Read(buf, 0, buf.Length);
                    if (r <= 0) break;
                    dstFs.Write(buf, 0, r);
                    _done += r;
                    Prog("写入备份镜像", Path.GetFileName(o.UimgPath), false);
                }
            }
            Log("镜像复制完成");
        }

        // ---------- 9. 校验 ----------
        Prog("回读校验", "检查关键文件 …", true);
        int files = 0; long bytes = 0;
        foreach (var f in Directory.GetFiles(root1, "*", SearchOption.AllDirectories)) {
            var fi = new FileInfo(f);
            files++; bytes += fi.Length;
        }
        bool bad = false;
        var musts = new System.Collections.Generic.List<string>();
        if (prof.IsUos) { musts.Add(@"live\filesystem.squashfs"); musts.Add(@"boot\grub\grub.cfg"); }
        if (prof.HasEfiBoot) musts.Add(prof.EfiPath.Replace('/', '\\'));
        if (musts.Count == 0) musts.Add(@"EFI\BOOT\BOOTX64.efi");
        foreach (var rel in musts) {
            if (!File.Exists(Path.Combine(root1, rel))) { Log("  缺少 " + rel); bad = true; }
        }
        if (files < iso.FileCount * 0.98) { Log(string.Format("  文件数偏少：ISO {0}，盘上 {1}", iso.FileCount, files)); bad = true; }
        Log(string.Format("  盘上 {0} 个文件 / {1:N2} GB", files, bytes / 1073741824.0));

        if (dual) {
            string d = Path.Combine(root2, "clone", Path.GetFileName(o.UimgPath));
            if (File.Exists(d) && new FileInfo(d).Length == uimgLen) Log("  镜像大小一致");
            else { Log("  镜像大小不一致"); bad = true; }
        }

        string info = string.Join("\r\n", new[] {
            (prof.IsUos ? "UOS 安装盘 制作信息" : prof.Title + " 安装盘 制作信息"),
            "制作时间 : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "目标磁盘 : Disk " + o.DiskNumber + " / " + target.Model + " / " + target.SizeGB + " GB",
            "分区布局 : " + (dual ? L1 + ": DEEPINOS(FAT32) + " + L2 + ": UOSIMG(NTFS)" : L1 + ": DEEPINOS(FAT32) 整盘单分区"),
            "安装来源 : " + o.IsoPath,
            "备份镜像 : " + (dual ? o.UimgPath : "（无）"),
            "去校验   : " + (o.RemoveVerify ? "已处理" : "未处理"),
            "文件统计 : " + files + " 个 / " + bytes + " 字节",
            ""
        });
        try { File.WriteAllText(Path.Combine(root1, "制作信息.txt"), info, System.Text.Encoding.UTF8); } catch { }

        res.Ok = !bad; res.P1 = L1; res.P2 = L2; res.Files = files; res.Bytes = bytes;
        res.Summary = string.Format("{0} 个文件 / {1:N2} GB", files, bytes / 1073741824.0);
        return res;
    }
        static int CopyTree(string src, string dst) {
            int n = 0;
            Directory.CreateDirectory(dst);
            foreach (string f in Directory.GetFiles(src)) {
                string t = Path.Combine(dst, Path.GetFileName(f));
                if (string.Equals(Path.GetFileName(f), "settings.ini", StringComparison.OrdinalIgnoreCase)) continue;
                File.Copy(f, t, true);
                n++;
            }
            foreach (string d in Directory.GetDirectories(src)) {
                n += CopyTree(d, Path.Combine(dst, Path.GetFileName(d)));
            }
            return n;
        }
}
