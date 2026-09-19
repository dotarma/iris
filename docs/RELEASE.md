# Release

Iris uses semantic `MAJOR.MINOR.PATCH` versions. `Directory.Build.props` is canonical: `build/Package.ps1`, the app user agent, and the installer display read it at build/run time. The release workflow rejects tags that do not match it.

## Procedure

1. Update `<Version>` in `Directory.Build.props`.
2. Run tests, build the solution, and package all architectures.
3. Test clean install, upgrade, repair, downgrade rejection, uninstall, and user-data preservation in clean x86, x64, and ARM64 Windows VMs.
4. Authenticode-sign and timestamp production binaries with a trusted certificate before manifests/checksums are generated. Do not publish unsigned production installers; current CI output is unsigned and suitable only for development testing.
5. Commit to `main` and tag `vMAJOR.MINOR.PATCH`.
6. The release workflow builds all architectures, reruns tests, creates a draft GitHub Release with checksums, publishes generated metadata to `update`, then publishes the release. Reruns reuse the draft release idempotently.
7. Verify the raw `update.json`, download each asset independently, and compare SHA-256 before announcing the release.

GitHub branch protection should require the build workflow. The workflow needs `contents: write` to create releases and update the channel branch.
