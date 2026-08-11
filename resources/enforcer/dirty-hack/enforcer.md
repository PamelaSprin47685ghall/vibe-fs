# dirty-hack — Enforcer

## Definition
A dirty hack is a local exception added to make an observed path work while leaving the governing model or invariant known to be wrong. The root-cause is that a known-false model is kept alive by a local exception instead of being repaired at its owner.

## Governing Principle
Every special case is a claim about reality. If the exception has no domain meaning and exists only because the current abstraction cannot express the truth, the codebase acquires two models: the official one and the workaround that knows where it fails. Repeating this process does not stabilize the system; it distributes the real specification across escape hatches.

## Trigger When
Trigger when a fallback, bypass, compatibility shim, duplicated path, magic condition, or one-off exception is introduced primarily to avoid repairing the underlying ownership, state model, or boundary.

## Do Not Trigger When
- The case is a genuine domain exception with a stable name, explicit rules, tests, and ownership because reality itself contains the exceptional case.
- A temporary spike is isolated, time-boxed, and scheduled for removal rather than shipped as the model.
- An adapter translates a foreign protocol at the boundary without claiming the core model is false.
- A documented, tested compatibility branch encodes a real external constraint, not an internal design lie.

## Distinguish From
`compatibility-cruft` preserves unjustified historical surfaces. `facade-hides-mess` conceals broad architecture debt. This rule is a local patch whose only semantics are “make the broken model survive here.” Tie-break: if the special case exists because the invariant is known false at this site, this rule owns the case.

## Decision Procedure
Ask what domain fact justifies the special case. If the answer is only an implementation defect or historical accident, repair the abstraction that made the case necessary.

## Examples
- positive: an extra `if (id === "legacy-user")` bypasses the ownership model so one path keeps working.
- near-miss: a named `InsufficientBalance` domain case with tests, because the business itself contains that refusal.
- counterexample: repair the invariant or ownership boundary and delete the bypass.

## Nudge
A workaround without domain meaning is a second secret model. Fix the invariant or ownership boundary so the exceptional path ceases to be necessary.
