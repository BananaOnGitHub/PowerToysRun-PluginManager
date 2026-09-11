<div align="center">
  <img src="https://raw.githubusercontent.com/BananaOnGitHub/PowerToysRun-PluginManager/main/src/PowerToysRun.PluginManager.App/Assets/plugin-manager.png" alt="PowerToys Run Plugin Manager logo" width="128" height="128">

  <h1>PowerToys Run Plugin Manager</h1>

  <p>Browse, install, update, and remove community plugins without leaving PowerToys Run.</p>

  <p>
    <a href="https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/actions/workflows/ci.yml">
      <img src="https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/actions/workflows/ci.yml/badge.svg" alt="Build status">
    </a>
    <a href="https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/releases/latest">
      <img src="https://img.shields.io/github/v/release/BananaOnGitHub/PowerToysRun-PluginManager?label=latest" alt="Latest release">
    </a>
    <a href="https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/releases">
      <img src="https://img.shields.io/github/downloads/BananaOnGitHub/PowerToysRun-PluginManager/total" alt="Total downloads">
    </a>
    <a href="https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/blob/main/LICENSE">
      <img src="https://img.shields.io/badge/license-Apache%202.0-blue" alt="Apache 2.0 license">
    </a>
  </p>

  <p>
    <a href="https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/releases/latest/download/PowerToysRun-PluginManager-Setup-win-x64.exe">
      <img src="https://img.shields.io/badge/Download-x64-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="Download for x64">
    </a>
    <a href="https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/releases/latest/download/PowerToysRun-PluginManager-Setup-win-arm64.exe">
      <img src="https://img.shields.io/badge/Download-ARM64-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="Download for ARM64">
    </a>
  </p>
</div>

## ✨ What it does

PowerToys Run Plugin Manager gives community plugins a proper home: descriptions, authors, icons,
versions, repository links, and install status in one native Windows app.

- **Discover plugins** from the main community lists and reviewed additions.
- **Search the catalog** by name, author, description, or tag.
- **Open rich plugin pages** with repository-owned icons, screenshots, descriptions, and tags.
- **Install, update, reinstall, or remove** plugins without manually moving folders around.
- **Queue multiple changes** and apply them with a single PowerToys restart.
- **See locally installed plugins**, including ones that are not yet in the catalog.
- **Get notified about manager updates** when a new stable release is available.
- **Use it with a keyboard or screen reader** through native WPF controls and UI Automation names.

The manager is a companion app, not a PowerToys fork. A tiny launcher plugin lives inside
PowerToys Run; the full gallery and installer stay independent so they can keep working across
PowerToys updates.

## 🚀 Installation

### Requirements

