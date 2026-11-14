using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// ISO 兼容性体检：直接读 .iso，判断这套「移除校验」补丁能不能用
public static class IsoCheck {
    public const string Target = "scripts/init-bottom/deepin-squashfs-verify.sh";
    public const string Marker = "no-verify patch";

    class Info {
        public string Path; public long Size; public long Extent; public bool Found;
    }

    static Info Find(IsoImage img, string path) {
        foreach (var e in img.Entries) {
            if (!e.IsDir && string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)) {
                return new Info { Path = e.Path, Size = e.Size, Extent = e.Extent, Found = true };
            }
        }
        return new Info { Path = path, Found = false };
    }

    static void Extract(IsoImage img, string isoPath, Info f, string outPath) {
        using (var fs = File.OpenRead(isoPath)) {
            fs.Position = f.Extent * 2048L;
            using (var o = File.Create(outPath)) {
                var buf = new byte[1 << 20];
                long left = f.Size;
                while (left > 0) {
                    int r = fs.Read(buf, 0, (int)Math.Min(buf.Length, left));
                    if (r <= 0) break;
                    o.Write(buf, 0, r);
                    left -= r;
                }
            }
        }
    }

    static string Human(long b) {
        if (b >= 1073741824L) return string.Format("{0:N2} GB", b / 1073741824.0);
        if (b >= 1048576L) return string.Format("{0:N1} MB", b / 1048576.0);
        return string.Format("{0:N0} B", b);
    }

    // 轻量入口：只回答"能不能打"，供做盘流程 / 主界面自动检测用
    public static bool Compatible(string isoPath, string patchText) {
        bool ok = false;
        try { Run(isoPath, patchText, out ok, null); } catch { ok = false; }
        return ok;
    }

    // 返回报告文本；ok 表示两套 initrd 都能打补丁
    public static string Run(string isoPath, string patchText, out bool ok, Action<string> log) {
        ok = false;
        var sb = new StringBuilder();
        var di = new FileInfo(isoPath);

        sb.AppendLine("【ISO 体检】" + di.Name);
        sb.AppendLine("  文件大小 " + Human(di.Length) + "    " + di.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));

        IsoImage img;
        try { img = IsoImage.Open(isoPath); }
        catch (Exception ex) { sb.AppendLine("  ✗ 打不开这个 ISO：" + ex.Message); return sb.ToString(); }

        sb.AppendLine("  卷标 " + img.VolumeId + "    文件 " + img.FileCount + " 个 / " + Human(img.TotalBytes));

        var grub = Find(img, "boot/grub/grub.cfg");
        var sqfs = Find(img, "live/filesystem.squashfs");
        var oemIni = Find(img, "oem/settings.ini");
        var oemSq = Find(img, "oem.squashfs");
        var i1 = Find(img, "live/initrd.lz");
        var i2 = Find(img, "live/initrd-5.10.0.lz");

        sb.AppendLine("  引导配置 " + (grub.Found ? "boot/grub/grub.cfg  " + Human(grub.Size) : "✗ 缺失"));
        sb.AppendLine("  系统镜像 " + (sqfs.Found ? "live/filesystem.squashfs  " + Human(sqfs.Size) : "✗ 缺失（不像 UOS 安装盘）"));
        sb.AppendLine("  oem 配置 " + (oemIni.Found ? "oem/settings.ini 可直接改（第二层补丁能落）"
                                        : (oemSq.Found ? "只有 oem.squashfs（第二层跳过，靠 initrd 层）" : "无")));
        sb.AppendLine();

        if (!sqfs.Found) { sb.AppendLine("结论：这不是 UOS 安装镜像，补丁不适用。"); return sb.ToString(); }

        bool allOk = true;
        int checkedCount = 0;
        foreach (var pair in new[] { new object[] { i1, "live/initrd.lz" }, new object[] { i2, "live/initrd-5.10.0.lz" } }) {
            var f = (Info)pair[0];
            string label = (string)pair[1];
            if (!f.Found) { sb.AppendLine("· " + label + "：不存在，跳过"); continue; }
            checkedCount++;
            sb.AppendLine("· " + label + "  " + Human(f.Size));
            string tmp = Path.Combine(Path.GetTempPath(), "uoschk_" + Guid.NewGuid().ToString("N") + ".lz");
            try {
                Extract(img, isoPath, f, tmp);
                if (log != null) log("  已抽出 " + label);
                InitrdFile ir = InitrdFile.Load(tmp);
                sb.AppendLine("    压缩格式 " + ir.Format + (ir.Format == "lz4" ? "（老版 deepin 封装）" : "（gzip 封装）"));

                int[] loc = Cpio.Locate(ir.CpioBody, Target);
                if (loc[0] < 0) {
                    sb.AppendLine("    ✗ 找不到 " + Target);
                    sb.AppendLine("    → 这个镜像的校验脚本不在预期位置，补丁不适用");
                    allOk = false;
                    continue;
                }
                string txt = Cpio.GetText(ir.CpioBody, Target);
                bool patched = txt != null && txt.IndexOf(Marker, StringComparison.Ordinal) >= 0;
                sb.AppendLine("    目标脚本 " + loc[1] + " B    需要 " + patchText.Length + " B");
                sb.AppendLine("    含 livecd-installer 判定：" + (txt != null && txt.IndexOf("livecd-installer", StringComparison.Ordinal) >= 0 ? "是（只在安装/修复模式校验）" : "否（机制不同）"));
                if (patched) {
                    sb.AppendLine("    状态：已经是补丁版本");
                } else if (patchText.Length > loc[1]) {
                    sb.AppendLine("    ✗ 槽位不够：脚本 " + loc[1] + " B，补丁 " + patchText.Length + " B");
                    allOk = false;
                } else {
                    sb.AppendLine("    ✓ 可打补丁（余 " + (loc[1] - patchText.Length) + " B 用 # 填充，长度不变）");
                }
            } catch (Exception ex) {
                sb.AppendLine("    ✗ 解析失败：" + ex.Message);
                allOk = false;
            } finally {
                try { File.Delete(tmp); } catch { }
            }
            sb.AppendLine();
        }

        if (checkedCount == 0) { sb.AppendLine("结论：ISO 里没有 live/initrd*.lz，补丁不适用。"); return sb.ToString(); }

        ok = allOk;
        sb.AppendLine(allOk
            ? "结论：✓ 这个 ISO 可以打「移除校验」补丁，做出来的盘能直接用。"
            : "结论：✗ 这套 ISO 和补丁不匹配，别对这个镜像用「移除校验」。");
        if (allOk) sb.AppendLine("  注：ISO 本身不会被修改，补丁只作用在制作出来的 U 盘上。");
        return sb.ToString();
    }
}
