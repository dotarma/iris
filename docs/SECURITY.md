# Security

## Trust Boundaries

User commands, device output, settings, firmware paths, archives, downloads, update metadata, drivers, and external executables are untrusted.

## Controls

- Terminal executable allowlist is exactly `adb` and `fastboot`.
- Shells, executable paths, ADB server/transport selectors (`-H`, `-P`, `-L`, `-t`, `-d`, `-e`, `-s`, `--serial`, `--transport-id`), chaining, pipes, redirection, command substitution, and environment expansion are rejected.
- Processes use absolute paths, `UseShellExecute=false`, and structured argument lists. Output is capped and cancellation drains with a bounded timeout.
- Device-specific actions require one explicitly selected serial and matching transport. Refresh never auto-selects the first device.
- Only `adb devices/version/get-state/get-serialno`, `adb shell getprop`, `fastboot devices/--version/getvar` are treated as read-only; every other terminal command requires explicit confirmation showing the exact command and target identity.
- Downloads require HTTPS, write to random temporary files, optionally retry, verify SHA-256 when supplied, and finalize atomically.
- Platform-Tools downloads are restricted to `dl.google.com`; archive entry count, expanded size, required executables, and canonical paths are validated. A checksum is optional in the UI but recommended: without it Iris can verify structure, not provenance.
- Updates accept only ZIP packages from `github.com/dotarma/iris/releases/download/`, reject downgrades and process-architecture mismatch, and verify outer ZIP hash plus every inner file hash before offering the installer.
- brokkR requires an absolute existing file named `brokkr.exe`; every flash includes `--target`. Flashing has no automatic timeout; user cancellation force-kills the process and may leave a device in an unsafe state.
- Driver install is never automated. Iris does not disable Windows security or use undocumented silent switches.
- Installer payloads are staged and hash-verified from a locked source stream before replacing the active installation. The previous installation is retained until shortcuts and uninstall registration succeed.
- Uninstall derives its target from the running installed uninstaller or registered `InstallLocation`, validates Iris ownership, and fails loudly instead of silently swallowing deletion errors.

Logs contain process names, argument counts, statuses, sizes, and errors, but not full commands, firmware names, credentials, or device output by default.

## Reporting

Report vulnerabilities privately through GitHub Security Advisories for `dotarma/iris`. Do not include device serials, logs containing personal data, or unpublished exploit details in public issues.

## Residual Risk

Checksums prove that a package matches repository metadata, not publisher identity. Current development and CI artifacts are unsigned; production releases must add Authenticode signing/timestamping and verification before checksums are generated, plus branch protection for `main` and `update`. ADB shell commands execute on the selected Android device and can still be powerful; users should run only commands they understand. Update metadata itself is presently authenticated only by HTTPS and GitHub repository scoping, not by a detached metadata signature.
