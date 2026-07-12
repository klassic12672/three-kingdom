# SP-XX — Subplan title

## Metadata

| Field | Value |
|---|---|
| Status | Planned/blocked |
| Master-plan version | [0.2.0](../MASTER_PLAN.md) |
| First required milestone | M? |
| Dependencies | Linked SP files or None |
| Affected ADRs | [ADR index](../adr/README.md) |

## Goal

State the player-visible and technical outcome.

## Non-goals

- List deliberately excluded work.

## Requirements

- List required behavior and invariants.

## Public contracts

Identify use or extension of `EntityId`, `CampaignCommand`, `CampaignEvent`, `WorldSnapshot`, `BattleSetup`, `BattleResult`, `ContentManifest`, and `SaveEnvelope`. Define additional subsystem contracts without contradicting the master plan.

## Data flow

Describe inputs, authoritative processing, outputs, persistence, and presentation.

## Implementation workstreams

1. Order work by dependency and vertical value.
2. Keep work packages independently verifiable.

## Edge cases and failure handling

- Define invalid data, interrupted operations, unavailable dependencies, and recovery behavior.

## Performance budget

- State measurable CPU, memory, loading, or throughput targets.

## Tests

- Unit tests.
- Integration or headless tests.
- Presentation/manual tests where necessary.

## Acceptance criteria

- [ ] Each item is observable and required to complete the plan.

## Risks

| Risk | Mitigation |
|---|---|
| Example | Concrete mitigation |

## Deferred work

- Keep post-milestone or post-1.0 work here; it is not part of acceptance.
