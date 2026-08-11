# abbreviation-anxiety — Main

## What To Do Now
Replace locally invented or ambiguous abbreviations with the domain term they compress. Do not optimize identifier length before optimizing recognition.

## Why This Matters
Code is read by reconstructing concepts from names. An abbreviation inserts an unnecessary inverse function into that process: token → guessed expansion → domain concept. The machine pays nothing for this compression; only readers pay. Repetition turns a tiny local saving into a permanent tax on maintenance.

## Repair Strategy
Rename at the owning boundary, then follow the concept through public types, parameters, events, tests, and documentation. Converge on one stable term rather than preserving aliases for the old spelling.

## Wrong Fixes
Do not add a glossary for avoidable private acronyms. Do not keep both short and long aliases. Do not replace one obscure abbreviation with another. The cure is removal of translation, not documentation of translation.

## Verification
Read the changed surface as a new contributor would. The identifier should reveal the concept without opening the implementation or consulting a local legend.

## Done When
The code speaks the same vocabulary as the domain, and understanding a name no longer requires decoding a private shorthand.
