# math-flavored-name — Main

## What To Do Now
Rename pseudo-mathematical identifiers to the concrete domain concepts they represent unless the code is implementing a formal model where the notation is genuinely standard.

## Why This Matters
A symbol is only shorter when its meaning is already shared. Otherwise the missing characters reappear as reader effort: inspect assignments, trace types, decode comments, then remember the result. Domain names spend a few bytes to save repeated reconstruction.

## Repair Strategy
Translate each abstract identifier into the noun or action a domain discussion would use. Preserve established mathematical notation only inside narrow scopes where the formula or algorithm makes the mapping explicit.

## Wrong Fixes
Do not replace single letters with equally abstract words such as `value`, `item`, or `data`. The goal is semantic specificity, not merely alphabetic length.

## Verification
Read declarations and call sites without implementation detail. The names should reveal what business or algorithmic fact each value carries.

## Done When
Notation compresses established mathematics where appropriate, and ordinary domain code speaks ordinary domain language everywhere else.
