# SP-14 — Art, Animation, VFX, Audio, and Provenance

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.1.0](../MASTER_PLAN.md) |
| First required milestone | M3 |
| Dependencies | [SP-08](SP-08-tactical-runtime-command-morale-formations.md), [SP-11](SP-11-ui-ux-accessibility-tutorial.md), [SP-13](SP-13-scenarios-historical-content-pipeline.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Create a cohesive, readable, scalable visual/audio identity that supports the illustrated campaign map, instanced low-poly 2.5D battles, character drama, bilingual UI, and solo production constraints with complete provenance.

## Non-goals

- Photorealism or high-end Unreal-style rendering.
- Unique full-body models and animation sets for every historical character.
- Sexually explicit illustrations, animation, or audio through version 1.0.
- Unreviewed or rights-uncertain AI-generated release assets.

## Requirements

- Establish an art bible covering palette, silhouettes, line/ink influence, materials, scale, lighting, camera, faction colors, regional variation, UI integration, and historical reference standards.
- Campaign map uses stylized relief, illustrated textures, readable borders/routes/stops, localized landmarks, and map-mode overlays.
- Tactical visuals use modular low-poly soldier kits with shared skeletons, material/equipment variation, GPU instancing where practical, animation sharing, and LOD.
- Unit strips visibly communicate weapon/armor category, faction/army colors, flags, formation, fighting, casualties, cohesion loss, panic, and rout.
- Character presentation uses layered portrait production, attire/status/culture variants where affordable, and consistent age/health changes.
- Relationship/VN scenes remain illustrated and non-explicit, using reusable locations/compositions and fade-to-black where appropriate.
- VFX communicate missiles, impacts, fire, smoke, water, weather, morale shock, selection, objectives, and destruction without obscuring tactical state.
- Audio covers UI, ambience, weather, settlements, armies, formations, weapons, missiles, cavalry, ships, siege, morale, commands, music states, and accessibility subtitles/captions where speech conveys gameplay.
- Every asset records origin, license/rights, source references, tools, author/generator, AI-assistance details, edits, reviewer, checksum, and release status.
- Offline AI assistance is permitted only for non-explicit shipped text/portraits/illustrations with human editing and rights/provenance review; live generation is prohibited.

## Public contracts

- Asset identity uses stable namespaced `EntityId` values referenced through validated content records rather than hard-coded paths.
- `VisualProfile`: model/sprite/portrait/material/animation/VFX references, variation rules, LOD, and fallback.
- `AudioProfile`: event/category references, variation, priority, concurrency, subtitle/caption key, and fallback.
- `AssetProvenance`: contract defined in SP-02 and governed by [content rules](../content/README.md).
- Presentation consumes read-only campaign/tactical state and never controls authoritative outcomes.

## Data flow

```text
Historical/art references + production brief
→ source/AI-assisted creation
→ human edit and technical optimization
→ provenance and rights review
→ import profile and content reference
→ automated validation/performance test
→ development or release manifest
```

## Implementation workstreams

1. Create art/audio bibles, asset naming, directory/import rules, budgets, provenance register, and release checklist.
2. Prototype campaign-map relief, route/stop language, overlays, labels, and regional landmark style.
3. Prototype one modular infantry kit, one cavalry kit, flags, shared skeleton, animations, instancing, LOD, and casualty density.
4. Expand weapon/armor/equipment/cultural variants through data-driven modular parts.
5. Create portrait pipeline, age/status variants, non-explicit VN composition templates, and bilingual text integration.
6. Add combat/terrain/weather VFX and layered battle/campaign audio.
7. Profile import size, draw calls, animation, memory, shader compilation, and audio concurrency on both release platforms.

## Edge cases and failure handling

- Missing assets use visible development placeholders; release validation forbids placeholder IDs.
- Unsupported modular combinations fall back to the nearest validated visual profile without changing unit mechanics.
- Color alone never communicates faction, selection, morale, or objective state; use flags, shapes, icons, motion, and labels.
- Provenance-incomplete assets remain development-only and are excluded from release manifests.
- AI-assisted output that cannot demonstrate commercial-use rights is rejected and replaced.
- Age/relationship assets must obey SP-04's adult romance and non-explicit boundaries.

## Performance budget

- Art/animation/VFX must preserve SP-08's 60 FPS target, 30 FPS 54-unit floor, and 6 GB battle working-set limit on M2 Pro.
- Use draw-call, visible-instance, bone, texture-memory, particle, and audio-voice budgets defined by the M3 prototype and enforced in CI reports.
- Combined battle scene load target is 15 seconds on the development Mac SSD by Early Access.

## Tests

- Automated missing-reference, placeholder, import-setting, texture-size, LOD, skeleton, animation, and provenance checks.
- Draw-call, instance, animation, memory, VFX overdraw, and audio-concurrency profiling at 18/36/54 units.
- Korean/English font and portrait/VN layout tests.
- Color-blind and non-color readability reviews.
- Windows/macOS shader, material, audio, and asset-bundle smoke tests.
- Release scan rejecting explicit or unapproved AI-assisted assets.

## Acceptance criteria

- [ ] Art/audio bibles and measurable technical budgets are approved before content scaling.
- [ ] Campaign-map prototype is readable and consistent with the illustrated 2.5D direction.
- [ ] Modular instanced soldier prototype communicates equipment, formation, casualties, panic, and faction identity.
- [ ] Portrait and non-explicit VN pipelines support Korean/English presentation and age/status variation.
- [ ] VFX/audio improve state readability without exceeding performance/concurrency budgets.
- [ ] Every release asset has complete provenance and rights status.
- [ ] No explicit, placeholder, or unreviewed AI-assisted asset enters a release manifest.

## Risks

| Risk | Mitigation |
|---|---|
| Fully solo art volume exceeds capacity | Prioritize modular kits, shared rigs, reusable scenes, strict LOD, and milestone-specific content. |
| Instancing conflicts with equipment variation | Partition batches by validated modular profiles and measure variation cost in the first prototype. |
| AI-assisted assets create inconsistent style | Use art bible, constrained briefs, human repaint/edit, and rejection thresholds. |
| Visual spectacle obscures tactical information | Treat readability as an acceptance criterion and provide VFX intensity settings. |

## Deferred work

- Explicit content of any kind.
- Unique cinematic animation for the full roster.
- Full voice acting.
- Post-1.0 high-resolution asset pack.
