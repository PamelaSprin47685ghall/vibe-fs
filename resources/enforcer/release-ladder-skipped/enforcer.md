# release-ladder-skipped — Enforcer

## Definition
The release ladder is skipped when verification jumps to a broad or expensive stage without first clearing the lower-level proofs that isolate simpler classes of failure.

## Governing Principle
Verification levels form an information hierarchy. Pure tests prove local logic cheaply; contract tests prove boundaries; replay tests prove history; canaries prove real hosts. A high-level success cannot substitute cleanly for lower proofs because failure there has many possible causes, while success may not exercise the specific property a lower rung targets. The ladder orders evidence from narrow causality to broad realism.

## Trigger When
Trigger when applicable lower gates are bypassed and work proceeds directly to integration, canary, release, or completion claims.

## Do Not Trigger When
Do not trigger when a lower rung is genuinely irrelevant to the change—for example pure content with no runtime surface—and the applicable ladder is still followed.

## Distinguish From
canary-skipped omits one real-boundary rung. unverified-completion-claim lacks sufficient verification generally. This rule is specifically violation of the ordered proof strategy.

## Decision Procedure
List which layers the change touches, map each to the narrowest proof that can fail there, and run from cheapest/local to broadest/real before promotion.

## Nudge
Do not use a broad test to compensate for missing narrow proofs. Climb from local causality to real-environment evidence so each failure has a small search space.
