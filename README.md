# PowerToys Run Plugin Manager

A standalone, accessible plugin gallery and transactional installer for
[PowerToys Run](https://learn.microsoft.com/windows/powertoys/run).

The project is intentionally not a PowerToys fork. The full manager lives outside PowerToys,
while a tiny Run plugin makes it easy to reach:

```text
plugins
plugins clipboard
```

The catalog currently combines 70 entries from:

- [Microsoft's third-party Run plugin list](https://github.com/microsoft/PowerToys/blob/main/doc/thirdPartyRunPlugins.md)
- [Awesome PowerToys Run Plugins](https://github.com/hlaueriksson/awesome-powertoys-run-plugins)

The catalog includes descriptions, authors, source provenance, repository links, GitHub avatar
artwork, release versions, and architecture metadata when available. A scheduled read-only
workflow produces an enriched catalog artifact for review.

## Status

The current release includes the gallery, local plugin scanner, queued installs and
removals, transactional updater, PowerToys Run launcher, conventional per-user installer, and an
in-app manager update notification. Community release layouts vary, so unsupported packages fail
during staging without touching the live plugin directory.

## Why it is split in two

PowerToys can keep DLLs and plugin directories open while it is running. Administrator rights do
not solve an active file lock. The manager therefore follows one rule: **never modify the live
plugin directory while PowerToys is running.**

1. The app downloads and validates each requested release into
   `%LOCALAPPDATA%\\PowerToysRunPluginManager\\Staging`.
2. Installs, updates, and removals collect in a queue while PowerToys keeps running.
3. When the queue is applied, the updater asks PowerToys to close with the normal Windows `WM_CLOSE`
   path and waits for both PowerToys and PowerLauncher to exit.
4. It applies the entire queue in one transaction, retains replaced plugins as backups, and
   restarts PowerToys once.
5. A failed multi-step transaction rolls previously applied operations back.

The updater does not force-kill PowerToys and does not request elevation by default.

## Install a release

Download `PowerToysRun-PluginManager-Setup-win-x64.exe` or the ARM64 setup executable from the
GitHub release and run it. Setup installs for the current user, creates a normal Apps & Features
uninstall entry, and does not require administrator rights. It deliberately creates no Start-menu
or desktop shortcut: the manager is opened through PowerToys Run. Setup stages the small Run
launcher and applies it through the same safe transaction path as every other plugin.

After PowerToys restarts, open Run and type `plugins`.

The extracted ZIP and `Install.ps1` remain available as a portable fallback.

Setup can optionally move its own downloaded executable to the Recycle Bin or permanently delete
it after installation. The option is unchecked by default, and the Recycle Bin is the default
choice when it is enabled.

## Manager updates

When the manager opens, it checks the latest stable GitHub release. A banner appears only when a
newer architecture-matched setup executable exists. Updates are downloaded after the user clicks
the banner action, then Setup starts and the manager exits so its files can be replaced.

Release binaries are not currently Authenticode-signed. Windows may therefore identify the first
downloaded installer as coming from an unknown publisher. The installed application itself is
copied by Setup rather than launched from a downloaded archive, so this warning is not repeated on
every manager launch. A trusted code-signing certificate can be added to the release pipeline
later without changing the update design.

## Versions and releases

Versions follow semantic `major.minor.patch` numbering. `Directory.Build.props` is the source of
truth for application binaries, while the Run plugin manifest mirrors that version. A version bump
and matching changelog entry merged to `main` creates the corresponding `v` tag and GitHub Release;
manually pushed matching tags are supported as well. Setup executables are attached directly to
the release so they download as `.exe` files rather than Actions artifact ZIPs.

See [CHANGELOG.md](CHANGELOG.md) for release notes.

## Build

Requirements:

- .NET 10 SDK
- Windows 10 2004 or newer for running the WPF app

```powershell
dotnet build PowerToysRun.PluginManager.slnx -c Release
dotnet test tests/PowerToysRun.PluginManager.Core.Tests -c Release
dotnet run --project tools/PowerToysRun.PluginManager.CatalogBuilder -c Release -- --skip-enrichment
```

The repository cross-targets Windows from other operating systems, but the GUI and updater only
run on Windows.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/PowerToysRun.PluginManager.App` | WPF gallery and staging UI |
| `src/PowerToysRun.PluginManager.Core` | Catalog, scanning, validation, and transaction models |
| `src/PowerToysRun.PluginManager.RunPlugin` | Small `plugins` launcher inside PowerToys Run |
| `src/PowerToysRun.PluginManager.Bootstrapper` | Installer bridge for safely adding or removing the Run launcher |
| `src/PowerToysRun.PluginManager.Updater` | Restart-aware transaction executor and rollback |
| `tools/PowerToysRun.PluginManager.CatalogBuilder` | Source merge and GitHub enrichment |
| `registry/sources.json` | Reviewed catalog source configuration |
| `catalog/catalog.json` | Bundled offline catalog |

## Accessibility

The UI uses native WPF controls, follows the Windows light/dark theme, supports keyboard navigation,
exposes control names to UI Automation, and does not encode installed/update state using color
alone.

## Trust model

Catalog inclusion is discovery, not an endorsement. Plugin code belongs to its publisher and runs
inside PowerToys after installation. Downloads are validated structurally and hashed during
staging, but a hash fetched alongside an unsigned release is not publisher verification. Future
catalog policy can add reviewed checksums or signatures without weakening the current transaction
boundary.

## License

Apache License 2.0. See [LICENSE](LICENSE).
