# ADR-0001 — Mac-first development with deferred physical Windows verification

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-07-12 |
| Decision owners | Project owner |
| Supersedes | None |
| Superseded by | None |

## Context

The project is developed solo on an Apple Silicon Mac and targets both Apple Silicon macOS and Windows x64 for Steam. The original M0 gate required physical Windows launch evidence and production signing/notarization evidence before later simulation and gameplay milestones could begin. The project owner cannot currently perform reliable manual testing on Windows hardware.

Keeping those physical and credentialed checks in M0 would block unrelated headless and gameplay development. Removing Windows validation until late development would instead create unacceptable portability risk. The existing Godot/.NET project, platform boundaries, PowerShell scripts, and hosted Windows CI provide a middle path: continuous Windows compatibility without claiming physical Windows verification.

## Decision

1. Apple Silicon macOS is the primary local development, interactive testing, visual review, and performance-profiling platform through M3.
2. Windows x64 remains a locked version 1.0 release target. This is not a Mac-only product and does not authorize a later platform rewrite.
3. M0 requires exact-SHA hosted macOS and Windows build, test, import, export, and automated smoke evidence. It also requires native Apple Silicon macOS local smoke evidence.
4. M0 does not require physical Windows manual testing, Windows Authenticode verification, macOS Developer ID signing/notarization, Steam overlay verification, or public-release credentials.
5. Physical Windows x64 smoke, input/display checks, packaged save compatibility, and representative playtesting become M4 exit gates before any public demo.
6. Signing, notarization, clean-install/update behavior, Steam overlay/depot checks, and release-candidate certification remain mandatory SP-15 gates before public promotion.
7. Platform-specific APIs remain isolated behind platform or presentation boundaries. Simulation, application, content, save, and authored-data contracts remain platform-independent.
8. Evidence must continue to distinguish local macOS, hosted macOS, hosted Windows, physical Windows, signed/notarized, Steam, and release-ready states.

## Alternatives considered

### Keep physical Windows and signing verification in M0

This maximizes early platform confidence but blocks the solo developer from progressing on unrelated systems until hardware and credentials are available.

### Develop Mac-only and port to Windows later

This minimizes immediate friction but allows path, packaging, input, renderer, native-plugin, and deterministic behavior differences to accumulate until they are expensive to correct.

### Mac-first with continuous hosted Windows validation

This preserves daily development velocity while keeping Windows compilation, tests, exports, automated smoke, manifests, and checksums continuously exercised. Physical and credentialed claims remain deferred and explicit.

## Consequences

- M0 can close after both hosted platform jobs pass for one exact SHA and native macOS local evidence is recorded.
- Windows CI failures remain release-target regressions and must be fixed; they are not optional because development is Mac-first.
- The project may progress through M1–M3 without physical Windows hardware evidence.
- M4 cannot close and no public demo may ship until physical Windows testing passes.
- No public build may ship until SP-15 signing, notarization, Steam, clean-install, and release-candidate gates pass.
- Some Windows-only interaction, driver, renderer, antivirus, installer, and Steam issues may be discovered later than macOS issues, so an accessible Windows test machine must be obtained before M4.

## Affected plans and contracts

- [Master plan](../MASTER_PLAN.md)
- [Milestone roadmap](../ROADMAP.md)
- [SP-00 repository, toolchain, CI, and packaging](../plans/SP-00-repository-toolchain-ci-packaging.md)
- [SP-15 Steam, release QA, and crash reporting](../plans/SP-15-steam-release-qa-crash-reporting.md)
- [Development environment](../DEVELOPMENT.md)
- [Release signing and packaging](../RELEASE.md)

Release platforms remain unchanged. No runtime contract, schema, save, content, or build-manifest migration is introduced.

## Migration and rollout

Update the master plan to version 0.2.0, revise the M0 and M4 gates, update affected subplans and operational documentation, and keep hosted Windows CI required. Existing exact-SHA evidence remains historically valid under the labels it originally established.

## Verification

- Repository documentation validation reports no contradictory M0 physical-Windows or production-signing requirement.
- Exact-SHA macOS and Windows CI both run build, tests, import, export, automated smoke, manifest checks, and artifact upload before M0 closes.
- M4 documentation retains an explicit physical Windows x64 gate before public-demo promotion.
- SP-15 retains signing, notarization, Steam, install/update, and release-candidate gates before any public build.