- Windows 10 version 2004 or newer
- [Microsoft PowerToys](https://github.com/microsoft/PowerToys/releases) with PowerToys Run enabled
- An x64 or ARM64 PC

### Quick install

1. Download the setup executable for your PC:
   - [Download x64 setup](https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/releases/latest/download/PowerToysRun-PluginManager-Setup-win-x64.exe)
   - [Download ARM64 setup](https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/releases/latest/download/PowerToysRun-PluginManager-Setup-win-arm64.exe)
2. Run Setup. Administrator rights are not required.
    - Setup will automatically restart PowerToys.
4. Open PowerToys Run with <kbd>Alt</kbd> + <kbd>Space</kbd> (or your configured shortcut) and type `plugins`.

That's it 🎉

Setup installs for the current user unless specified and adds a normal entry to **Settings → Apps → Installed
apps**. It intentionally creates no Start menu or desktop shortcut—the manager is meant to be
opened from PowerToys Run.

Windows may show an unknown-publisher warning because releases are not Authenticode-signed yet. Click `More info` on the SmartScreen popup and `Run` to bypass this.
The warning applies to the downloaded installer; it should not appear each time the installed
manager opens.

<details>
<summary><b>Portable installation</b></summary>

Portable ZIP packages are also available on the
[Releases page](https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/releases/latest).
Extract the correct archive and run `Install.ps1`. The regular setup executable is recommended for
most people because it supports clean upgrades and uninstalling through Windows Settings.

</details>

## 🔎 Usage

Open PowerToys Run and type:

```text
plugins
```

Add search text to jump straight into a filtered catalog. For example:

```text
plugins clipboard
```
or
```text
plugins weather
```

Choose **Install**, **Update**, **Reinstall**, or **Remove** on as many plugins as you like. The
changes wait in a queue while you continue browsing. When you choose **Apply changes**, the manager
closes PowerToys once, applies the full queue, and starts it again.

##  Plugin catalog

The bundled catalog currently contains **73 plugins** collected from:

- [Microsoft's third-party PowerToys Run plugin list](https://github.com/microsoft/PowerToys/blob/main/doc/thirdPartyRunPlugins.md)
- [Awesome PowerToys Run Plugins](https://github.com/hlaueriksson/awesome-powertoys-run-plugins)
- [Additional PowerToys Run plugins by ruslanlap](https://github.com/ruslanlap?tab=repositories&q=PowerToysRun)

The catalog stores descriptions, authors, source provenance, repository links, repository-owned
icons and screenshots, release versions, and architecture information when available. A scheduled
read-only workflow builds an enriched catalog artifact for review.

Catalog inclusion helps people discover a plugin; it is not an endorsement. Community plugins run
inside PowerToys under your user account, so check the linked repository before installing code you
do not trust.

##  Safe installs and updates

PowerToys can lock plugin DLLs while it is running. Running as administrator does not unlock a file
that is actively in use, so the manager never edits the live plugin directory until PowerToys has
closed.

1. Downloads are staged and checked before anything live changes.
2. Installs, updates, and removals are collected into one queue.
3. PowerToys is asked to close normally; it is not force-killed.
4. The queue is applied as one transaction and replaced plugins are kept as backups.
5. If one step fails, completed steps are rolled back before PowerToys restarts.

The manager does not request elevation by default. Staging data and transaction results are stored
under `%LOCALAPPDATA%\PowerToysRunPluginManager`.

##  Manager updates

The manager checks the latest stable GitHub release when it opens. If a newer version is available,
a small banner offers the correct setup executable for the current architecture. Nothing is
downloaded until you choose to update.

Versions follow [Semantic Versioning](https://semver.org/). `Directory.Build.props` is the source
of truth, and matching changes merged to `main` produce a `vX.Y.Z` tag and GitHub Release. See the
[changelog](https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/blob/main/CHANGELOG.md) for release notes.

##  Building from source

You will need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/BananaOnGitHub/PowerToysRun-PluginManager.git
cd PowerToysRun-PluginManager

dotnet build PowerToysRun.PluginManager.slnx -c Release
dotnet test tests/PowerToysRun.PluginManager.Core.Tests -c Release
```

To rebuild the bundled catalog without GitHub enrichment:

```powershell
dotnet run --project tools/PowerToysRun.PluginManager.CatalogBuilder -c Release -- --skip-enrichment
```

The repository can be built from another operating system, but the WPF manager and updater only
run on Windows.

<details>
<summary><b>Project structure</b></summary>

| Path | Purpose |
| --- | --- |
| `src/PowerToysRun.PluginManager.App` | WPF gallery and staging UI |
| `src/PowerToysRun.PluginManager.Core` | Catalog, scanning, validation, and transaction models |
| `src/PowerToysRun.PluginManager.RunPlugin` | Small `plugins` launcher inside PowerToys Run |
| `src/PowerToysRun.PluginManager.Bootstrapper` | Safe installer bridge for the Run launcher |
| `src/PowerToysRun.PluginManager.Updater` | Restart-aware transaction executor and rollback |
| `tools/PowerToysRun.PluginManager.CatalogBuilder` | Source merge and GitHub enrichment |
| `registry/sources.json` | Reviewed catalog source configuration |
| `catalog/catalog.json` | Bundled offline catalog |

</details>

##  Contributing

Bug reports, catalog fixes, accessibility feedback, and pull requests are welcome. If a plugin has
an unusual release layout, please include a link to one of its release assets so the installer can
be tested against the real package.

Please run the build and tests before opening a pull request.

##  License

PowerToys Run Plugin Manager is available under the
[Apache License 2.0](https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/blob/main/LICENSE).

---

<div align="center">
  Built for people who still prefer PowerToys Run <img src="https://cdn.7tv.app/emote/01G1M77D4R0004YN3NKDRR9YKJ/1x.webp" alt=":ok:" width="20">
</div>
