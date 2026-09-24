# Changelog

All notable changes to PowerToys Run Plugin Manager are documented here. Versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Automatic x64 and ARM64 development prereleases from successful CI commits, with update
  notifications for users who install a development build.
- In-window screenshot preview with animated GIF playback.
- Rich plugin detail pages with repository screenshots, longer descriptions, tags, and direct
  repository links.
- Catalog enrichment from repository READMEs, plugin manifests, and Git trees, with filtering for
  badges and unrelated sibling projects.
- Six additional plugins by ruslanlap: AI Prompt Generator, Bored, Package Manager, QuickBrain,
  Radio, and StackOverflow.
- Local installed-plugin icons and a built-in fallback when remote artwork cannot be loaded.

### Changed

- Reworked the gallery into cleaner, clickable WinUI-style rows with plugin actions on the detail
  page and accessible hover and keyboard-focus states.

## [0.3.0]

### Added

- Native WPF plugin gallery with Discover, Installed, and Updates views.
- Combined catalog sourced from Microsoft's third-party list and Awesome PowerToys Run Plugins.
- PowerToys Run launcher available through the `plugins` action keyword.
- Queued installs, updates, reinstalls, and removals with one PowerToys shutdown and restart.
- Transactional plugin replacement, retained backups, and rollback across multi-plugin changes.
- Per-user x64 and ARM64 Setup executables with a normal Apps & Features uninstall entry.
- Nonblocking manager update notification backed by stable GitHub Releases.
- Optional installer cleanup from the Setup completion page, using either the Recycle Bin or
  permanent deletion.

### Fixed

- PowerToys 0.100 plugin identity initialization compatibility.
- Date serialization in portable PowerShell installation transactions.
- Plugin matching when multiple plugins share one GitHub repository.
- Installer wait behavior when the updater restarts PowerToys.
- Narrow-window text wrapping, card clipping, button styling, and overly fast scrolling.
- Duplicate CI runs for pull-request branches.

[Unreleased]: https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/BananaOnGitHub/PowerToysRun-PluginManager/releases/tag/v0.3.0
