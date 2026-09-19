# Architecture

Iris uses a compact layered architecture.

## Projects

- `Iris.Core` contains immutable domain records and service contracts. It has no UI or Windows App SDK dependency.
- `Iris.Services` contains direct process execution, strict command parsing, ADB/Fastboot adapters, downloads, safe archive handling, settings, structured logs, diagnostics, drivers, updates, and external brokkR process integration.
- `Iris.App` is the unpackaged WinUI 3 executable. Pages contain UI behavior only; view models own presentation state and call injected interfaces.
- `Iris.Installer` is a separate unpackaged WinUI 3 executable. Its engine verifies all payload hashes before atomic replacement, creates shortcuts, registers uninstall, and preserves user data by default.
- `Iris.Tests` tests service parsing, integrity checks, archive boundaries, and command-injection defenses.

`App.xaml.cs` is the dependency-composition root. Services are singleton where they own shared state; page view models are transient. `IDeviceSelectionService` is the single selected-target source shared across pages.

## Data Locations

- Settings: `%LocalAppData%\Iris\settings.json`
- Logs: `%LocalAppData%\Iris\logs\iris-YYYYMMDD.jsonl`
- Managed tools: `%LocalAppData%\Iris\tools` by default
- Downloads: `%UserProfile%\Downloads\Iris` by default
- Application: `C:\Program Files\Iris\App` by default

Mutable data never belongs in the installation directory.

## Process Boundary

`IProcessRunner` requires an absolute executable path, sets `UseShellExecute=false`, and uses `ArgumentList`. It asynchronously drains stdout and stderr with per-stream caps and a bounded post-cancellation drain, reports lines, applies timeout/cancellation, and kills the process tree when cancelled. Callers resolve only configured or detected known tools.

The terminal parser permits exactly `adb` or `fastboot`, rejects shell metacharacters and environment/command expansion, and rejects user-supplied server/transport/target selectors. Iris inserts the selected serial itself. Device refresh preserves the exact `(serial, transport)` selection and never auto-selects the first device.
