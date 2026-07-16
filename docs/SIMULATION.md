# Simulation and Save Foundation

SP-01 provides a Godot-free deterministic campaign kernel in `Simulation.Core` and a headless operator tool in `Tools.Simulation`.

## Deterministic model

- `EntityId` accepts lowercase namespaced IDs such as `character:liu_bei`; comparison is ordinal and IDs are immutable.
- `CampaignDate` implements a project-owned proleptic Gregorian calendar without `DateTime`, culture, time-zone, or operating-system calendar behavior.
- Campaign turns alternate between three and four daily steps. Each day resolves ordered phases, then priority, then stable ID.
- Domain changes enter through validated `CampaignCommand` values and commit through immutable `CampaignEvent` values.
- Commands and events use explicit JSON discriminators (`$type`); assembly-qualified CLR type names are never persisted.
- Random streams derive isolated state from root seed, system, and context. Reading one stream cannot advance another.
- Full, Reduced, and Aggregate tier changes carry a conservation ledger and preserve resources and pending work.
- Background work may calculate concurrently, but its proposed events are sorted before the authoritative commit phase.

Use `SimulationJson.CreateOptions()` when an external tool reads or writes simulation contracts. It rejects unmapped fields so new required data is not silently discarded.

## Character-family birth resolution

The reserved Commands-phase character-family envelope can resolve one exact due or overdue active pregnancy into one generated child. The authoritative caller supplies only a `loc:` primary-name key plus current parental culture/family/household choices and a canonical parental trait subset capped at eight. Core derives collision-checked child and birth IDs, retains the pregnancy's expected date as the child's birth date, creates `Generated/Fictional` provenance, adds exactly two biological parent links, inserts selected memberships, and removes the pregnancy. Character and pregnancy candidates are both replanned and compared before either is committed; failed or stale work changes neither subsystem and consumes no random stream. No automatic scheduling, naming, loss, twins, education, public death, inheritance, content, UI, or AI is included.

## Primary-guardian education attainment

The reserved Commands-phase character-family envelope can complete one exact ability attainment for a living, capable ward under 18. The action names the ward, exact active primary guardianship, and ability; the guardian is derived as the teacher. The teacher must be a living, capable, free adult with the ability in the immutable character-definition baseline, while the ward must lack it. Ward custody remains allowed. Success appends one immutable, causally sourced attainment and exposes the ability through the authoritative profile without changing the definition.

Education records are canonical, defensive, unique per ward/ability, and capped at 64. Preparation and event application replan the exact guardianship, character conditions, source IDs, payload, and affected IDs. Same-ability conflicts select one priority/event-ID winner, independent abilities commute, and earlier same-day guardianship or condition changes can cancel stale work. Exact-birthday education fails before Systems-phase coming of age. There is no enrollment, progress, scheduler, RNG, level, removal, decay, relationship effect, custody/access inference, public death, inheritance, content, UI, or AI behavior.

## Household-head death handoff

The reserved Commands-phase character-condition envelope supports ordinary death and the separate `resolve_household_head_death.v1` mechanism. Ordinary death remains blocked for a current household head. The head-specific action supplies the exact current condition, household, and replacement ID; the replacement must already be a different, born, living member of that household. The accepted death-v3 evidence remains unchanged inside a new composite outcome that also carries exact v1 head-change evidence.

Target death, dependent custody releases, and the head-pointer update are prepared in one character candidate before marriage, guardianship, pregnancy, and career candidates validate. No subsystem mutates until every candidate succeeds. Household membership, dead-owned wealth and estates, retained retinues, and every unrelated character record remain unchanged. Core does not infer legal-heir status, eligibility precedence, claims, regency, inheritance, player continuity, or any replacement rule beyond the exact supplied member.

## Checksums and replay

`SimulationChecksum.Compute` canonicalizes entity, pending-command, random-stream, system-version, geography, character-world including education attainments, relationship-world, career-world, character-resource-world, character-estate-holding-world, character-marriage-world, character-guardianship-world, and character-pregnancy-world order before SHA-256 hashing. Presentation state and command-validation diagnostics are excluded.

The ten-year, 1,000-entity fixture is a cross-platform golden replay. Its seed and checksum are fixed in `TierAndSoakTests`; both macOS and Windows CI run that test.

Headless commands:

```bash
dotnet run --project tools/Tools.Simulation -- soak --years 10 --entities 1000 --seed 20260712
dotnet run --project tools/Tools.Simulation -- replay --input replay.json
dotnet run --project tools/Tools.Simulation -- checksum --save campaign.save.gz
dotnet run --project tools/Tools.Simulation -- compare --left a.save.gz --right b.save.gz
dotnet run --project tools/Tools.Simulation -- inspect --save campaign.save.gz
```

A replay JSON document contains `initialSnapshot` and `commands`, using the same camel-case contract shape and explicit payload discriminators as saves.

## Save behavior

