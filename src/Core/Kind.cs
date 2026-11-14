using System;
using System.IO;

// 识别 ISO 属于哪个发行版家族，决定盘上布局与校验收据
public enum IsoFamily { Uos, Kylin, Ubuntu, Debian, Generic }

public class IsoProfile {
    public IsoFamily Family;
    public string Title;          // 中文显示名
    public bool HasEfiBoot;
    public string EfiPath;
    public long LargestFile;
    public string LargestPath;
    public bool FitsFat32;
    public bool IsUos { get { return Family == IsoFamily.Uos; } }

    const long Fat32MaxFile = 4294967295L; // 4 GiB - 1

    public int LoaderRank = 99;
    public string Arch = "";

    // 固件默认路径：不同架构找不同文件名，rank 越小越优先
    static int RankOf(string low) {
        if (low.EndsWith("bootx64.efi")) return 0;
        if (low.EndsWith("bootaa64.efi")) return 1;
        if (low.EndsWith("bootloongarch64.efi")) return 2;
        if (low.EndsWith("bootriscv64.efi")) return 3;
        if (low.EndsWith("bootmips64.efi") || low.EndsWith("bootmips.efi")) return 4;
        if (low.EndsWith("bootia32.efi")) return 5;
        return 90; // 非标准名（如麒麟的 BOOT.EFI），仅作兜底
    }

    static string ArchOf(string low) {
        if (low.EndsWith("bootx64.efi")) return "x86_64";
        if (low.EndsWith("bootaa64.efi")) return "arm64";
        if (low.EndsWith("bootloongarch64.efi")) return "loongarch64";
        if (low.EndsWith("bootriscv64.efi")) return "riscv64";
        if (low.EndsWith("bootia32.efi")) return "ia32";
        if (low.EndsWith("bootmips64.efi")) return "mips64";
        return "";
    }

    public static IsoProfile Detect(IsoImage img, string fileName) {
        var p = new IsoProfile();
        bool liveSq = false, casperSq = false, debInst = false, ubuntuMark = false, kylinMark = false;

        foreach (var e in img.Entries) {
            if (e.Path == null) continue;
            string low = e.Path.ToLowerInvariant();
            if (!e.IsDir) {
                if (low == "live/filesystem.squashfs") liveSq = true;
                if (low == "casper/filesystem.squashfs") casperSq = true;
                if (low.StartsWith("install.amd/") || low.StartsWith("install/")) debInst = true;
                if (low == "casper/ubuntu") ubuntuMark = true;
                if (e.Size > p.LargestFile) { p.LargestFile = e.Size; p.LargestPath = e.Path; }
                if (low.StartsWith("efi/boot/") && low.EndsWith(".efi")) {
                    // 按架构优先级挑固件默认会去找的那个名字
                    int rank = RankOf(low);
                    if (!p.HasEfiBoot || rank < p.LoaderRank) {
                        p.HasEfiBoot = true; p.EfiPath = e.Path; p.LoaderRank = rank;
                    }
                }
            }
            if (low.StartsWith("editions/") || low.StartsWith("apps-third/")) kylinMark = true;
        }

        string name = (fileName == null ? "" : fileName).ToLowerInvariant();
        if (liveSq) { p.Family = IsoFamily.Uos; p.Title = "UOS 20"; }
        else if (casperSq) {
            if (kylinMark || name.Contains("kylin")) { p.Family = IsoFamily.Kylin; p.Title = "银河麒麟桌面版"; }
            else if (ubuntuMark || name.Contains("ubuntu")) { p.Family = IsoFamily.Ubuntu; p.Title = "Ubuntu"; }
            else { p.Family = IsoFamily.Kylin; p.Title = "类 Ubuntu 发行版"; }
        }
        else if (debInst || name.Contains("debian")) { p.Family = IsoFamily.Debian; p.Title = "Debian"; }
        else { p.Family = IsoFamily.Generic; p.Title = "通用 Linux"; }

        p.FitsFat32 = p.LargestFile <= Fat32MaxFile - 65536;
        if (p.HasEfiBoot) {
            string low = p.EfiPath.ToLowerInvariant();
            p.Arch = ArchOf(low);
            if (p.Arch.Length == 0) p.Arch = "未知（自定义名）";
        }
        return p;
    }

    public string Describe() {
        var sb = new System.Text.StringBuilder();
        sb.Append(Title);
        if (HasEfiBoot) sb.Append(" · EFI 引导(" + Arch + ")");
        else sb.Append(" · 无 EFI 引导文件");
        if (!FitsFat32) sb.Append(string.Format(" · 含 {0:N2} GB 单文件（超出 FAT32）", LargestFile / 1073741824.0));
        return sb.ToString();
    }
}
