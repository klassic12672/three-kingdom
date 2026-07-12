# Master Development Plan

| Field | Value |
|---|---|
| Version | 0.2.0 |
| Date | 2026-07-12 |
| Status | Approved source of truth |
| Active milestone | M2 — 191 campaign slice |
| Development model | Solo-first, milestone-gated, Mac-first with continuous hosted Windows validation |

## 1. Product definition

### North star

Build a single-player, character-driven Three Kingdoms grand-strategy RPG combining:

- Koei-style all-officer play, relationships, appointments, and custom characters.
- Deep faction, subfaction, court, succession, and imperial politics.
- A route-based campaign map covering China, Manchuria, Korea, and northern/central Vietnam.
- Real-time-with-pause 2.5D battles focused on morale, formations, logistics, and combined land-water-siege warfare.
- One continuous unification game in which scenarios are historical bookmarks rather than separate modes.

### Version 1.0 definition of done

Version 1.0 contains:

- Windows x64 and Apple Silicon macOS Steam releases.
- Korean and English localization.
- Three complete bookmarks: 군웅할거 in late 191/early 192, 관도대전 in early 200, and 적벽대전 in autumn 208.
- The complete planned world map.
- Approximately 800–1,000 curated historical characters, supplemented by generated characters.
- Historical records and modern scholarship as canonical data; Romance material is tagged optional flavor.
- Custom characters and custom-faction formation.
- Character, family, marriage, succession, retinue, faction, subfaction, diplomacy, coalition, emperor, title, office, economy, technology, espionage, army, and battle systems.
- Up to three generals and eighteen units per army.
- Up to 54 simultaneously active tactical units, with further armies entering through reinforcement rules.
- Data-based modding for scenarios, characters, units, technologies, events, and localization.
- Campaign victory through unifying territories tagged as the scenario's `imperial_core`. Independent neighboring states outside that set may remain allies, tributaries, vassals, or optional conquests.

### Non-goals for version 1.0

- Multiplayer or networking.
- Linux, consoles, mobile, web, or Intel Mac support.
- Seamless transition between campaign and tactical maps.
- Fully simulated individual soldiers.
- A full scripting mod API.
- Live generative-AI content.
- Sexually explicit text, illustrations, animations, or gameplay.
- Adult-content DLC before or alongside version 1.0.
- Additional bookmarks such as 황건적의 난, 반동탁연합군, 삼국정립, 이릉대전, 오장원의 지는 별, 이궁지쟁, and 고평릉사변 unless promoted by a later master-plan revision.

Any explicit post-1.0 content is uncommitted and requires a separate product decision, ADR, storefront review, and master-plan revision after version 1.0 ships.

## 2. Product pillars

### Character-first play

The player controls a person inside the world rather than an abstract faction. Supported starts range from unaffiliated or custom characters through officers, administrators, generals, governors, subfaction leaders, rulers, emperors, and imperial protectors. Available authority and information depend on the character's position.

### Layered allegiance

Personal loyalty, family membership, retinue membership, subfaction membership, territorial service, faction allegiance, and loyalty to the imperial institution are separate relationships. Territory, legal appointment, local acceptance, and military control are likewise separate.

### Politics and war form one simulation

Military bookmarks retain internal political conflict, while court-centered bookmarks retain the wider unification war. Political decisions determine commanders, resources, alliances, and defections; victories and defeats change prestige, loyalty, succession, and subfaction strength.

### Historical pressure without forced outcomes

Bookmarks initialize historical conditions and active pressures. Events remain conditional. Red Cliffs, Guandu, coups, successions, and betrayals may occur differently or not at all.

### Morale-centered warfare

Frontal combat fixes, fatigues, and disrupts units. Position, morale, cohesion, command, terrain, flanking, rear attacks, rout, pursuit, and supply create decisive results.

## 3. Technical foundation

- Engine: Godot 4.6.1 .NET, pinned for initial development.
- Runtime: latest supported .NET 10 LTS patch, pinned with `global.json`.
- Language: C# for runtime, simulation, tools, and tests.
- Development host: M2 Pro MacBook Pro with 16 GB memory.
- Release targets: Windows x64 and Apple Silicon macOS through Steam.
- Source control: Git with Git LFS for binary assets.
- Automation: macOS and Windows CI runners, with a physical Windows test machine required before the public demo.

### Platform development and verification policy

- Apple Silicon macOS is the primary local development, interactive testing, visual-review, and performance-profiling platform through M3.
- Windows x64 remains a continuous release target: exact-SHA hosted Windows build, tests, import, export, automated smoke, manifests, and artifacts are required from M0 onward.
- Hosted Windows automation is portability evidence, not physical Windows evidence.
- Physical Windows x64 smoke, input/display checks, packaged save compatibility, and representative playtesting are required before M4 can close or a public demo can ship.
- Developer ID signing/notarization, Authenticode, Steam overlay/depot checks, clean installs/updates, and release-candidate certification remain mandatory SP-15 public-promotion gates.
- Platform-specific behavior stays isolated behind presentation or platform boundaries; authoritative simulation, application, content, and save contracts remain platform-independent.

