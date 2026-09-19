# Installer

`Iris.Installer` is a separate WinUI 3 application with Welcome, System Check, Options, Location, Components, Ready, Installing, and Completed stages.

## Package Layout

Release ZIPs contain the self-contained installer at the root and the application under `payload/Iris.App`. `package.manifest.json` authenticates every file inside the ZIP after the outer release SHA-256 is checked. `payload.manifest.json` is checked again during installation.

Run `Iris.Installer.exe` after extracting the release package. The default destination is `C:\Program Files\Iris`; start the installer as administrator for that location. A user-writable location does not require elevation.

The installer blocks downgrades, identifies existing versions, stages files before replacement, and restores the previous installation when replacement or post-install registration fails. It only installs into an empty folder or an Iris-owned folder, rejects known system folders and overlapping installer paths, and creates optional Start Menu and desktop shortcuts plus an Add/Remove Programs entry. Release packages include `LICENSE.txt` and `THIRD-PARTY-NOTICES.md`.

## Components

- Iris application is required.
- Platform-Tools, Android driver, and Samsung driver selections are currently recorded to `install-options.json` for future first-run onboarding; they do not bundle or install third-party software today.
- Drivers are never bundled or silently installed.

## Uninstall

Add/Remove Programs launches the installed Iris installer with `--uninstall`. The uninstaller derives its target from its own installed location or registered `InstallLocation`, validates Iris ownership, and uses a dedicated uninstall flow with explicit settings/tools removal checkboxes. Application files and shortcuts are removed; logs are always preserved. Locked files are scheduled for deletion at reboot through the Windows API, not through a shell.

Current development and CI artifacts are unsigned. Production releases must Authenticode-sign and timestamp executables before manifests, checksums, and ZIPs are generated, and verify signatures in the release job.
