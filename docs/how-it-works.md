# How BootSmith works

Four non-obvious problems had to be solved to make a file-copy USB writer work for UOS/Kylin/Debian images. This note explains each one.

---

## 1. Reading the ISO without external tools

Mainstream tools shell out to 7-Zip. BootSmith parses the image itself (`src/Core/Iso.cs`):

- Read sector 16 (2048 bytes) for the primary volume descriptor.
- Look for a supplementary volume descriptor with the Joliet escape sequence (`%/@`, `%/C`, `%/E`); prefer it, because Debian/Ubuntu families store UTF-8 names there.
- Check the root directory record for a Rock Ridge `SP` entry at offset 34. When present, walk `NM` (long name), `CE` (continuation area), `CL` (child link) and `RE` (relocated directory) records.
- Directory entries `.` and `..` are *not* data — their name fields are single bytes `0x00` and `0x01`. A parser that treats them as text produces one bogus empty entry per directory.

The result is a full path/size/extent table in ~70 ms for a 5 GB image, then a straight read-and-write extraction with progress callbacks every 120 ms (a single 3.7 GB squashfs would otherwise freeze the bar).

## 2. Formatting FAT32 beyond 32 GB

`Format-Volume` and `diskpart` refuse FAT32 above 32 GB, even though the filesystem supports 2 TB. BootSmith writes the filesystem itself (`src/Core/Fat32.cs`):

1. `CreateFileW("\\.\X:")` → `FSCTL_LOCK_VOLUME` → `FSCTL_DISMOUNT_VOLUME`.
2. Get the size with `IOCTL_DISK_GET_LENGTH_INFO`, the sector size with `IOCTL_DISK_GET_DRIVE_GEOMETRY`.
3. Choose cluster size by capacity (32 KB above 32 GB) and cap the cluster count below `0x0FFFFF0`.
4. Write BPB, FSInfo, the backup boot sector at 6/7, both FATs and a volume-label entry in the root directory.

**The trap:** after the volume is locked and dismounted, `.NET FileStream` throws `UnauthorizedAccessException` on every write. Raw `WriteFile` on the same handle works fine. All writes therefore go through `SetFilePointerEx` + `WriteFile` with a length check.

## 3. Patching the UOS initramfs

A UOS 20 `initrd.lz` is not a single compressed stream:

```
[ uncompressed cpio: kernel/ microcode ]  [ LZ4 legacy frame: the real initramfs ]
```

7-Zip opens only the first segment. BootSmith decodes the LZ4 legacy stream (`magic 0x184C2102`, 8 MiB blocks, classic token/literal/match decoding), gets a plain cpio, edits one entry and re-emits the file:

- The verification hook is `scripts/init-bottom/deepin-squashfs-verify.sh`, 2932 bytes.
- It is replaced **in place** with a script of exactly the same length, padded with `#`. cpio entry sizes stay identical, so nothing else in the archive shifts.
- The new body is compressed with gzip and concatenated after the original microcode segment. The kernel accepts multiple initramfs segments and unpacks them in order, so the result is a valid initrd.

Before doing any of this, the tool checks the slot size and the presence of the `livecd-installer` guard string; if the image is from a different build family it reports "not compatible" and skips the step instead of producing an unbootable stick.

## 4. Two independent signature checks

UOS 20 verifies media twice:

| Layer | Where | What it checks |
|---|---|---|
| initramfs | `deepin-squashfs-verify.sh` (runs only when `livecd-installer` is on the kernel cmdline, i.e. install/repair mode) | `boot/grub/grub.cfg` and `live/vmlinuz*` against their `_sign` files (detached PKCS#7) |
| installer | `CommonFunc::verifyISO()` reading `DI_DEEPIN_SQUASHFS_VERIFY` (default `/usr/bin/deepin-iso-verify`) | `live/filesystem.squashfs` against `filesystem.squashfs_sign` |

Both are neutralised:

- the initrd hook is replaced with a no-op that also drops a two-line `#!/bin/sh\nexit 0\n` stub over `/usr/bin/deepin-iso-verify` in the live overlay before `switch_root`;
- `oem/settings.ini` gets `DI_DEEPIN_SQUASHFS_VERIFY="/bin/true"`. The installer reads `oem/settings.ini` from the medium, and a directory takes precedence over `oem.squashfs`.

Secure Boot is unaffected: the initrd and the squashfs are not part of the firmware's signature chain — only the shim/GRUB binaries are, and those are copied byte-for-byte.

## 5. OEM hooks

`oem/hooks/in_chroot/*.job` are plain bash scripts executed as root inside the freshly installed system's chroot, in filename order. That is the vendor's own extension point — the stock image already uses it for `machine-id` regeneration and driver installation. BootSmith ships templates for license deployment, silent vendor-agent installation and a one-shot first-boot unit for anything that needs a running system (D-Bus, graphical session).

---

## Known constraints

- File-copy installers are **UEFI only**: there is no MBR boot code, no `i386-pc` GRUB, and `syslinux` cannot be installed without a working syslinux binary. Legacy-boot machines need a DD-mode writer.
- A FAT32 partition cannot hold a file ≥ 4 GB. Images that contain one are rejected up front.
- The signature-check removal is build-family specific. It is gated by an explicit compatibility probe.
