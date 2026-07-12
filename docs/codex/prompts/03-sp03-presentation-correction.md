# Prompt Package: SP-03 Presentation Correction

Use after M0-A establishes an auditable revision. This package corrects the known gap between SP-03 domain coverage and presentation acceptance.

## Prompt

~~~text
/goal

Implement and verify the smallest SP-03 presentation correction package.

Read AGENTS.md, docs/MASTER_PLAN.md, docs/ROADMAP.md,
docs/plans/SP-03-campaign-map-regions-routes-supply.md,
docs/CAMPAIGN_GEOGRAPHY.md, geographic content/contracts/tests,
and the current CampaignMapView implementation.

Use:
- project-architect to map required known-information and presentation contracts;
- godot-presentation-engineer for assigned game presentation files;
- simulation-engineer only if a missing engine-independent query is proven necessary;
- verification-reviewer after integration.

The main agent owns shared query contracts and documentation.

Required outcomes:
1. Render historically appropriate region, district, and locality labels, not only stop names.
2. Implement the required map modes from authoritative known-information queries.
3. Remove the assumption that every non-player controller is hostile in diplomacy mode.
4. Represent supply throughput, route capacity, and disruption rather than only stored quantity.
5. Verify selection/picking, route highlighting, label collision/readability, and Korean/English behavior.
6. Add focused engine/query tests and presentation-level checks where automation is feasible.
7. Record visual evidence for every checked presentation criterion.

Constraints:
- keep authoritative political, diplomatic, intelligence, and supply rules outside presentation;
- do not leak hidden information;
- do not redesign unrelated UI;
- do not mark rendering or interaction complete from compile/import success;
- if Godot still stalls before project code, diagnose and record the boundary while leaving visual criteria unchecked.

Run focused tests, full Release tests, Godot import, the relevant scene, and visual/manual checks.
Report performance observations and any remaining environment-level blocker.
~~~
