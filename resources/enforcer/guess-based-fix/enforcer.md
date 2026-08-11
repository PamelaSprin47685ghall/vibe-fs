# guess-based-fix — Enforcer

## Definition
A guess-based fix changes variables until a symptom disappears without establishing a causal mechanism that explains both the failure and the repair.

## Governing Principle
Correlation after a patch is not a proof of cause. Complex systems contain many interventions that can perturb timing, ordering, cache state, or error visibility enough to hide a symptom temporarily. A repair becomes engineering knowledge only when it identifies the violated invariant and predicts why the observed change restores it.

## Trigger When
Trigger when multiple speculative edits are tried until tests pass or behavior improves, with no causal explanation and no regression that isolates the mechanism.

## Do Not Trigger When
Do not trigger for controlled experiments explicitly used to discriminate between hypotheses, provided the winning hypothesis is then verified and encoded durably.

## Distinguish From
guessed-not-verified is an unsupported claim before action. blind-edit changes before locating ownership. This rule is trial-and-error remediation mistaken for causal repair.

## Decision Procedure
State a hypothesis that predicts both the failure and a discriminating observation. Test the hypothesis before finalizing the patch. Keep the smallest causal change and encode the old failure in a regression test.

## Nudge
Do not accept “it passes now” as explanation. Identify the violated invariant, prove the causal mechanism, and make the regression test preserve that knowledge.
