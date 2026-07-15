# Three Kingdoms Grand Strategy

A character-driven historical grand-strategy RPG spanning late Han China, Manchuria, the Korean Peninsula, and northern/central Vietnam. Players may begin as historical or custom characters, build families and retinues, compete inside layered factions, use imperial authority, command real-time-with-pause battles, and pursue the unification of China.

## Project status

- Master-plan version: `0.2.0`
- Current milestone: **M2 — 191 campaign slice**
- Development model: solo-first, milestone-gated, Mac-first with continuous hosted Windows validation
- Engine: Godot 4.6.1 .NET with C# and .NET 10 LTS
- Release targets: Steam for Windows x64 and Apple Silicon macOS
- Launch languages: Korean and English
- Networking: single-player only

SP-00/M0, SP-01/SP-02, and SP-03 are complete with their required evidence. M2 remains active, and SP-04A character/family/household foundations are locally verified; exact-SHA hosted macOS/Windows evidence for an accepted revision remains pending. Physical Windows and production signing remain M4/SP-15 gates under ADR-0001.

## Quick start

On macOS:

```bash
./scripts/test.sh Release
./scripts/import.sh
./scripts/export.sh macos development
./scripts/smoke.sh macos
```

On Windows PowerShell:

```powershell
./scripts/test.ps1 -Configuration Release
./scripts/import.ps1
./scripts/export.ps1 -Platform windows -Flavor development
./scripts/smoke.ps1
```

All commands enforce the pinned .NET, Godot, export-template, and Git LFS prerequisites before doing work. See the [development guide](docs/DEVELOPMENT.md) for setup and the [release guide](docs/RELEASE.md) for tagged signing and packaging.

## Documentation

- [Master development plan](docs/MASTER_PLAN.md)
- [Milestone roadmap](docs/ROADMAP.md)
- [Subsystem plan index](docs/plans/README.md)
- [Architecture decision records](docs/adr/README.md)
- [Historical and content research rules](docs/content/README.md)
- [Development environment and build commands](docs/DEVELOPMENT.md)
- [Release signing and packaging](docs/RELEASE.md)
- [Simulation, replay, and save guide](docs/SIMULATION.md)
- [Content, localization, modding, and research guide](docs/CONTENT_PIPELINE.md)
- [Character, family, and household foundation](docs/CHARACTERS.md)
- [Codex implementation prompts and agent roster](docs/codex/README.md)

All subsystem work must follow the master plan. Changes to locked product scope, architecture, release targets, or content boundaries require an architecture decision record and a master-plan revision.
