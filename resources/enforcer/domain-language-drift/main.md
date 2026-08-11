# domain-language-drift — Main

## What To Do Now
Choose the canonical domain term for each concept within the owning context and rename code, tests, events, and docs until that vocabulary is consistent. The bounded context is who owns each concept’s canonical term; adapters are who owns translation only at a genuine border.

## Why This Matters
A stable vocabulary compresses reasoning: once a term is learned, every occurrence recalls the same concept. Drift reverses that compression. Readers must repeatedly ask which synonym or meaning is active, and cross-team discussion loses the guarantee that identical words denote identical things.

## Repair Strategy
Start from domain distinctions, not existing identifiers. Separate overloaded concepts first, then converge accidental synonyms. At bounded-context borders, preserve local language and make translation explicit instead of forcing a false global vocabulary.

## Decision Branches
- If one concept has several names in the same context, converge on the canonical term and retire synonyms.
- If one name covers several concepts, split the names before renaming anything else.
- If two contexts intentionally differ, keep both languages and add an explicit translation at the border.

## Common Wrong Fixes
- Do not create a glossary that excuses inconsistent code indefinitely.
- Do not mechanically rename distinct context concepts merely because their English words resemble each other.
- Do not introduce a third “neutral” name that becomes another synonym.
- Do not leave events and tests on the old vocabulary after the types were renamed.

## Verification
Search for retired synonyms and overloaded uses. A domain expert and a code reader should be able to point to the same term and mean the same thing in the same context. The invariant is a one-to-one map from term to concept within a context, with translation only at genuine borders.

## Done When
Names, types, tests, and documentation form one coherent language map, with explicit translation only where genuine context boundaries require it.
