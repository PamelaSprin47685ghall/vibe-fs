# abbreviation-anxiety — Main

## What To Do Now
Replace locally invented or ambiguous abbreviations with the domain term they compress. Do not optimize identifier length before optimizing recognition.

## Why This Matters
Code is read by reconstructing concepts from names. An abbreviation inserts an unnecessary inverse function into that process: token → guessed expansion → domain concept. The machine pays nothing for this compression; only readers pay. Repetition turns a tiny local saving into a permanent tax on maintenance.

## Repair Strategy
Rename at the owning boundary, then follow the concept through public types, parameters, events, tests, and documentation. Converge on one stable term rather than preserving aliases for the old spelling.

## Decision Branches
- If the abbreviation is locally invented or overloaded, spell the domain term and retire the cipher at the owning surface.
- If the same concept already has a stable full name elsewhere, converge on that name rather than inventing a second expansion.
- If the token is already the domain’s public vocabulary, leave it; this rule does not demand expansion of HTTP-class names.

## Common Wrong Fixes
- Do not add a glossary for avoidable private acronyms.
- Do not keep both short and long aliases for the same concept.
- Do not replace one obscure abbreviation with another equally private one.
- Do not “document the decoding” in comments while leaving the identifier compressed.

## Verification
Read the changed surface as a new contributor would. The identifier should reveal the concept without opening the implementation or consulting a local legend. The naming invariant is that a competent domain reader recognizes the term without a private expansion step.

## Done When
The code speaks the same vocabulary as the domain, and understanding a name no longer requires decoding a private shorthand.
