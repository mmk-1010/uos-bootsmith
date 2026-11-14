using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;

// ISO9660 + Joliet + Rock Ridge 读取器（只读，流式解压）
public class IsoEntry {
    public string Path;      // 相对路径，用 / 分隔
    public long Size;
    public uint Extent;      // LBA
    public bool IsDir;
    public bool MultiExtent;
}

public class IsoImage : IDisposable {
    const int SEC = 2048;

    FileStream _fs;
    uint _rootLba;
    uint _rootSize;
    bool _joliet;
    bool _rr;

    public string Path { get; private set; }
    public List<IsoEntry> Entries { get; private set; }
    public long TotalBytes { get; private set; }
    public int FileCount { get; private set; }
    public string VolumeId { get; private set; }

    public static IsoImage Open(string path) {
        var img = new IsoImage();
        img.Path = path;
        img._fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
        img.Parse();
        return img;
    }

    void Parse() {
        byte[] pvd = ReadSector(16);
        if (Encoding.ASCII.GetString(pvd, 1, 5) != "CD001") throw new Exception("不是 ISO9660 镜像（扇区 16 没有 CD001）");

        byte[] joliet = null;
        for (int i = 16; i < 32; i++) {
            byte[] s = ReadSector(i);
            if (s[1] != (byte)'C' || s[2] != (byte)'D' || s[3] != (byte)'0' || s[4] != (byte)'0' || s[5] != (byte)'1') break;
            int type = s[0];
            if (type == 255) break;
            if (type == 2) {
                byte e0 = s[88], e1 = s[89], e2 = s[90];
                if (e0 == 0x25 && e1 == 0x2F && (e2 == 0x40 || e2 == 0x43 || e2 == 0x45)) { joliet = s; break; }
            }
        }

        byte[] vol = joliet ?? pvd;
        _joliet = joliet != null;
        VolumeId = ReadStr(vol, 40, 32).Trim();

        _rootLba = BitConverter.ToUInt32(vol, 156 + 2);
        _rootSize = BitConverter.ToUInt32(vol, 156 + 10);

        // 探测 Rock Ridge：根目录 "." 记录的 SUA 里应有 SP
        _rr = false;
        byte[] root = ReadExtent(_rootLba, Math.Min(_rootSize, (uint)SEC));
        if (root.Length > 34) {
            int nameLen = root[32];
            int sua = 33 + nameLen; if ((nameLen & 1) == 0) sua++;
            if (sua + 7 <= root.Length && root[sua] == (byte)'S' && root[sua + 1] == (byte)'P') _rr = true;
        }

        Entries = new List<IsoEntry>();
        var seen = new HashSet<string>();
        Walk(_rootLba, _rootSize, "", 0, seen);
        foreach (var e in Entries) { if (!e.IsDir) { TotalBytes += e.Size; FileCount++; } }
    }

    static string ReadStr(byte[] b, int off, int len) {
        int n = 0;
        while (n < len && b[off + n] != 0 && b[off + n] != (byte)' ') n++;
        return Encoding.ASCII.GetString(b, off, n);
    }

    byte[] ReadSector(int lba) {
        byte[] b = new byte[SEC];
        _fs.Position = (long)lba * SEC;
        int got = 0;
        while (got < SEC) { int r = _fs.Read(b, got, SEC - got); if (r <= 0) break; got += r; }
        return b;
    }

    byte[] ReadExtent(uint lba, uint size) {
        byte[] b = new byte[size];
        _fs.Position = (long)lba * SEC;
        int got = 0;
        while (got < size) { int r = _fs.Read(b, got, (int)size - got); if (r <= 0) break; got += r; }
        return b;
    }

    byte[] ReadBody(uint lba, uint offset, uint length) {
        byte[] b = new byte[length];
        _fs.Position = (long)lba * SEC + offset;
        int got = 0;
        while (got < length) { int r = _fs.Read(b, got, (int)length - got); if (r <= 0) break; got += r; }
        return b;
    }

