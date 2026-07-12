# SP-11 — UI, UX, Accessibility, and Tutorial

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.1.0](../MASTER_PLAN.md) |
| First required milestone | M2 |
| Dependencies | Relevant active subsystem plans; initially [SP-03](SP-03-campaign-map-regions-routes-supply.md), [SP-04](SP-04-characters-family-marriage-succession.md), [SP-05](SP-05-factions-court-emperor-diplomacy-espionage.md), [SP-07](SP-07-armies-recruitment-equipment-logistics.md), and [SP-08](SP-08-tactical-runtime-command-morale-formations.md) |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

Make the layered campaign and real-time battles understandable and controllable through information-dense but navigable keyboard-and-mouse interfaces, delegation, contextual explanation, and progressive onboarding.

## Non-goals

- Controller-first or console UI.
- Mobile/touch layouts.
- Exposing information the player character does not know.
- Replacing system clarity with frequent modal event popups.

## Requirements

- Use a consistent design system for typography, spacing, colors, icons, focus, tooltips, dialogs, alerts, tables, charts, and map overlays.
- Support Korean and English text expansion, font fallback, line breaking, and scalable UI from the beginning.
- Campaign navigation includes map, character, family/household, relationship graph, faction/subfaction, court, diplomacy, administration, economy, technology, army, intelligence, event log, and objectives.
- Every authority/control view distinguishes service, membership, office, claim, appointment, local acceptance, and actual control.
- Tooltips expose relevant calculations, sources, uncertainty, and consequences without revealing hidden information.
- Battle HUD includes speed/pause, selection, formations, command groups, ammunition/fire controls, morale/cohesion/fatigue, objectives, reinforcements, alerts, delegation, and front camera bookmarks.
- Support user-configurable automatic pauses for routs, flank/rear attacks, landings, breaches, general danger, reinforcement arrival, and major objective changes.
- Provide saved control groups, standing-order templates, and general/contingent delegation to control 54-unit battles.
- Accessibility baseline includes scalable text/UI, remappable keyboard/mouse controls, color-blind-safe overlays, non-color state indicators, subtitle controls, reduced motion, screen-shake toggles, pause-any-time, and readable contrast.
- Tutorial uses contextual objectives and a bounded 191 scenario, teaching one system at a time while preserving the actual game rules.
- Notifications aggregate routine information into reports and reserve interruption for actionable events.

## Public contracts

- UI reads immutable, knowledge-filtered query models and submits validated `CampaignCommand` or tactical commands; it never mutates domain state directly.
- `UiNotification`: severity, category, source event, recipients, localization key/arguments, actions, expiry, and aggregation key.
- `TooltipBreakdown`: localized labeled factors, values/ranges, confidence, and source references.
- `TutorialStep`: trigger, prerequisites, highlighted targets, instruction, completion condition, skip/fallback, and save-safe state.
- UI preferences persist separately from `WorldSnapshot` but are referenced by `SaveEnvelope` only where campaign-specific.

## Data flow

```text
Knowledge-filtered read models + CampaignEvents/TacticalEvents
→ screens, overlays, tooltips, notifications, and tutorial triggers
→ player input
→ commands
→ validation/result feedback
→ refreshed read models and accessible presentation
```

## Implementation workstreams

1. Define design tokens, typography/fonts, icon rules, layout primitives, input action map, and UI preference persistence.
2. Build campaign shell, map interaction, search, entity inspector, history/event log, and map modes.
3. Add character/family/relationship, political authority, court, diplomacy, administration, economy, technology, and army screens as their query contracts stabilize.
4. Build tactical selection, orders, command groups, formations, ranged controls, status, objectives, alerts, delegation, and camera bookmarks.
5. Implement notification aggregation, automatic pause, uncertainty presentation, and calculation tooltips.
6. Implement accessibility settings and automated layout/localization checks.
7. Build contextual onboarding and bounded-demo tutorial using real commands and systems.

## Edge cases and failure handling

- Missing localization or icons show development diagnostics; release validation prevents shipping placeholders.
- Commands invalidated between display and submission return the precise current reason and refresh the relevant panel.
- Unknown or uncertain values display ranges/labels rather than false precision.
- Large relationship graphs default to meaningful links with filters and search rather than rendering every acquaintance.
- UI state survives resolution changes and save/load without storing invalid direct object references.
- A skipped tutorial returns full control and records the skip without changing simulation rules.

## Performance budget

- Common panels open within 100 ms using cached read models.
- Map-mode transitions complete within 100 ms for visible data.
- UI processing does not reduce tactical frame rate by more than 5 FPS in the 54-unit stress test.
- No normal interface frame allocates unbounded collections or rebuilds the full world view.

## Tests

- Korean/English layout, font, variable, truncation, and scaling tests at supported resolutions.
- Keyboard/mouse remapping and focus/navigation tests.
- Color-blind/non-color indicator, reduced-motion, subtitle, contrast, and pause tests.
- Knowledge-filter tests preventing hidden information leakage.
- Command invalidation and precise error-feedback tests.
- 54-unit battle selection/control/alert/delegation usability sessions.
- Tutorial completion, skip, save/load, and changed-world-state tests.

## Acceptance criteria

- [ ] Core campaign systems are reachable through a consistent searchable shell.
- [ ] Authority, allegiance, appointment, claim, acceptance, and control are visually distinguishable.
- [ ] Battle HUD supports practical control/delegation of 54 units and multiple fronts.
- [ ] Korean and English layouts pass release validation at required resolutions/scales.
- [ ] Accessibility baseline and remappable keyboard/mouse controls are complete.
- [ ] Tooltips explain known calculations and uncertainty without leaking hidden state.
- [ ] The bounded 191 tutorial teaches the integrated loop using normal game rules.

## Risks

| Risk | Mitigation |
|---|---|
| Grand-strategy UI overwhelms new players | Progressive disclosure, contextual suggestions, search, summaries, and tutorial objectives. |
| UI work repeatedly breaks as systems evolve | Consume stable query contracts and shared design components; avoid direct runtime-class binding. |
| Korean/English layouts diverge late | Test both languages and scalable fonts from the first screens. |
| Battle alerts become noisy | Make categories configurable, aggregate repeats, and reserve auto-pause for player-selected triggers. |

## Deferred work

- Controller and Steam Deck-specific interface.
- Screen-reader certification beyond semantic groundwork.
- Additional localization languages.
- Player-customizable UI layouts.
