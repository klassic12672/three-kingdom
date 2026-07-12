# Architecture Decision Records

ADRs record decisions that alter or clarify the [master plan](../MASTER_PLAN.md).

## Numbering and filenames

Use monotonically increasing four-digit identifiers:

```text
0001-short-decision-title.md
0002-next-decision.md
```

Numbers are never reused, including after rejection or supersession.

## Status values

- Proposed
- Accepted
- Rejected
- Superseded by ADR-NNNN

## Required process

1. Copy [ADR_TEMPLATE.md](ADR_TEMPLATE.md).
2. Describe the conflict, alternatives, and affected master/subplan sections.
3. Accept the ADR before implementing the conflicting change.
4. Increment the master-plan version when the decision changes a locked product, architecture, release, content, contract, or milestone rule.
5. Update every affected subplan and schema.
6. Mark replaced ADRs as superseded; never delete them.

## Accepted decisions

- [ADR-0001 — Mac-first development with deferred physical Windows verification](0001-mac-first-development-deferred-physical-windows-verification.md), incorporated in master-plan version 0.2.0.
