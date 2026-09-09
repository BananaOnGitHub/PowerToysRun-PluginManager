# Contributing

Issues and pull requests are welcome. Please keep catalog changes separate from installer logic
when practical.

For a new catalog source, add a reviewed definition to `registry/sources.json` and update the
parser tests. Do not add repositories found only through broad search directly to the trusted
catalog; put them through review first.

Installer changes must preserve the shutdown, staging, path-validation, backup, and rollback
invariants documented in `AGENTS.md`.
