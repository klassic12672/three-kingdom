# Three Kingdoms Grand Strategy

A character-driven historical grand-strategy RPG spanning late Han China, Manchuria, the Korean Peninsula, and northern/central Vietnam. Players may begin as historical or custom characters, build families and retinues, compete inside layered factions, use imperial authority, command real-time-with-pause battles, and pursue the unification of China.

## Project status

- Master-plan version: `0.1.0`
- Current milestone: **M0 — Source of truth and toolchain**
- Development model: solo-first, milestone-gated
- Engine: Godot 4.6.1 .NET with C# and .NET 10 LTS
- Release targets: Steam for Windows x64 and Apple Silicon macOS
- Launch languages: Korean and English
- Networking: single-player only

The SP-00 Godot/.NET baseline, SP-01 deterministic simulation/save foundation, and SP-02 validated bilingual data-content pipeline are implemented locally. Physical Windows, hosted cross-platform CI, and release-signing gates still require external verification.

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
- [Codex implementation prompts and agent roster](docs/codex/README.md)

All subsystem work must follow the master plan. Changes to locked product scope, architecture, release targets, or content boundaries require an architecture decision record and a master-plan revision.