    void Walk(uint dirLba, uint dirSize, string prefix, int depth, HashSet<string> seen) {
        if (depth > 24) return;
        string key = dirLba + ":" + dirSize;
        if (!seen.Add(key)) return;

        byte[] data = ReadExtent(dirLba, dirSize);
        int p = 0;
        while (p < data.Length) {
            int recLen = data[p];
            if (recLen == 0) { p = ((p / SEC) + 1) * SEC; continue; }
            if (p + recLen > data.Length) break;

            int extent = (int)BitConverter.ToUInt32(data, p + 2);
            uint size = BitConverter.ToUInt32(data, p + 10);
            byte flags = data[p + 25];
            int nameLen = data[p + 32];
            bool isDir = (flags & 0x02) != 0;
            bool multi = (flags & 0x80) != 0;

            // 名字
            string name;
            if (_joliet && !_rr) {
                name = Encoding.BigEndianUnicode.GetString(data, p + 33, nameLen);
            } else {
                name = Encoding.ASCII.GetString(data, p + 33, nameLen);
            }

            // SUA
            int sua = 33 + nameLen; if ((nameLen & 1) == 0) sua++;
            int suaEnd = recLen;
            string rrName = null;
            int clLba = -1;
            bool relocated = false;

            ParseSua(data, p + sua, p + suaEnd, ref rrName, ref clLba, ref relocated, 0);

            // "." 的 name 字节是 0x00，".." 是 0x01（ASCII 媒体上看不出）
            if (nameLen <= 1 && (data[p + 33] == 0 || data[p + 33] == 1)) { p += recLen; continue; }
            if (name == "." || name == "..") { p += recLen; continue; }
            if (relocated) { p += recLen; continue; }

            if (rrName != null && rrName.Length > 0) name = rrName;
            else if (!_joliet) {
                int semi = name.IndexOf(';'); if (semi >= 0) name = name.Substring(0, semi);
                if (name.EndsWith(".")) name = name.Substring(0, name.Length - 1);
            }

            string rel = prefix.Length == 0 ? name : prefix + "/" + name;
            string clean = CleanName(rel);

            uint realLba = clLba >= 0 ? (uint)clLba : (uint)extent;

            Entries.Add(new IsoEntry { Path = clean, Size = size, Extent = realLba, IsDir = isDir, MultiExtent = multi });

            if (isDir) Walk(realLba, size, clean, depth + 1, seen);
            p += recLen;
        }
    }

    void ParseSua(byte[] data, int p, int end, ref string rrName, ref int clLba, ref bool relocated, int depth) {
        while (p + 4 <= end && depth < 4) {
            byte a = data[p], b = data[p + 1];
            int len = data[p + 2];
            if (len < 4 || p + len > end) {
                // 对齐到下一个 2 字节边界再试
                p += 2; continue;
            }
            if (a == (byte)'N' && b == (byte)'M') {
                int fl = data[p + 4];
                if ((fl & 0x01) == 0 || (fl & 0x02) == 0) {
                    rrName = (rrName ?? "") + Encoding.UTF8.GetString(data, p + 5, len - 5);
                }
            } else if (a == (byte)'C' && b == (byte)'E') {
                uint blk = BitConverter.ToUInt32(data, p + 4);
                uint off = BitConverter.ToUInt32(data, p + 8);
                uint l = BitConverter.ToUInt32(data, p + 12);
                if (l > 0 && l < 1 << 16) {
                    byte[] cont = ReadBody(blk, off, l);
                    ParseSua(cont, 0, cont.Length, ref rrName, ref clLba, ref relocated, depth + 1);
                }
            } else if (a == (byte)'C' && b == (byte)'L') {
                clLba = (int)BitConverter.ToUInt32(data, p + 4);
            } else if (a == (byte)'R' && b == (byte)'E') {
                relocated = true;
            }
            p += len;
        }
    }

    static string CleanName(string rel) {
        var sb = new StringBuilder(rel.Length);
        foreach (char c in rel) {
            switch (c) {
                case '<': case '>': case ':': case '"': case '|': case '?': case '*': sb.Append('_'); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    // 解压到目录；progress(路径, 已写字节, 总字节, 当前文件字节/总)
    public void ExtractTo(string destRoot, Action<string, long, long> onFileDone,
                          Func<bool> cancelled, Action<string> onSkip) {
        Directory.CreateDirectory(destRoot);
        long done = 0;
        byte[] buf = new byte[1 << 20];
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long lastMs = -1;

        // 先建目录
        foreach (var e in Entries) {
            if (e.IsDir) Directory.CreateDirectory(Combine(destRoot, e.Path));
        }

        foreach (var e in Entries) {
            if (e.IsDir) continue;
            if (cancelled != null && cancelled()) throw new OperationCanceledException();
            string dst = Combine(destRoot, e.Path);
            string dir = System.IO.Path.GetDirectoryName(dst);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (e.MultiExtent) { if (onSkip != null) onSkip(e.Path + "（多段文件，已跳过）"); continue; }

            _fs.Position = (long)e.Extent * SEC;
            using (var outFs = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20)) {
                long left = e.Size;
                while (left > 0) {
                    int want = (int)Math.Min(buf.Length, left);
                    int r = _fs.Read(buf, 0, want);
                    if (r <= 0) break;
                    outFs.Write(buf, 0, r);
                    left -= r; done += r;
                    if (cancelled != null && cancelled()) throw new OperationCanceledException();
                    // 大文件也要持续报进度（否则写 3.7 GB 的 squashfs 时进度条会卡住）
                    if (onFileDone != null && sw.ElapsedMilliseconds - lastMs >= 120) {
                        lastMs = sw.ElapsedMilliseconds;
                        onFileDone(e.Path, done, TotalBytes);
                    }
                }
            }
            try { File.SetLastWriteTime(dst, DateTime.Now); } catch { }
            if (onFileDone != null) onFileDone(e.Path, done, TotalBytes);
        }
    }

    static string Combine(string root, string rel) {
        return System.IO.Path.Combine(root, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
    }

    public void Dispose() { if (_fs != null) { _fs.Dispose(); _fs = null; } }
}
