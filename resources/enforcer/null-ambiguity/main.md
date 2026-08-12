# null-ambiguity — Main

## What To Do Now
Restore the absence distinctions at the last point that still knows them.

Return a closed result with one case per **behaviorally relevant** outcome, and let callers match the result instead of reconstructing cause from null checks plus side channels.

Keep plain `Option` where one notion of absence is genuinely enough. Rich result types are not a virtue when they name distinctions nobody is allowed or required to use.

## Why This Matters
Collapsing outcomes into null is a one-way compression.

The producer knows whether an object was missing, forbidden, unavailable, malformed, cancelled, or simply not applicable. If it exports only “no value,” downstream layers cannot recover that knowledge honestly. They compensate with flags, status inspection, retries, timing assumptions, string parsing, and comments — all weaker than preserving the original fact.

This often creates secondary bugs far from the source. A cache outage becomes a cache miss. A permission failure becomes a 404 accidentally. A loading state flashes “no results.” A decode error quietly becomes “optional field absent.”

The return type should carry enough information for the next rightful decision and no more.

## Repair Strategy
Work from caller behavior backward:

1. enumerate absence causes at the producer;
2. enumerate the caller actions each cause should permit/require;
3. merge causes only when callers intentionally treat them identically;
4. create a closed result for the remaining semantic groups;
5. attach structured data only to cases that need it;
6. translate to HTTP/UI/logging representations at their own boundaries;
7. remove sibling flags/status/prose parsing that previously reconstructed the result;
8. keep security-motivated indistinguishability explicit when callers must not learn the difference.

Do not expose lower-level implementation errors one-for-one if the caller does not own those distinctions. Preserve **semantic** information, not every diagnostic detail.

## Decision Branches
- **One absence meaning:** keep `Option`/nullable representation and stop there.
- **Different caller actions:** return named cases.
- **Security requires missing/forbidden indistinguishable:** return one intentionally opaque case and keep richer cause internal/audited if appropriate.
- **Infrastructure failure vs domain absence:** keep them distinct so retry/fallback policy cannot confuse outage with normal miss.
- **Loading vs loaded-empty vs failed:** model lifecycle state explicitly rather than `value? + flags`.
- **Wire format uses null:** decode null into the domain result at ingress; do not let wire ambiguity become domain ambiguity.

## Common Wrong Fixes
- Add `wasFound`, `wasAuthorized`, `didFail`, or `isLoaded` booleans beside the nullable value. This recreates a larger illegal state space.
- Return `(value option, error option)` and rely on convention about which combinations are legal.
- Throw one generic exception for every absence reason. Information is still collapsed, just into a different channel.
- Encode absence reason in a string/status field that callers parse.
- Create ten result cases because ten low-level errors exist, even though callers intentionally handle nine of them identically. Preserve useful distinctions, not implementation trivia.
- Expose “not found vs forbidden” when security policy intentionally requires them to be indistinguishable.

## Verification
For each modeled outcome, prove callers can make the required decision from the result alone.

Then attempt former ambiguous cases:

- backend unavailable must not look like normal miss;
- forbidden must not accidentally look like success/absence unless policy intentionally requires opacity;
- loading must not look like loaded-empty;
- malformed persisted data must not silently become optional absence.

Search for side-channel flags, status peeking, and prose parsing that are no longer needed.

Invariant:

> Every absence distinction that changes authorized caller behavior survives until that decision is made.

## Done When
No downstream layer needs to ask “why is this null?” using evidence the producer already possessed.

And no result type carries distinctions that exist only to make the type look sophisticated.
