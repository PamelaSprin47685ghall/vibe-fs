# release-ladder-skipped — Enforcer

## Definition
The release ladder is skipped when verification jumps to a broad or expensive stage without first clearing the lower-level proofs that isolate simpler classes of failure. The root-cause is that a broader stage is counted as proof while a cheaper applicable rung that isolates a narrower class of failure was never cleared.

## Governing Principle
Verification levels form an information hierarchy. Pure tests prove local logic cheaply; contract tests prove boundaries; replay tests prove history; canaries prove real hosts. A high-level success cannot substitute cleanly for lower proofs because failure there has many possible causes, while success may not exercise the specific property a lower rung targets. The ladder orders evidence from narrow causality to broad realism.

## Trigger When
Trigger when applicable lower gates are bypassed and work proceeds directly to integration, canary, release, or completion claims.

## Do Not Trigger When
- A lower rung is genuinely irrelevant to the change—for example pure content with no runtime surface—and the applicable ladder is still followed.
- The change is already proven at the narrower rung and the current command is the next applicable stage.
- The skipped command does not exist in this project’s ladder and no substitute local proof was omitted.
- Re-running a higher rung after a lower-rung regression was already added and is green.

## Distinguish From
canary-skipped omits one real-boundary rung. unverified-completion-claim lacks sufficient verification generally. This rule is specifically violation of the ordered proof strategy. Tie-break: fire here when a cheaper/narrower applicable proof was skipped; fire canary-skipped when the ordered ladder was followed except the real-host rung; fire unverified-completion-claim when there is no adequate proof at any level.

## Decision Procedure
List which layers the change touches, map each to the narrowest proof that can fail there, and run from cheapest/local to broadest/real before promotion.

## Examples
- positive: a fold algebra change is declared done after a staging deploy, with no unit or property tests of the new law.
- near-miss: a docs-only edit skips runtime tests because the project’s ladder marks them inapplicable.
- counterexample: unit, contract, then canary all run in order for a host-policy change.

## Nudge
Do not use a broad test to compensate for missing narrow proofs. Climb from local causality to real-environment evidence so each failure has a small search space.