This policy is governed by [ADR-0001](adr/0001-mac-first-development-deferred-physical-windows-verification.md).

### Assembly boundaries

```text
Simulation.Core
    Deterministic domain logic with no Godot dependency.

Game.Application
    Commands, queries, saves, scenario loading, and battle transitions.

Game.Content
    Schemas, stable IDs, validation, localization, and mod loading.

Game.Presentation
    Godot campaign map, tactical scenes, UI, animation, audio, and input.

Game.Platform.Steam
    Steam initialization and platform services.

Tools.ContentPipeline
    Headless imports, validation, reports, and source audits.
```

Dependencies flow downward from presentation/platform code toward application, content, and pure simulation. `Simulation.Core` never references Godot or Steam.

### Stable public contracts

- `EntityId`: namespaced immutable identifier; deleted IDs are never reused.
- `CampaignCommand`: a player or AI decision submitted to the simulation.
- `CampaignEvent`: an authoritative result emitted by simulation logic.
- `WorldSnapshot`: complete serializable campaign state.
- `BattleSetup`: participants, units, commanders, terrain, weather, fronts, objectives, and reinforcement conditions.
- `BattleResult`: casualties, morale/cohesion, prisoners, officers, supplies, territorial effects, memories, and political consequences.
- `ContentManifest`: pack identity, version, dependencies, load order, and checksums.
- `SaveEnvelope`: schema version, game version, enabled manifests, seed, snapshot, and recent command history.

Subplans may extend these contracts but may not replace or bypass them.

## 4. Data, saves, localization, and modding

- Author content in validated JSON; use CSV for localization and bulk tabular import where appropriate.
- Use stable namespaced IDs for every cross-referenced entity.
- Use `System.Text.Json` for authored data and versioned saves.
- Compress saves with `GZipStream` and provide explicit migrations between all publicly released save schemas.
- Load built-in content first and mods afterward in declared dependency order.
- Reject invalid content packs without corrupting existing saves.
- Ship Korean and English together; all player-facing text uses localization keys.
- Record historical citations, confidence, disputes, and historical-versus-Romance tags.

Offline AI assistance may be used for shipped text, portraits, and non-explicit illustrations only when model/service, source material, prompts, edit history, human review, and rights provenance are recorded. Live generation is prohibited.

## 5. Campaign simulation

- Campaign turns alternate between three and four days, creating two turns per week.
- Each campaign turn resolves through deterministic daily steps.
- The same seed and command sequence must produce matching state checksums on Windows and macOS.
- Use three simulation tiers:
  - Full: player-relevant actors, neighboring powers, active wars, active plots, and directly relevant characters.
  - Reduced: distant historical actors and factions using simplified daily logic.
  - Aggregate: peaceful distant localities resolved once per campaign turn.
- Tier transitions preserve people, resources, promises, pending events, and political state.
- Background threads may perform pure calculations, but state commits occur in deterministic order.

### Campaign map

- Present an illustrated 2.5D relief map with explicit political and logistical overlays.
- Armies traverse a graph of routes and intermediate stops; territorial adjacency alone does not permit movement.
- Track actual control, legal appointment, local acceptance, and claims independently.
- Use universal `region`, `district`, and `locality` tiers while displaying historically appropriate local terms.
- Provide political, claim, administration, diplomacy, supply, population, culture, intelligence, and route map modes.

## 6. Character and political simulation

Characters have abilities, aptitudes, traits, flaws, memories, ambitions, relationships, offices, titles, reputations, families, households, personal resources, and retinues.

Political structures include:

- Imperial institution and court.
- Major faction or state.
- Subfaction or political bloc.
- Personal retinue.
- Family and household.
- Coalition, alliance, vassalage, or tributary relationship.

The emperor or imperial controller may issue directives, grant 작위, 관직, 군호, and territorial appointments, and create legal claims. Imperial recognition does not automatically transfer actual control or local acceptance.

Marriage may be political or romantic. Romance, concubinage, widow remarriage, household incorporation, children, and succession are non-explicit systems. Interactive romance routes require adult characters aged 18 or older. Historically relevant childhood betrothals remain political-only until both characters are adults. Coercive household actions produce political and character consequences and are never presented as successful romance.

## 7. Military and battle simulation

### Army structure

```text
Army
├── General 1: up to 6 units
├── General 2: up to 6 units
└── General 3: up to 6 units
```

