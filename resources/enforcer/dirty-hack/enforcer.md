# dirty-hack — Enforcer

## Definition
A dirty hack is a local exception added to make an observed path work while leaving the governing model or invariant known to be wrong.

## Governing Principle
Every special case is a claim about reality. If the exception has no domain meaning and exists only because the current abstraction cannot express the truth, the codebase acquires two models: the official one and the workaround that knows where it fails. Repeating this process does not stabilize the system; it distributes the real specification across escape hatches.

## Trigger When
Trigger when a fallback, bypass, compatibility shim, duplicated path, magic condition, or one-off exception is introduced primarily to avoid repairing the underlying ownership, state model, or boundary.

## Do Not Trigger When
Do not trigger for genuine domain exceptions that have stable names, explicit rules, tests, and ownership because reality itself contains the exceptional case.

## Distinguish From
compatibility-cruft preserves unjustified historical surfaces. facade-hides-mess conceals broad architecture debt. This rule is a local patch whose only semantics are “make the broken model survive here.”

## Decision Procedure
Ask what domain fact justifies the special case. If the answer is only an implementation defect or historical accident, repair the abstraction that made the case necessary.

## Nudge
A workaround without domain meaning is a second secret model. Fix the invariant or ownership boundary so the exceptional path ceases to be necessary.
