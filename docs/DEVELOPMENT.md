# Development Environment

## Pinned tools

| Tool | Required version |
|---|---|
| Godot .NET editor | `4.6.1.stable.mono.official.14d19694e` |
| Godot .NET export templates | `4.6.1.stable.mono` |
| .NET SDK | `10.0.301` |
| Git LFS | `3.7.1` or newer |

The SDK is pinned by [`global.json`](../global.json); engine and template hashes are recorded in [`build/toolchain.json`](../build/toolchain.json). The preflight scripts reject a different SDK, engine, or template version with an actionable message. Install the .NET-enabled Godot editor and its matching templates from the [Godot download archive](https://godotengine.org/download/archive/4.6.1-stable/), and install the SDK from the [.NET 10 download page](https://dotnet.microsoft.com/download/dotnet/10.0).

The documented editor is Visual Studio Code with the recommendations in [`.vscode/extensions.json`](../.vscode/extensions.json). A paid IDE is not required. The shared settings intentionally contain no machine-local editor path; configure such a path in VS Code user settings if the extension cannot discover Godot.

On macOS, install Xcode command-line tools and accept the Xcode license. On Windows, install a current Windows 11 SDK so `signtool.exe` is available for release signing. Git LFS must be installed before checkout so binary objects are materialized.

## Repository layout

| Path | Purpose |
|---|---|
| `src/` | Pure simulation, application, content, and optional platform assemblies |
| `game/` | Godot project, presentation assembly, scenes, and imported game assets |
| `data/` | Authored data, schemas, localization, content manifest, and provenance registers |
| `tools/` | Headless content-pipeline, simulation/replay, and repository validation tools |
| `tests/` | Automated repository, architecture, simulation, save, and build-manifest tests |
| `scripts/` | Local/CI validation, build, export, smoke, signing, and packaging entry points |
| `build/` | Version and toolchain pins |
| `artifacts/` | Ignored exports, derived templates, packages, and manifests |

The Godot project needs its adjacent classic [`Game.Presentation.sln`](../game/Game.Presentation.sln) for .NET export. The repository-wide [`ThreeKingdom.slnx`](../ThreeKingdom.slnx) is the normal CLI and VS Code entry point.

## Commands

macOS and Unix shell commands:

```bash
./scripts/preflight.sh --require-templates
./scripts/validate.sh
./scripts/build.sh Release
./scripts/test.sh Release
./scripts/import.sh
./scripts/export.sh macos development
./scripts/export.sh windows development
./scripts/smoke.sh macos
```

Windows PowerShell commands:

```powershell
./scripts/preflight.ps1 -RequireTemplates
./scripts/validate.ps1
./scripts/build.ps1 -Configuration Release
./scripts/test.ps1 -Configuration Release
./scripts/import.ps1
./scripts/export.ps1 -Platform windows -Flavor development
./scripts/smoke.ps1
```

The macOS arm64 export script derives an arm64-only custom template from the pinned official Universal 2 template using `lipo`. The derived archive is written under `artifacts/` and is never committed. This keeps the official template as the source of truth while satisfying the Apple Silicon-only target.

For deterministic simulation soaks, replay/checksum comparison, and save inspection, use `Tools.Simulation` as described in the [simulation guide](SIMULATION.md). The standard test scripts run its golden checksum and save-compatibility suite on both CI platforms.

Content validation is part of every `validate`, `build`, and `test` invocation. Direct validation, normalization, report, and fixture commands are documented in the [content pipeline guide](CONTENT_PIPELINE.md); normalized outputs belong under ignored `artifacts/`.

The scripts resolve a symlinked Godot command to the real application-bundle executable so the editor can find its bundled `GodotSharp` assemblies. Set `GODOT_BIN` only when automatic discovery does not find the pinned editor. Set `GODOT_EXPORT_TEMPLATES_DIR` only for a nonstandard templates location; neither value belongs in source control.

## Dependency locks and clean checkout

Every project commits `packages.lock.json`. Normal restore uses `--locked-mode`; a dependency change requires an intentional `dotnet restore ThreeKingdom.slnx --use-lock-file --force-evaluate` followed by review of every lock-file change. Platform export restores use ignored intermediate lock files so an export cannot mutate the committed cross-platform locks.

For a clean-checkout verification:

1. Clone the repository with Git LFS installed.
2. Run `git lfs pull` and `git lfs fsck`.
3. Install only the pinned SDK, Godot editor, and matching templates.
4. Run the validation, test, import, export, and native smoke commands above.

Repository validation rejects broken local documentation links, missing required layout files, signing material in tracked paths, common machine-local absolute paths, missing LFS rules, malformed LFS pointers, and missing LFS objects. CI uses the same scripts.

Before the first commit exists, validation audits cached and nonignored untracked candidate files. For every working-tree LFS pointer it requires valid OID and size fields, verifies the object byte count, and recomputes SHA-256. History-dependent `git lfs fsck` begins once `HEAD` exists; the absence of `HEAD` alone is not a validation failure. This pre-HEAD mode establishes baseline readiness but is not clean-checkout or same-revision evidence.

## CI behavior

[`ci.yml`](../.github/workflows/ci.yml) runs on a macOS 15 arm64 runner and Windows Server 2025 x64 runner. Each job checks out LFS objects, installs the exact .NET SDK and official Godot artifacts, validates documentation, restores locked dependencies, builds/tests, performs a headless import, exports the native development preset, and launches the result as a smoke test. Artifacts are explicitly named unsigned and are not release-ready.

Build-manifest schema 2 records the canonical top-level pack checksum as `contentManifestChecksum` and the aggregate validated registry checksum as `contentRegistryChecksum`. Both must match the same source revision before an artifact can support acceptance evidence.
