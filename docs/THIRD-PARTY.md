# Third-Party Software

See `THIRD-PARTY-NOTICES.md` for notices and upstream links.

## Redistribution Policy

Iris release packages contain Iris, the .NET self-contained runtime dependencies, Windows App SDK dependencies, and the NuGet libraries documented in notices. Release maintainers must preserve all license files emitted by publish and audit dependency changes.

Iris does not redistribute:

- Google's official Android Platform-Tools archive
- Samsung USB drivers
- Android/OEM USB drivers
- brokkR-flash

The Platform-Tools manager downloads directly from Google's official HTTPS host after user acknowledgement. Drivers remain interactive upstream prerequisites. brokkR remains an external process selected by the user, preserving a clear program and licensing boundary.

## brokkR Limitations

Iris targets the documented brokkR CLI. Output is human-oriented and may change upstream. Omitting `--target` can affect all Odin devices, so Iris never omits it. Wireless mode is not integrated. brokkR currently publishes Windows x64 and ARM64 builds; Samsung flashing integration is unavailable on x86 unless upstream adds a supported build.
