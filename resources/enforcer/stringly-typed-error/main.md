# stringly-typed-error — Main

## What To Do Now
Replace message parsing with a closed typed error contract and move human-readable formatting to the presentation/logging boundary.

## Why This Matters
Error prose is allowed to improve; machine semantics are not allowed to change accidentally when a sentence does. String parsing binds control flow to wording, localization, provider text, and formatting choices that were never intended as compatibility guarantees.

## Repair Strategy
Introduce domain/infrastructure error cases with stable identity and structured data. Translate provider errors once at the adapter, branch on typed cases internally, and format messages only for humans.

## Wrong Fixes
Do not centralize regexes into one helper and call the contract fixed. A single brittle parser is still a prose protocol.

## Verification
Change the rendered error wording without changing the typed case. Program behavior must remain identical; changing the typed case should be the only way to alter control semantics.

## Done When
No machine decision depends on human wording, and error messages can be clarified or localized without risking retries, routing, or recovery behavior.
