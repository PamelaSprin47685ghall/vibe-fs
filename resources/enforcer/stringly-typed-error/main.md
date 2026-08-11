# stringly-typed-error — Main

## What To Do Now
Replace message parsing with a closed typed error contract and move human-readable formatting to the presentation/logging boundary. The typed error contract at the producing boundary is who owns the identity invariant that machines branch on closed cases, not rendered wording.

## Why This Matters
Error prose is allowed to improve; machine semantics are not allowed to change accidentally when a sentence does. String parsing binds control flow to wording, localization, provider text, and formatting choices that were never intended as compatibility guarantees.

## Repair Strategy
Introduce domain/infrastructure error cases with stable identity and structured data. Translate provider errors once at the adapter, branch on typed cases internally, and format messages only for humans.

## Decision Branches
If a decision depends on error identity, introduce a closed typed case and branch on that case.
If the string is only for humans after the case is known, keep formatting at the presentation boundary.

## Common Wrong Fixes
- Centralize regexes into one helper and call the contract fixed.
- Freeze current wording as an unofficial protocol instead of adding typed identity.
- Map every provider string to a generic `Unknown` and still parse remaining prose downstream.

## Verification
Invariant: control semantics must be independent of rendered wording. Change the error prose without changing the typed case and program behavior must remain identical; changing the typed case should be the only way to alter control semantics.

## Done When
No machine decision depends on human wording, and error messages can be clarified or localized without risking retries, routing, or recovery behavior.
