using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;

public class UsbDisk {
    public int Number;
    public string Model;
    public long Size;
    public bool IsSystem;
    public List<string> Letters = new List<string>();
    public double SizeGB { get { return Math.Round(Size / 1073741824.0, 2); } }
}

public static class Drives {
    public static List<UsbDisk> ListUsb() {
        var list = new List<UsbDisk>();
        var systemDisks = new HashSet<int>();

        try {
            using (var sys = new ManagementObjectSearcher(
                "SELECT Antecedent,Dependent FROM Win32_LogicalDiskToPartition WHERE Dependent='C:'")) {
                foreach (ManagementObject mo in sys.Get()) {
                    string part = ((string)mo["Antecedent"]).Split('"')[1];
                    systemDisks.Add(DiskOfPartition(part));
                }
            }
        } catch { }

        // 不再只信 WMI 的 InterfaceType：USB-SATA 桥接盘常被报成 SCSI（External hard disk media）
        using (var q = new ManagementObjectSearcher(
            "SELECT Index,Model,Size,DeviceID,InterfaceType,MediaType,PNPDeviceID FROM Win32_DiskDrive")) {
            foreach (ManagementObject d in q.Get()) {
                int idx = Convert.ToInt32(d["Index"]);
                string iface = Convert.ToString(d["InterfaceType"]);
                string media = Convert.ToString(d["MediaType"]);
                string pnp = Convert.ToString(d["PNPDeviceID"]);

                bool isUsb = string.Equals(iface, "USB", StringComparison.OrdinalIgnoreCase)
                          || (pnp != null && pnp.IndexOf("USBSTOR", StringComparison.OrdinalIgnoreCase) >= 0)
                          || (pnp != null && pnp.IndexOf(@"USB\", StringComparison.OrdinalIgnoreCase) >= 0)
                          || BusTypeOf(idx) == 7;                      // BusTypeUsb
                if (!isUsb) continue;

                var u = new UsbDisk();
                u.Number = idx;
                u.Model = System.Text.RegularExpressions.Regex.Replace(Convert.ToString(d["Model"]).Trim(), @"\s{2,}", " ");
                u.Size = d["Size"] == null ? 0 : Convert.ToInt64(d["Size"]);
                u.IsSystem = systemDisks.Contains(idx);
                u.Letters = LettersOfDisk(idx);
                list.Add(u);
            }
        }
        list.Sort(delegate (UsbDisk a, UsbDisk b) { return a.Number.CompareTo(b.Number); });
        return list;
    }

    // ---- 用 IOCTL_STORAGE_QUERY_PROPERTY 读真实总线类型（和「磁盘管理」一致）----
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(IntPtr h, uint code, byte[] inBuf, int inSize, byte[] outBuf, int outSize, out int ret, IntPtr ov);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);

    public static int BusTypeOf(int diskNumber) {
        IntPtr h = IntPtr.Zero;
        try {
            h = CreateFileW(@"\\.\PhysicalDrive" + diskNumber, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (h == IntPtr.Zero || h == new IntPtr(-1)) return -1;
            byte[] q = new byte[12];
            byte[] o = new byte[1024];
            int ret;
            if (!DeviceIoControl(h, 0x002D1400, q, q.Length, o, o.Length, out ret, IntPtr.Zero)) return -1;
            if (ret < 29) return -1;
            return o[28];
        } catch { return -1; }
        finally { if (h != IntPtr.Zero && h != new IntPtr(-1)) CloseHandle(h); }
    }
    static int DiskOfPartition(string partitionDeviceID) {
        try {
            using (var q = new ManagementObjectSearcher(string.Format(
                "ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{0}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition",
                partitionDeviceID))) {
                foreach (ManagementObject d in q.Get()) return Convert.ToInt32(d["Index"]);
            }
        } catch { }
        return -1;
    }

    static readonly Dictionary<int, List<string>> _letterCache = new Dictionary<int, List<string>>();
    static DateTime _cacheTime = DateTime.MinValue;

    static List<string> LettersOfDisk(int diskIndex) {
        var res = new List<string>();
        try {
            using (var q = new ManagementObjectSearcher(
                "SELECT Antecedent,Dependent FROM Win32_LogicalDiskToPartition")) {
                foreach (ManagementObject mo in q.Get()) {
                    string dep = (string)mo["Dependent"];      // Win32_LogicalDisk.DeviceID="E:"
                    string ant = (string)mo["Antecedent"];     // Win32_DiskPartition.DeviceID="Disk #2, Partition #0"
                    int di = ParseDiskFromPartition(ant);
                    if (di == diskIndex) res.Add(dep.Split('"')[1]);
                }
            }
        } catch { }
        res.Sort();
        return res;
    }


    /// <summary>取某块盘第 N 个分区（1 起）当前挂的盘符；没挂返回 null。</summary>
    public static string LetterOfPartition(int diskIndex, int partNumber) {
        try {
            using (var q = new ManagementObjectSearcher(
                "SELECT Antecedent,Dependent FROM Win32_LogicalDiskToPartition")) {
                foreach (ManagementObject mo in q.Get()) {
                    string dep = (string)mo["Dependent"];
                    string ant = (string)mo["Antecedent"];
                    if (ParseDiskFromPartition(ant) != diskIndex) continue;
                    string key = "Disk #" + diskIndex + ", Partition #" + (partNumber - 1);
                    if (ant.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return dep.Split('"')[1];
                }
            }
        } catch { }
        return null;
    }

    static int ParseDiskFromPartition(string ant) {
        int i = ant.IndexOf("Disk #");
        if (i < 0) return -1;
        int p = i + 6, n = 0;
        while (p < ant.Length && char.IsDigit(ant[p])) { n = n * 10 + (ant[p] - '0'); p++; }
        return n;
    }

    public static List<string> Fat32VolumesWithLive() {
        var res = new List<string>();
        foreach (var di in DriveInfo.GetDrives()) {
            try {
                if (di.DriveType != DriveType.Removable && di.DriveType != DriveType.Fixed) continue;
                if (!di.IsReady) continue;
                if (di.DriveFormat != "FAT32") continue;
                string root = di.Name;
                if (File.Exists(Path.Combine(root, @"live\filesystem.squashfs"))) res.Add(root);
            } catch { }
        }
        return res;
    }
}

public static class Diskpart {
    public static string Run(string script) {
        string tmp = Path.Combine(Path.GetTempPath(), "dp_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(tmp, script.Replace("\r\n", "\n").Replace("\n", "\r\n"), Encoding.ASCII);
        try {
            var psi = new ProcessStartInfo("diskpart.exe", "/s \"" + tmp + "\"");
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            using (var p = Process.Start(psi)) {
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit(120000);
                return o;
            }
        } finally {
            try { File.Delete(tmp); } catch { }
        }
    }

    public static void RunChecked(string script) {
        string o = Run(script);
        if (o.IndexOf("Virtual Disk Service error", StringComparison.OrdinalIgnoreCase) >= 0 ||
            o.IndexOf("DiskPart 遇到错误", StringComparison.Ordinal) >= 0 ||
            o.IndexOf("DiskPart has encountered an error", StringComparison.OrdinalIgnoreCase) >= 0) {
            throw new Exception("diskpart 执行失败：" + LastError(o));
        }
    }

    static string LastError(string o) {
        var lines = o.Split('\n');
        for (int i = 0; i < lines.Length; i++) {
            if (lines[i].IndexOf("错误", StringComparison.Ordinal) >= 0 ||
                lines[i].IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0) {
                return lines[i].Trim();
            }
        }
        return "(未知)";
    }

    public static byte MbrPartType(int diskNumber, int partIndex) {
        try {
            using (var fs = new FileStream(@"\\.\PhysicalDrive" + diskNumber, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                byte[] b = new byte[512];
                fs.Position = 0;
                fs.Read(b, 0, 512);
                int off = 446 + 16 * (partIndex - 1) + 4;
                return b[off];
            }
        } catch { return 0; }
    }
}
