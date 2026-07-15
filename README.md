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

SP-00/M0, SP-01/SP-02, and SP-03 are complete with their required evidence. M2 and SP-04 remain active. SP-04A character/family/household foundations have passing exact-SHA hosted macOS arm64 and Windows x64 evidence for `eaa3aaf3a0687a231d2a3441e5be4954e905e9ea`; see the [SP-04A hosted report](docs/evidence/SP-04A-EXACT-SHA-eaa3aaf.md). SP-04B-L relationship/memory work is locally verified and has passing [exact-SHA hosted macOS arm64/Windows x64 evidence](docs/evidence/SP-04B-EXACT-SHA-ff7420f.md) at `ff7420fbefb5dcb7d42dcff82746d61c39d02b7a`. Full SP-04 remains incomplete and SP-05 remains blocked. Physical Windows and production signing remain M4/SP-15 gates under ADR-0001.

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
