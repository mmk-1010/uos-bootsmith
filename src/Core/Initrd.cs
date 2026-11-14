using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// ---- cpio newc ----
public static class Cpio {
    public static int HeadEnd(byte[] a) {
        int p = 0;
        while (p + 110 <= a.Length) {
            string magic = Encoding.ASCII.GetString(a, p, 6);
            if (magic != "070701" && magic != "070702") return -1;
            int ns = (int)Convert.ToInt64(Encoding.ASCII.GetString(a, p + 94, 8), 16);
            long fs = Convert.ToInt64(Encoding.ASCII.GetString(a, p + 54, 8), 16);
            string nm = Encoding.ASCII.GetString(a, p + 110, ns - 1);
            int dOff = (p + 110 + ns + 3) / 4 * 4;
            int next = (int)((dOff + fs + 3) / 4 * 4);
            if (nm == "TRAILER!!!") return next;
            p = next;
        }
        return -1;
    }

    public static int[] Locate(byte[] a, string target) {
        int p = 0;
        while (p + 110 <= a.Length) {
            string magic = Encoding.ASCII.GetString(a, p, 6);
            if (magic != "070701" && magic != "070702") return new int[] { -1, -1 };
            int ns = (int)Convert.ToInt64(Encoding.ASCII.GetString(a, p + 94, 8), 16);
            long fs = Convert.ToInt64(Encoding.ASCII.GetString(a, p + 54, 8), 16);
            string nm = Encoding.ASCII.GetString(a, p + 110, ns - 1);
            int dOff = (p + 110 + ns + 3) / 4 * 4;
            if (nm == target) return new int[] { dOff, (int)fs };
            if (nm == "TRAILER!!!") return new int[] { -1, -1 };
            p = (int)((dOff + fs + 3) / 4 * 4);
        }
        return new int[] { -1, -1 };
    }

    public static string GetText(byte[] a, string target) {
        int[] l = Locate(a, target);
        if (l[0] < 0) return null;
        return Encoding.UTF8.GetString(a, l[0], l[1]);
    }

    public static bool ReplaceAt(byte[] a, int off, int size, byte[] data) {
        if (data.Length > size) return false;
        Array.Copy(data, 0, a, off, data.Length);
        for (int i = data.Length; i < size; i++) a[off + i] = (byte)'#';
        return true;
    }
}

// ---- LZ4 legacy frame ----
public static class Lz4Legacy {
    public static byte[] Decode(byte[] src, int start) { return Decode(src, start, null, 0); }

    public static byte[] Decode(byte[] src, int start, Action<long, long> onProgress, long total) {
        if (BitConverter.ToUInt32(src, start) != 0x184C2102u) throw new Exception("LZ4 魔数不匹配");
        int pos = start + 4;
        byte[] dst = new byte[8 * 1024 * 1024];
        var ms = new MemoryStream(320 << 20);
        while (pos + 4 <= src.Length) {
            int csize = BitConverter.ToInt32(src, pos); pos += 4;
            if (csize <= 0 || pos + csize > src.Length) break;
            int dlen = Block(src, pos, csize, dst);
            ms.Write(dst, 0, dlen);
            pos += csize;
            if (onProgress != null) onProgress(pos - start, total);
        }
        return ms.ToArray();
    }

    static int Block(byte[] src, int off, int len, byte[] dst) {
        int ip = off, iend = off + len, op = 0;
        while (ip < iend) {
            int token = src[ip++];
            int lit = token >> 4;
            if (lit == 15) { int b; do { b = src[ip++]; lit += b; } while (b == 255); }
            Array.Copy(src, ip, dst, op, lit); ip += lit; op += lit;
            if (ip >= iend) break;
            int offset = src[ip] | (src[ip + 1] << 8); ip += 2;
            int ml = token & 0xF;
            if (ml == 15) { int b; do { b = src[ip++]; ml += b; } while (b == 255); }
            ml += 4;
            int mp = op - offset;
            for (int i = 0; i < ml; i++) dst[op + i] = dst[mp + i];
            op += ml;
        }
        return op;
    }
}

// ---- initrd 读写 ----
public class InitrdFile {
    public byte[] Raw;
    public int BodyStart;
    public byte[] CpioBody;
    public string Format;   // "gzip" / "lz4"

    public static InitrdFile Load(string path) { return Load(path, null, null); }

    /// <summary>onRead(已读字节, 文件总字节)；onInflate(已解压的压缩字节, 压缩段总字节)</summary>
    public static InitrdFile Load(string path, Action<long, long> onRead, Action<long, long> onInflate) {
        byte[] raw;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20)) {
            long len = fs.Length;
            raw = new byte[len];
            int off = 0;
            while (off < raw.Length) {
                int n = fs.Read(raw, off, Math.Min(4 << 20, raw.Length - off));
                if (n <= 0) break;
                off += n;
                if (onRead != null) onRead(off, len);
            }
        }

        int p = Cpio.HeadEnd(raw);
        if (p < 0) throw new Exception("不是有效的 initrd：" + path);
        while (p < raw.Length && raw[p] == 0) p++;
        if (p + 4 >= raw.Length) throw new Exception("initrd 缺少主段：" + path);

        byte[] body;
        string fmt;
        long compTotal = raw.Length - p;

        if (raw[p] == 0x1F && raw[p + 1] == 0x8B) {
            fmt = "gzip";
            var ms = new MemoryStream(raw, p, raw.Length - p);
            using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress)) {
                var o = new MemoryStream(320 << 20);
                var buf = new byte[1 << 20];
                int n;
                while ((n = gz.Read(buf, 0, buf.Length)) > 0) {
                    o.Write(buf, 0, n);
                    if (onInflate != null) onInflate(ms.Position, compTotal);
                }
                body = o.ToArray();
            }
        } else if (raw[p] == 0x02 && raw[p + 1] == 0x21 && raw[p + 2] == 0x4C && raw[p + 3] == 0x18) {
            fmt = "lz4";
            body = Lz4Legacy.Decode(raw, p, onInflate, compTotal);
        } else {
            throw new Exception(string.Format("未知的 initrd 主段格式（偏移 {0}）：{1}", p, path));
        }

        var f = new InitrdFile();
        f.Raw = raw; f.BodyStart = p; f.CpioBody = body; f.Format = fmt;
        return f;
    }

    public void Save(string path) { Save(path, null); }

    /// <summary>onWrite(已写入磁盘的压缩字节, 0)</summary>
    public void Save(string path, Action<long, long> onWrite) {
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20)) {
            fs.Write(Raw, 0, BodyStart);
            using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal)) {
                int off = 0;
                int chunk = 1 << 20;
                while (off < CpioBody.Length) {
                    int n = Math.Min(chunk, CpioBody.Length - off);
                    gz.Write(CpioBody, off, n);
                    off += n;
                    if (onWrite != null) onWrite(fs.Position, 0);   // 真实输出字节（压缩后）
                }
            }
        }
    }

    /// <summary>分批拷贝并回报进度（用于「恢复原厂校验」）</summary>
    public static void CopyWithProgress(string src, string dst, Action<long, long> onProgress) {
        using (var i = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
        using (var o = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20)) {
            long total = i.Length, done = 0;
            var buf = new byte[4 << 20];
            int n;
            while ((n = i.Read(buf, 0, buf.Length)) > 0) {
                o.Write(buf, 0, n);
                done += n;
                if (onProgress != null) onProgress(done, total);
            }
        }
    }
}
