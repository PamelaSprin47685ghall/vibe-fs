# stale-documentation — Main

## What To Do Now
Update every authoritative document, schema, example, or diagram whose contract changed, in the same delivery as the implementation.

## Why This Matters
Stale authoritative prose creates two competing truths. The next engineer may faithfully implement the written contract and thereby reintroduce the behavior the code had already replaced. Synchronization is therefore part of correctness, not clerical cleanup.

## Repair Strategy
Trace the changed concept to its owning documentation, update semantics rather than merely examples, and remove obsolete descriptions instead of leaving historical alternatives in current docs.

## Wrong Fixes
Do not add a note saying “docs may be outdated” or duplicate the new behavior in a second document. Authority must converge, not disclaim itself.

## Verification
Read the documentation as though the implementation were unavailable. It should predict the observable contract exercised by tests and current code.

## Done When
Every authoritative representation tells the same present-tense story, and a reader cannot follow maintained documentation into an obsolete API or invariant.