`SaveEnvelope` schema 24 records game/schema/contract versions, content manifests, root seed, a complete world snapshot including geography, runtime character-v3, relationship-v2, career-v1, character-resource-v1, character-estate-holding-v1, character-marriage-v2, character-guardianship-v1, and character-pregnancy-v1 state, bounded command/event diagnostics, and the authoritative checksum. Character definitions remain v2 while character/family/household states are v3. Before DTO use, current-schema loads require the envelope collections plus calendar, random-stream, entity, command, system-version, geography, character, relationship, career, resource, estate, marriage, guardianship, and pregnancy snapshot fields to have their expected non-null JSON shapes. Character definitions/states must expose their required descriptor, flaw, typed-link, condition, and non-null education-attainment data; relationship memories retain v2 causal-source identity; later subsystem records retain their accepted exact shapes. D1 through F3 pending commands and diagnostics use explicit outer and nested discriminators. Current death diagnostics require complete death-v3 condition, release, marriage, guardianship, pregnancy, and career evidence; household-head death additionally requires exact action pairing and complete head-change-v1 evidence. Null entries, unknown discriminators, unsupported nested versions, inconsistent causal evidence, and invalid lifecycle combinations are rejected deliberately. `SaveStore` serializes with `System.Text.Json` and compresses using `GZipStream`.

Writes are created and flushed in a same-directory temporary file, parsed again, then atomically moved into place. Autosaves retain numbered generations (`.1`, `.2`, `.3` by default). A corrupt or simulation-invalid primary is never changed during load; snapshot validation failures are normalized at the save boundary so recovery checks generations newest-first and returns the source path and diagnostic.

Required simulation content is matched by pack ID, version, and checksum. Missing required manifests block loading with a precise list. Missing optional presentation manifests may be substituted by the owning presentation subsystem.

Schema migrations are explicit, forward-only, one-version steps. Before DTO deserialization or checksum canonicalization, schema-specific raw-shape validation requires the common legacy snapshot objects and collections that canonicalization dereferences. Schemas 1 and 2 require geography to remain absent, schema 3 requires its complete geography shape, schemas 1 through 3 require character state to remain absent, schemas 1 through 4 require relationship state to remain absent, schemas 1 through 6 require career state to remain absent, schemas 1 through 7 require character-resource state to remain absent, schemas 1 through 8 require character-estate-holding state to remain absent, schemas 1 through 9 require character-marriage state to remain absent, schemas 1 through 14 require character-guardianship state to remain absent, and schemas 1 through 17 require character-pregnancy state to remain absent. Schemas 10 and 11 require exactly one character-marriage-v1 system registration; schemas 12 through 23 require marriage-v2; schemas 15 through 23 also require exactly one guardianship-v1 registration and complete guardianship-v1 state; schemas 18 through 23 additionally require exactly one pregnancy-v1 registration and complete pregnancy-v1 state. The existing D1 through E5 exclusions remain unchanged. Schema 19 forbids E6 education/runtime-character-v3 data, schema 20 forbids F0 public-death vocabulary, schema 21 forbids F1 career-death evidence/reasons, schema 22 forbids F2 custody-release evidence/death-v3, and schema 23 forbids both F3 discriminators and every unique head-change property including explicit null. Malformed explicit-null legacy data and duplicate historical system registrations are rejected as controlled compatibility failures so recovery can continue without changing any candidate generation.

Before mutation, schemas 3–23 are integrity-checked against frozen canonical shapes. Existing schema 3–22 fixture identities remain unchanged. The schema-23 fixture is a 15,227-byte exact-F2 save generated by a temporary detached-worktree harness at `ab8917a95ea064911a584cd640647374745fd2c7`; it retains a nonempty three-release F2 death, one pending ordinary death, head/replacement household state, and dead-owner inheritance inputs. Its stored checksum is `1f40e30dbaec836ac8258efbb4cf610a40968548bd2deb30abb621eb91314299`, and file SHA-256 is `e2712c9a95618867d2543f57aeedc76f2a5b1843ecaa6b78e1072a5dd6b58588`. The authenticated 23→24 step changes only registered vocabulary, preserves the snapshot and checksum, and does not rewrite accepted death-v3 history. Every migration preserves source bytes, recomputes the destination checksum when required, and the complete 1→24 chain is tested from frozen inputs with corruption, raw-shape, recovery, and source-byte-preservation negatives. Exact earlier fixture identities and structural migrations remain recorded in the character guide and SP-04 plan.

The schema-3 fixture is reconstructed from the exact schema-3 contract and serializer at baseline `4e6e83cb5a8f70b33e109d84782ef16681bd6e20`; the schema-4 and schema-5 fixtures use their accepted historical serializer contracts and nonempty character data. No repository commit used schema 1 or 2 as its current save schema, and no retained pre-geography save exists. Their fixtures therefore remain explicitly synthetic/inferred and do not establish field compatibility. Any externally retained schema-1/2 save must be checked before that compatibility is treated as field-proven.

`WorldSnapshot` remains contract version 1 through additive default-empty subsystem properties. A schema-24 save must contain complete runtime character-v3, relationship, career, resource, estate, marriage, guardianship, and pregnancy objects plus all eight exact subsystem registrations; omission, null, partial fields, or a missing, duplicate, or incompatible system version are rejected. Constructor-backed DTO failures such as an invalid character birth date or `EntityId` are normalized to compatibility diagnostics at the read boundary so autosave recovery can continue without catching fatal runtime failures. Default-empty exceptions apply only to legacy standalone snapshots whose omitted subsystem state is complete, valid, and empty; partial-null or nonempty state without its registered system version fails deliberately. A current capture writes all eight registered versions. See [the character guide](CHARACTERS.md) for the full contracts and validation boundaries.
