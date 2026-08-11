# stringly-typed-error — Main

## What To Do Now
Introduce a closed error type or stable error code. Map to messages only at the presentation edge. Branch on the typed case, never on substrings.

## Repair Strategy
Find `includes`/`match` on error messages. Replace with discriminated unions/enums. Stabilize the wire code if cross-process.

## Decision Branches
If a third party only offers strings, parse once at the adapter into your typed error and never re-parse downstream.

## Wrong Fixes
Matching translated UI text. Adding more regexes for new locales. Documenting "error message must contain X" as the API.

## Verification
Change message copy without breaking logic. Tests assert on typed cases, not substrings.

## Done When
Control flow uses typed errors; message text is presentation-only.

## Scope and Authority
In-process and API error contracts used for branching.
