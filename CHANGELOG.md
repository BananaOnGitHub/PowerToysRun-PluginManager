# Changelog

All notable changes to PowerToys Run Plugin Manager are documented here. Versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

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
