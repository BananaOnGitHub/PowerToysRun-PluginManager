# Repository guidance

## Non-negotiable behavior

- Never write, replace, or delete files under the live PowerToys Run plugin directory while a
  PowerToys or PowerLauncher process is running.
- Stage downloads under the manager data directory, validate them there, then apply through a
  transaction after graceful PowerToys shutdown.
- Do not add force-kill behavior as an automatic fallback.
- Keep the GUI/updater installation outside the PowerToys plugin directory. Only the bootstrap
  launcher belongs inside it.
- Treat catalog entries as untrusted metadata and downloaded archives as untrusted input.

## Verification

Run these before submitting changes:

```powershell
dotnet build PowerToysRun.PluginManager.slnx -c Release
dotnet test tests/PowerToysRun.PluginManager.Core.Tests -c Release
dotnet run --project tools/PowerToysRun.PluginManager.CatalogBuilder -c Release -- --skip-enrichment --output artifacts/catalog.json
```

The generated catalog should contain at least 50 entries. Windows CI builds the x64 and ARM64
bootstrap variants.

## Style

- Nullable reference types remain enabled.
- Warnings are errors.
- Prefer small, testable core services over filesystem or network logic in WPF code-behind.
- Preserve the Apache-2.0 license.
