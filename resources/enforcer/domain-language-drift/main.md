# domain-language-drift — Main

## What To Do Now
Choose the canonical domain term for each concept within the owning context and rename code, tests, events, and docs until that vocabulary is consistent.

## Why This Matters
A stable vocabulary compresses reasoning: once a term is learned, every occurrence recalls the same concept. Drift reverses that compression. Readers must repeatedly ask which synonym or meaning is active, and cross-team discussion loses the guarantee that identical words denote identical things.

## Repair Strategy
Start from domain distinctions, not existing identifiers. Separate overloaded concepts first, then converge accidental synonyms. At bounded-context borders, preserve local language and make translation explicit instead of forcing a false global vocabulary.

## Wrong Fixes
Do not create a glossary that excuses inconsistent code indefinitely. Do not mechanically rename distinct context concepts merely because their English words resemble each other.

## Verification
Search for retired synonyms and overloaded uses. A domain expert and a code reader should be able to point to the same term and mean the same thing in the same context.

## Done When
Names, types, tests, and documentation form one coherent language map, with explicit translation only where genuine context boundaries require it.
