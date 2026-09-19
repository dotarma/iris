# Troubleshooting

## ADB Not Detected

- Install Platform-Tools from its Iris page after reviewing Google's terms, or set the tool directory in Settings.
- Confirm `adb.exe` exists in that directory or on `PATH`.
- Reconnect the cable and accept the device's USB debugging authorization prompt.
- Run `adb devices -l`; `unauthorized` requires confirmation on the phone, while `offline` often needs reconnecting or restarting ADB.

## Fastboot Device Missing

- Confirm the phone is in bootloader/fastboot mode. Many Samsung devices use Download/Odin mode instead of standard Fastboot.
- Install the signed OEM driver interactively and check Windows Device Manager.
- Try another data-capable cable and direct USB port.

## Samsung / Odin

- Install brokkR-flash separately and set the absolute `brokkr.exe` path in Settings.
- Install Samsung's official signed USB driver interactively.
- Use **Refresh targets** and pick one exact sysname, or paste it manually. Iris always passes `--target` and never flashes all devices.
- **Reboot device into Download Mode** runs `brokkr --reboot-download` (reboot via Samsung serial port; keep the screen unlocked and Auto Blocker off). If every device is already in Odin mode, brokkR reports `No files selected` — that means nothing needed rebooting.
- Iris does not support brokkR wireless mode because its listener binds unauthenticated TCP `0.0.0.0:13579`.

## Installer

- Program Files requires an administrator instance. Choose a user-writable directory if elevation is unavailable.
- A checksum or manifest error means the package is incomplete or modified; delete it and download it again from the GitHub Release.
- Iris blocks installation when package architecture and process architecture differ.

Export Logs and Copy Diagnostics from **Logs & Diagnostics** when filing a bug. Review exports and remove device identifiers before sharing.
