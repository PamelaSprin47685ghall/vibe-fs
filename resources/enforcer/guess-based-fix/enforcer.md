# guess-based-fix — Enforcer

## Definition
A guess-based fix changes variables until a symptom disappears without establishing a causal mechanism that explains both the failure and the repair.

## Governing Principle
Correlation after a patch is not a proof of cause. Complex systems contain many interventions that can perturb timing, ordering, cache state, or error visibility enough to hide a symptom temporarily. A repair becomes engineering knowledge only when it identifies the violated invariant and predicts why the observed change restores it.

## Trigger When
Trigger when multiple speculative edits are tried until tests pass or behavior improves, with no causal explanation and no regression that isolates the mechanism.

## Do Not Trigger When
- Do not trigger for controlled experiments explicitly used to discriminate between hypotheses, provided the winning hypothesis is then verified and encoded durably.
- Do not trigger for a single reversible probe that is discarded when it fails to confirm a named hypothesis.
- Do not trigger when the causal mechanism is already proven and remaining edits are mechanical follow-through of that proof.

## Distinguish From
guessed-not-verified is an unsupported claim before action. blind-edit changes before locating ownership. This rule is trial-and-error remediation mistaken for causal repair. Tie-break: if mutation hunts a passing configuration, use this rule; if the premise was never inspected, use guessed-not-verified; if the owner was never mapped, use blind-edit.

## Decision Procedure
State a hypothesis that predicts both the failure and a discriminating observation. Test the hypothesis before finalizing the patch. Keep the smallest causal change and encode the old failure in a regression test.

## Examples
- positive: Toggle timeouts, cache flags, and retry counts until CI is green, then ship the bundle with no explanation.
- near-miss: Two hypotheses are discriminated by a targeted assertion; the winner is kept and encoded in a regression.
- counterexample: The violated invariant is already identified; one targeted patch restores it.

## Nudge
Do not accept “it passes now” as explanation. Identify the violated invariant, prove the causal mechanism, and make the regression test preserve that knowledge.
