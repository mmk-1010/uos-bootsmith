using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>md5sum.txt 同步：打完 initrd 补丁后必须更新对应行，否则菜单里的「检查SHA256值」必然对不上。</summary>
public static class Md5Util
{
    public static string Of(string path)
    {
        using (var md5 = MD5.Create())
        using (var fs = File.OpenRead(path))
        {
            byte[] h = md5.ComputeHash(fs);
            var sb = new StringBuilder(32);
            foreach (byte b in h) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>把 md5sum.txt 里 ./live/xxx 那一行改成当前文件的真实 md5。</summary>
    public static bool Sync(string root, string relUnderRoot, string filePath)
    {
        try
        {
            string md5File = Path.Combine(root, "md5sum.txt");
            if (!File.Exists(md5File)) return false;
            string hash = Of(filePath);
            string rel = "./" + relUnderRoot.Replace('\\', '/');
            string[] lines = File.ReadAllLines(md5File);
            bool hit = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].TrimEnd();
                if (t.EndsWith(" " + rel) || t.EndsWith("*" + rel))
                {
                    lines[i] = hash + "  " + rel;
                    hit = true;
                }
            }
            if (hit) File.WriteAllLines(md5File, lines, new UTF8Encoding(false));
            return hit;
        }
        catch { return false; }
    }
}