Generals have unit permissions, restricted unit types, unlockable aptitudes, formation knowledge, command capacity, and relationships with other commanders.

### Tactical runtime

- Load tactical battles as separate Godot scenes.
- Run authoritative unit simulation at a fixed 20 Hz and interpolate presentation up to 60 FPS.
- Treat each formation as the simulation entity; instanced low-poly soldiers visualize strength, equipment, attacks, casualties, panic, and rout.
- Track manpower, morale, cohesion, formation integrity, fatigue, ammunition, experience, and command state.
- Support pause plus 0.25x, 0.5x, 1x, and 2x speeds.
- Support formation movement, facing, flank/rear detection, charge shock, ranged/melee switching, ammunition selection, fire-at-will, rout, rally, pursuit, surrender, and aftermath.
- Allow up to 54 simultaneously active units; additional forces enter through reinforcement rules.
- Combined maps may contain field, river/water, shoreline, port, siege line, wall, and settlement-interior fronts.
- Multiple factions retain separate commanders, objectives, coordination, and withdrawal decisions even when fighting on the same side.

## 8. Content targets

### Early Access opening

- One complete 군웅할거/191 bookmark playable toward unification.
- Full world-map topology with functional baseline systems across all included regions.
- Approximately 300–400 curated historical characters plus generated characters.
- Korean and English complete for shipped content.

### Version 1.0

- Three complete base bookmarks.
- Approximately 800–1,000 curated historical characters.
- Bespoke regional names, governments, units, technologies, and sufficient events across the full map.
- Generated archetypes: 장수형, 모사형, 관료형, 다재다능형, and 팔방미인형.
- Custom starts including wandering armies, local magnates, imperial appointees, defeated households, rebels, and frontier leaders.

## 9. Milestones

The authoritative milestone gates are maintained in [ROADMAP.md](ROADMAP.md). No public calendar date is promised until the M4 integrated vertical slice establishes measurable solo-development velocity.

M0 proves reproducible local macOS and hosted macOS/Windows development paths. M4 adds physical Windows validation before the public demo. Production signing, notarization, Steam, and release-candidate certification remain SP-15 gates before public promotion.

## 10. Subplans and governance

The authoritative subsystem index is [plans/README.md](plans/README.md). Every subplan must state its dependencies, first required milestone, public contracts, tests, acceptance criteria, risks, and deferred work.

| ID | Subplan | First milestone | Dependencies |
|---|---|---|---|
| SP-00 | Repository, toolchain, CI, and packaging | M0 | None |
| SP-01 | Simulation, calendar, determinism, and saves | M1 | SP-00 |
| SP-02 | Content, localization, modding, and research | M1 | SP-01 |
| SP-03 | Campaign map, regions, routes, and supply | M2 | SP-01, SP-02 |
| SP-04 | Characters, family, marriage, and succession | M2 | SP-01, SP-02 |
| SP-05 | Factions, court, emperor, diplomacy, and espionage | M2 | SP-03, SP-04 |
| SP-06 | Population, economy, administration, and technology | M2 | SP-03, SP-04, SP-05 |
| SP-07 | Armies, recruitment, equipment, and logistics | M2 | SP-03, SP-04, SP-05, SP-06 |
| SP-08 | Tactical runtime, command, morale, and formations | M3 | SP-01, SP-07 |
| SP-09 | Siege, naval, shoreline, and combined battles | M5 | SP-08 |
| SP-10 | Strategic/tactical AI and simulation tiers | M2 | SP-01, SP-03–SP-09 |
| SP-11 | UI, UX, accessibility, and tutorial | M2 | Relevant active systems |
| SP-12 | Custom/generated characters and faction founding | M2 | SP-02, SP-04–SP-07 |
| SP-13 | Scenarios and historical content pipeline | M2 | SP-02–SP-12 |
| SP-14 | Art, animation, VFX, audio, and provenance | M3 | SP-08, SP-11, SP-13 |
| SP-15 | Steam, release QA, and crash reporting | M0 groundwork; M4 public validation | SP-00, SP-11, SP-14 |

Changes to platforms, engine, product pillars, version 1.0 scope, content boundary, stable contracts, or milestone exit criteria require an [ADR](adr/README.md), a master-plan version increment, and updates to affected subplans.

## 11. Performance and quality targets

On the M2 Pro development Mac at 1920x1080 medium settings:

- Normal battles target 60 FPS.
- A 54-unit worst case must not remain below 30 FPS.
- Battle working memory must remain at or below 6 GB.
- Campaign-turn resolution in the vertical slice must complete within 3 seconds.
- Save and load operations must complete within 5 seconds.

Automated verification includes deterministic cross-platform replay, content validation, save migration, long-running simulation, battle behavior, combined fronts, localization coverage, relationship eligibility, AI-asset provenance, and release packaging.
