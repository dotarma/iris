# Updates

Iris reads metadata only from:

```text
https://raw.githubusercontent.com/dotarma/iris/update/update.json
```

The `update` branch is a release channel, not a development branch. Its root `update.json` follows the format in `build/update.template.json`.

Validation includes strict JSON fields, `MAJOR.MINOR.PATCH`, supported channel, release date, x86/x64/ARM64 key allowlist, current process architecture, HTTPS, trusted GitHub release hosts, expected package extension, SHA-256, and downgrade prevention.

Release ZIPs receive two integrity checks: the update metadata hashes the ZIP, then `package.manifest.json` hashes every extracted file. Extraction rejects path traversal, unexpected files, oversized archives, missing installer files, version mismatch, and architecture mismatch. Only after verification and explicit user confirmation does Iris directly start `Iris.Installer.exe`; no shell is used.

The release workflow creates `update.json` from actual release artifacts and publishes it to the `update` branch. Never hand-enter checksums.
