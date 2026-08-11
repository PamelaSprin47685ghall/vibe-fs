# stale-documentation — Main

## What To Do Now
Update every authoritative document, schema, example, or diagram whose contract changed, in the same delivery as the implementation.

## Why This Matters
Stale authoritative prose creates two competing truths. The next engineer may faithfully implement the written contract and thereby reintroduce the behavior the code had already replaced. Synchronization is therefore part of correctness, not clerical cleanup.

## Repair Strategy
Trace the changed concept to its owning documentation, update semantics rather than merely examples, and remove obsolete descriptions instead of leaving historical alternatives in current docs.

## Decision Branches
- If an owning spec/schema/how-doc would predict the old behavior, update or remove it in this delivery.
- If the text is explicitly historical, leave it and ensure current docs are the authority.
- If comments rather than owning docs are theatrical, that is comment-theater—do not treat them as this rule’s primary fix.

## Common Wrong Fixes
- Add a note saying “docs may be outdated” or duplicate the new behavior in a second document.
- Update only a changelog while leaving the how/schema pages wrong.
- Fix examples but leave the stated invariant unchanged.
- Point readers at source “as the real docs” while still publishing an authoritative stale page.

## Verification
Read the documentation as though the implementation were unavailable. It should predict the observable contract exercised by tests and current code. The invariant is: every authoritative representation tells the same present-tense contract as the implementation.

## Done When
Every authoritative representation tells the same present-tense story, and a reader cannot follow maintained documentation into an obsolete API or invariant.
