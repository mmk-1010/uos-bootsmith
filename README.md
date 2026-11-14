# BootSmith

A portable Windows tool that writes **UOS / Kylin / Debian** installation images to a USB stick — including the tricks that `Rufus` and `dd` cannot do for these images.

Single 250 KB executable. No installer, no .NET SDK, no PowerShell runtime scripts, no 7-Zip, no Java. Windows 10/11 ships everything it needs.

---

## What it does

| | |
|---|---|
| **Write any Linux ISO by file copy** | Reads the ISO directly (own ISO9660 + Joliet + Rock Ridge parser), extracts it to the stick. Long filenames survive, `isohybrid` is not required. |
| **Single or dual partition** | Single: one FAT32 partition spanning the disk. Dual: 16 GB FAT32 boot partition + NTFS data partition. |
| **UOS clone image support** | Points the install kit at a `clone\*.uimg` full-system backup, so the target machine can be restored with "Install from full-system backup". |
| **FAT32 without the 32 GB wall** | Windows refuses to format volumes larger than 32 GB as FAT32. BootSmith writes the BPB/FAT itself, so a 64 GB or 256 GB stick becomes one FAT32 volume. |
| **Remove the installer's image-signature check** | For UOS 20 build family: patches the initrd verification hook in place (equal-length) and writes an OEM override, so a modified ISO still installs. |
| **OEM hooks** | Ships `oem/hooks/in_chroot/*.job` templates to pre-activate UOS or silently install third-party agents during installation. |
| **Architecture aware** | Detects which EFI loader an ISO carries (`BOOTX64.EFI`, `BOOTAA64.EFI`, `BOOTLOONGARCH64.EFI`, `BOOTRISCV64.EFI`) and labels the image accordingly. |
| **Live progress** | Throughput, ETA and remaining time while extracting, while patching initrd, and while copying the backup image. Cancel is safe: initrd is written to a temp file and atomically swapped. |

## Requirements

- Windows 10 1903+ or Windows 11 (.NET Framework 4.8 is built in)
- Administrator rights (the manifest requests elevation — it touches raw disks)
- A USB stick of 8 GB or more (26 GB+ recommended for a dual-partition UOS kit)

## Quick start

1. Download `BootSmith.exe` from [Releases](../../releases).
2. Run it, pick an ISO, optionally a `.uimg`, pick the USB disk, press **Start**.
3. Boot the target machine in **UEFI** mode.

## Repository layout

```
src/            C# 5 + WPF (XAML compiled at runtime via XamlReader)
  App.cs        entry point, splash, theme
  Ui.cs         window XAML + styles
  MainWindow.cs behaviour, background jobs, progress plumbing
  Dialog.cs     custom modal dialogs
  Core/         Iso.cs Kind.cs Initrd.cs Drives.cs Fat32.cs Job.cs Check.cs Md5Util.cs
assets/         app.ico (multi-size, 47 KB) + app.manifest (requireAdministrator)
payload/patch/  verify-patch.sh - 2932-byte initrd patch blob, embedded into the exe at build time
docs/           how-it-works.md (architecture notes)
```

## How it works (short version)

- **ISO reading** — parses the primary volume descriptor, follows the Joliet SVD when present and applies Rock Ridge `NM`/`CE`/`CL`/`RE` entries, so names are not truncated to 8.3. No external tools.
- **FAT32 formatting** — locks and dismounts the volume, then writes BPB, FSInfo, both FATs and the root directory with `SetFilePointerEx` + `WriteFile`. `.NET FileStream` cannot write a dismounted volume handle, which is why this is raw Win32.
- **Initrd patching** — the UOS initramfs is `[uncompressed cpio with microcode] + [LZ4 legacy frame]`. BootSmith decodes the LZ4 frame, overwrites the `scripts/init-bottom/deepin-squashfs-verify.sh` entry in place (same length), re-compresses as gzip and re-emits both segments. The kernel concatenates both, so the initrd still works.
- **Signature check removal** — two independent layers: the initrd hook above, and the installer's `DI_DEEPIN_SQUASHFS_VERIFY` config read from `oem/settings.ini`, which is overridden to `/bin/true`.
- **OEM hooks** — the installer runs `oem/hooks/in_chroot/*.job` as root inside the freshly installed system's chroot, in filename order. That is the official extension point; BootSmith just deploys files into it.

More detail: [docs/how-it-works.md](docs/how-it-works.md).
Chinese guides: [docs/zh-CN](docs/zh-CN).

## Limitations

- **UEFI only.** File-copy installers have no MBR boot code and no BIOS GRUB, so machines that only boot in Legacy mode are not supported.
- **FAT32 single-file limit is 4 GB.** Images containing a larger single file are rejected with a hint to use a DD-mode writer instead.
- **Windows ISOs are out of scope.**
- Removing the signature check only works for the UOS 20 build family whose initrd layout matches (target script slot of 2932 bytes). BootSmith verifies this before touching anything and skips the step when it does not match.

## Use it on your own media

This tool writes raw disks and modifies installer media. Only use it on USB sticks and images you own or are authorised to modify. The signature-check feature exists because restoring your own machine images onto your own hardware should not require a vendor signature.

## License

MIT — see [LICENSE](LICENSE).