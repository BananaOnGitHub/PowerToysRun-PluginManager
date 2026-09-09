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

This is an early MVP. The gallery, local plugin scanner, release staging, bootstrap Run plugin,
transaction updater, tests, and release packaging are implemented. It still needs real-world
testing against a wider range of community release layouts before the first stable release.

## Why it is split in two

PowerToys can keep DLLs and plugin directories open while it is running. Administrator rights do
not solve an active file lock. The manager therefore follows one rule: **never modify the live
plugin directory while PowerToys is running.**

1. The app downloads a release into
   `%LOCALAPPDATA%\\PowerToysRunPluginManager\\Staging`.
2. It limits archive size, blocks ZIP path traversal, and verifies `plugin.json` plus its entry DLL.
3. After confirmation, the updater asks PowerToys to close with the normal Windows `WM_CLOSE`
   path and waits for both PowerToys and PowerLauncher to exit.
4. It moves the old plugin to a retained backup, atomically moves the staged directory into place,
   and restarts PowerToys.
5. A failed multi-step transaction rolls previously applied operations back.

The updater does not force-kill PowerToys and does not request elevation by default.

## Install a release

Download the ZIP matching the machine architecture, extract it, and run `Install.ps1`. The script
places the GUI and updater under `%LOCALAPPDATA%\\PowerToysRunPluginManager\\App`, stages the small
bootstrap plugin, and uses the same safe transaction path for its installation.

After PowerToys restarts, open Run and type `plugins`.

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
