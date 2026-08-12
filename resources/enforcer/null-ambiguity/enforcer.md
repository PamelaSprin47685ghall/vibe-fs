# null-ambiguity — Enforcer

## Definition
Null ambiguity exists when one “no value” representation is used for several outcomes that require different meaning, authority, or caller action.

`null`, `None`, missing property, empty string, and absent collection entry can all be perfectly valid ways to represent optionality. The defect appears when absence collapses distinctions the producer still knows: **not found, not authorized, not loaded yet, failed to load, not applicable, intentionally redacted, expired, cancelled**.

Once those causes become the same empty value, downstream code starts guessing why the value is missing.

## Governing Principle
Optionality is a domain statement only when there is exactly one relevant meaning of absence at that boundary.

`Option<User>` can honestly mean “a user may or may not exist.” It becomes dishonest when `None` also means “the caller may not know whether the user exists,” “the backend failed,” and “lookup was cancelled.” Those are different worlds with different next actions.

A useful rule is behavioral, not stylistic:

> If two absence causes should lead a correct caller to different behavior, they must remain distinguishable when they cross the boundary.

Do not invent extra cases for distinctions nobody needs. But do not destroy a distinction and then rebuild it from HTTP status, side-channel flags, logs, timing, or prose.

## Trigger When
Trigger when callers need to infer the cause of absence from context because the return value preserves only present/absent. Common forms:

- `null` means both “not found” and “forbidden,” so caller checks a separate status/error field;
- `None` means “not loaded yet” before an async operation and “loaded but missing” afterward;
- cache miss, backend failure, and negative lookup all become `None`, causing incorrect retries or stale fallback;
- an empty string means “unset,” “redacted,” and “legacy malformed value” depending on record history;
- optional data plus sibling booleans (`wasFound`, `wasAuthorized`, `didFail`) reconstruct the missing result kind;
- a function catches exceptions and returns null, making failure indistinguishable from legitimate absence;
- UI/state code cannot tell “loading” from “empty” and flashes incorrect empty states;
- persistence decode maps unknown/invalid enum values to `None`, erasing corruption into normal optionality.

## Do Not Trigger When
- There is genuinely one semantic notion of absence and every correct caller handles it identically.
- A lower-level optional is immediately wrapped into a richer typed result before crossing the boundary where behavior diverges.
- The absence reason is deliberately hidden for security and **callers are required to behave identically**; indistinguishability is then the contract, not an ambiguity.
- An optional field means exactly “this datum does not apply to this case,” and no other cause shares the representation.
- A collection lookup returns optional presence while transport/auth/failure semantics are handled separately before the lookup result is exposed.

## Distinguish From
`illegal-state-representable` concerns contradictory products of fields. Null ambiguity often causes that smell when a nullable value is paired with status flags, but the root wound is earlier: distinct absence outcomes were collapsed.

`expected-failure-as-exception` concerns using exceptions for expected domain failures; a typed result may solve both, but use this rule when the key information loss is multiple absence meanings becoming one.

`stringly-typed-error` appears when the missing distinction is later reconstructed by parsing prose. Use null ambiguity for the collapsed result, stringly error for the machine control protocol built from text.

## Decision Procedure
At the producing boundary, list every reason the value can be absent.

For each reason, write the correct caller action: retry, return 404, return 403, show empty state, keep loading, abort, log corruption, fall back, do nothing.

Group reasons only if **all relevant callers are intentionally entitled to treat them the same**.

If multiple groups are currently represented by one `None/null`, the boundary is throwing away required information.

Then ask the anti-overmodeling question: does the caller actually need the distinction? If not, keep the option simple.

## Examples
- positive: `getDocument(): Document?` returns null for missing, forbidden, backend timeout, and decryption failure; callers inspect unrelated logs/status to decide next action.
- positive: UI model uses `items: Item[] | null`, where null means both “loading” and “request failed,” so spinner/error state depends on another boolean.
- positive: cache API returns `None` for “key absent” and “cache unavailable,” causing a caller to treat outages as ordinary misses.
- near-miss: `Map.tryFind` returns `Option<Value>` and all it promises is key presence in an already-valid in-memory map.
- near-miss: security API intentionally returns the same `NotAvailable` case for missing and forbidden so callers cannot distinguish existence; the indistinguishability is explicit domain policy.
- counterexample: `Found value | NotFound | Forbidden | Unavailable cause` preserves the distinctions at the point they are known.

## Nudge
“No value” is not an explanation.

If absence changes what the caller should do, keep the reason until the caller has made that decision.
