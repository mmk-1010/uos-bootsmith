# Changelog

## 1.0.6

- Deploy an entire `payload/oem/` tree (in_chroot hooks, vendor packages) onto the install partition, so UOS can be pre-activated and third-party agents pre-installed during setup.
- Added OEM hook templates: `05_uos_activate.job`, `06_topsec_install.job`, `90_firstboot_activate.job`.

## 1.0.5

- Fixed `FAT32 format failed: access denied` on every real USB stick. `.NET FileStream` cannot write a volume handle that is locked and dismounted; all writes now go through `SetFilePointerEx` + `WriteFile`.
- Fixed drive-letter mix-up: letters are now resolved per partition (`Disk #N, Partition #M`) after an explicit `assign`, instead of trusting stale volume letters.
- `md5sum.txt` is updated automatically after patching initrd files.

## 1.0.4

- Page transition animation, disabled primary button on the second page, desaturated scheme badge, cached volume state, no more stray focus rectangle.

## 1.0.3

- Real progress, throughput and ETA when running "remove boot verification" / "restore".
- initrd write is atomic (`.new` + rename), so cancelling or a crash cannot leave a half-written file on the stick.

## 1.0.2

- New USB-stick icon, simpler and 87% smaller (7 icon sizes, PNG-compressed large frames).

## 1.0.1

- Renamed to BootSmith, dark accent palette.

## 1.00

- First public release: single/dual partition, FAT32 >32 GB, ISO reader, image-family detection, architecture-aware EFI loader detection.

## 0.0.1 — 0.09

- Rewritten from a PowerShell prototype to a self-contained C#/WPF application.
- Added UOS signature-check removal (initrd hook + OEM override), ISO compatibility probe, Kylin/Debian support.
