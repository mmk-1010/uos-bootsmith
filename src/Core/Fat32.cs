using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// 自写 FAT32 格式化：绕开 Windows「大于 32 GB 不能格 FAT32」限制。
/// 直接用 WriteFile 写卷句柄（不用 FileStream —— .NET 的 FileStream 在
/// 已 DISMOUNT 的卷句柄上写入会抛 UnauthorizedAccessException）。
/// </summary>
public static class Fat32Fmt
{
    const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2;
    const uint OPEN_EXISTING = 3;
    const uint FSCTL_LOCK_VOLUME = 0x00090018;
    const uint FSCTL_UNLOCK_VOLUME = 0x0009001C;
    const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;
    const uint IOCTL_DISK_GET_LENGTH_INFO = 0x0007405C;
    const uint IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x00070000;

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
    [DllImport("kernel32", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle h, uint code, IntPtr inBuf, uint inSize, IntPtr outBuf, uint outSize, out uint ret, IntPtr ov);
    [DllImport("kernel32", SetLastError = true)]
    static extern bool SetFilePointerEx(SafeFileHandle h, long dist, out long pos, uint method);
    [DllImport("kernel32", SetLastError = true)]
    static extern bool WriteFile(SafeFileHandle h, byte[] buf, uint count, out uint written, IntPtr ov);

    /// <summary>把某个卷格式化成一个整盘 FAT32 分区。</summary>
    public static string Format(string letter, string label)
    {
        string path = @"\\.\" + letter.TrimEnd(':') + ":";
        SafeFileHandle h = CreateFileW(path, GENERIC_READ | GENERIC_WRITE,
                                       FILE_SHARE_READ | FILE_SHARE_WRITE,
                                       IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h.IsInvalid)
            throw new Exception("无法打开卷 " + path + "（错误 " + Marshal.GetLastWin32Error() + "）");

        uint ret;
        if (!DeviceIoControl(h, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out ret, IntPtr.Zero))
        {
            int e = Marshal.GetLastWin32Error();
            h.Close();
            throw new Exception("卷被占用，锁定失败（错误 " + e + "）：请关掉资源管理器里这块盘的窗口、退出杀毒扫描后重试");
        }
        try
        {
            DeviceIoControl(h, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out ret, IntPtr.Zero);

            long len = GetLength(h);
            if (len <= 0) throw new Exception("读取卷大小失败（错误 " + Marshal.GetLastWin32Error() + "）");
            if (len < 64L * 1024 * 1024) throw new Exception("卷太小，不足 64 MB");

            int bps = GetBytesPerSector(h);
            long totalSectors = len / bps;

            // 簇大小：≥32 GB 用 32 KB，≥16 GB 用 16 KB，≥8 GB 用 8 KB，其它 4 KB
            int clusterBytes = 4 * 1024;
            if (len >= 32L * 1024 * 1024 * 1024) clusterBytes = 32 * 1024;
            else if (len >= 16L * 1024 * 1024 * 1024) clusterBytes = 16 * 1024;
            else if (len >= 8L * 1024 * 1024 * 1024) clusterBytes = 8 * 1024;
            int spc = clusterBytes / bps;
            if (spc < 1) spc = 1;
            while (spc < 128 && (totalSectors / spc) > 0x0FFFFF0L)
            {
                spc *= 2;
                clusterBytes = spc * bps;
            }

            long fatSec = 1, dataSec = 0, clusters = 0;
            for (int iter = 0; iter < 8; iter++)
            {
                dataSec = totalSectors - 32 - 2 * fatSec;
                if (dataSec <= 0) throw new Exception("卷太小");
                clusters = dataSec / spc;
                long need = (clusters + 2) * 4L;
                long needSec = (need + bps - 1) / bps;
                if (needSec == fatSec) break;
                fatSec = needSec;
            }
            if (clusters > 0x0FFFFF0L) throw new Exception("簇数超限");

            // ---- 引导扇区
            byte[] boot = new byte[bps];
            boot[0] = 0xEB; boot[1] = 0x58; boot[2] = 0x90;
            PutAscii(boot, 3, "MSWIN4.1", 8);
            PutU16(boot, 11, bps);
            boot[13] = (byte)spc;
            PutU16(boot, 14, 32);          // 保留扇区
            boot[16] = 2;                  // FAT 数
            PutU16(boot, 17, 0);
            boot[21] = 0xF8;               // media
            PutU16(boot, 22, 0);           // FAT16 段留空
            PutU16(boot, 24, 63);
            PutU16(boot, 26, 255);
            PutU32(boot, 28, 0);           // 隐藏扇区
            PutU32(boot, 32, (uint)totalSectors);
            PutU32(boot, 36, (uint)fatSec);
            PutU16(boot, 40, 0);
            PutU16(boot, 42, 0);
            PutU32(boot, 44, 2);           // 根目录簇
            PutU16(boot, 48, 1);           // FSInfo 扇区
            PutU16(boot, 50, 6);           // 备份引导扇区
            boot[64] = 0x80;
            boot[66] = 0x29;
            PutU32(boot, 67, 0x20261107);
            PutAscii(boot, 71, PadLabel(label), 11);
            PutAscii(boot, 82, "FAT32   ", 8);
            boot[bps - 2] = 0x55; boot[bps - 1] = 0xAA;

            // ---- FSInfo
            byte[] fsi = new byte[bps];
            fsi[0] = (byte)'R'; fsi[1] = (byte)'R'; fsi[2] = (byte)'a'; fsi[3] = (byte)'A';
            fsi[484] = (byte)'r'; fsi[485] = (byte)'r'; fsi[486] = (byte)'a'; fsi[487] = (byte)'A';
            PutU32(fsi, 488, (uint)(clusters > 1 ? clusters - 1 : 0));
            PutU32(fsi, 492, 3);
            fsi[bps - 2] = 0x55; fsi[bps - 1] = 0xAA;

            WriteAt(h, 0, boot);
            WriteAt(h, bps, fsi);
            WriteAt(h, 6L * bps, boot);
            WriteAt(h, 7L * bps, fsi);

            // ---- 两条 FAT
            long fatBytes = fatSec * bps;
            byte[] fat = new byte[fatBytes];
            PutU32(fat, 0, 0x0FFFFFF8);
            PutU32(fat, 4, 0x0FFFFFFF);
            PutU32(fat, 8, 0x0FFFFFFF);    // 根目录簇 2 已占用
            long fat1Start = 32L * bps;
            WriteAt(h, fat1Start, fat);
            WriteAt(h, fat1Start + fatBytes, fat);

            // ---- 根目录（簇 2）写卷标项
            long rootStart = (32L + 2 * fatSec) * bps;
            byte[] root = new byte[spc * bps];
            byte[] ent = new byte[32];
            PutAscii(ent, 0, PadLabel(label), 11);
            ent[11] = 0x08;                // 卷标属性
            Array.Copy(ent, 0, root, 0, 32);
            WriteAt(h, rootStart, root);

            return string.Format("FAT32 完成：{0} GB，簇 {1} KB，共 {2:N0} 簇，FAT {3} 扇区，扇区 {4} B",
                Math.Round(len / 1073741824.0, 2), clusterBytes / 1024, clusters, fatSec, bps);
        }
        finally
        {
            try { if (!h.IsInvalid && !h.IsClosed) DeviceIoControl(h, FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out ret, IntPtr.Zero); }
            catch { }
            try { h.Close(); } catch { }
        }
    }

    static long GetLength(SafeFileHandle h)
    {
        IntPtr ob = Marshal.AllocHGlobal(8);
        try
        {
            uint got;
            if (DeviceIoControl(h, IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0, ob, 8, out got, IntPtr.Zero))
                return Marshal.ReadInt64(ob);
            return 0;
        }
        finally { Marshal.FreeHGlobal(ob); }
    }

    static int GetBytesPerSector(SafeFileHandle h)
    {
        IntPtr ob = Marshal.AllocHGlobal(32);
        try
        {
            uint got;
            if (DeviceIoControl(h, IOCTL_DISK_GET_DRIVE_GEOMETRY, IntPtr.Zero, 0, ob, 32, out got, IntPtr.Zero))
            {
                int bps = Marshal.ReadInt32(ob, 20);   // DISK_GEOMETRY.BytesPerSector
                if (bps == 512 || bps == 1024 || bps == 2048 || bps == 4096) return bps;
            }
            return 512;
        }
        finally { Marshal.FreeHGlobal(ob); }
    }

    /// <summary>定位并整段写入（走 WriteFile，绕开 .NET FileStream 的访问检查）。</summary>
    static void WriteAt(SafeFileHandle h, long off, byte[] data)
    {
        long pos;
        if (!SetFilePointerEx(h, off, out pos, 0))
            throw new Exception("定位失败（错误 " + Marshal.GetLastWin32Error() + "，偏移 " + off + "）");
        uint written;
        if (!WriteFile(h, data, (uint)data.Length, out written, IntPtr.Zero))
        {
            int e = Marshal.GetLastWin32Error();
            throw new Exception("写入失败（错误 " + e + "，偏移 " + off + "）：" + Explain(e));
        }
        if (written != data.Length)
            throw new Exception("写入不完整（" + written + "/" + data.Length + "）");
    }

    static string Explain(int e)
    {
        switch (e)
        {
            case 5: return "访问被拒绝，通常是这块盘被资源管理器或杀毒软件占用";
            case 19: return "介质写保护";
            case 21: return "设备未就绪，重新插拔后再试";
            case 23: return "数据错误（CRC），可能是坏盘或线材问题";
            case 87: return "参数错误，扇区大小不匹配";
            case 1117: return "IO 设备错误，换 USB 口或换线";
            default: return "Win32 错误 " + e;
        }
    }

    static void PutU16(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }
    static void PutU32(byte[] b, int o, uint v)
    {
        b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); b[o + 2] = (byte)(v >> 16); b[o + 3] = (byte)(v >> 24);
    }
    static void PutAscii(byte[] b, int o, string s, int n)
    {
        for (int i = 0; i < n; i++) b[o + i] = (byte)(i < s.Length ? s[i] : ' ');
    }
    static string PadLabel(string label)
    {
        if (string.IsNullOrEmpty(label)) return "NO NAME";
        string u = label.ToUpperInvariant();
        return u.Length > 11 ? u.Substring(0, 11) : u;
    }
}
