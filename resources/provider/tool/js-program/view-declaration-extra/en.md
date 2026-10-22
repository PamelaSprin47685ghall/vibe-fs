State the next request's view with two flat top-level parameters: `prefixUntil` and `thenMap`. They are not wrapped in a `view` object — the old wrapper is retired, and a call that still carries `view` is refused with `VIEW_DECLARATION_INVALID`. Both fields are required once either is supplied: `prefixUntil` is a non-negative integer anchor number. Content below it is in the cheap cache prefix; content at or above it is recomputed at full price each turn. The current `thenMap` silently ignores entries below `prefixUntil`, leaving the prefix unchanged. Raising this boundary never restores text compressed earlier.

The 80/20 rule: in no less than 20% of turns, you must prune no less than 80% of the context. In the other 80% of turns, aim to prune no less than 20% of the context.

Summarize material known to be unnecessary early, while it is still outside the protected prefix, instead of paying full price for it on later requests. Keep useful, stable material in the cheap cache prefix rather than rewriting it just to look concise. Cheap caching does not make obsolete detail worth keeping forever, and deep consolidation must not erase causes or evidence. Decide what can still change the next action before deciding how much to retain.

Each `thenMap` value decides one visible live anchor. A non-blank summary string replaces that anchor's current interval. `null` keeps the interval as written. A key that is absent is not carried into the next request, nor passed to a child or back to the caller. Entries below `prefixUntil` are ignored. `{ "5": null, "6": "<summary>" }` keeps 5, replaces 6, and drops every unlisted anchor. A blank summary is refused.

Compression is permanent: neither `null` nor `prefixUntil` restores original text.

Implicit anchor `0` always points to the beginning and always exists. It can be replaced but never disappears; real anchors start at `1`. After replacing 1, anchor 0 represents its content; a later replacement of 0 replaces that entire interval. Anchor 0 obeys prefix protection too; use `prefixUntil: 0` to replace it.

The programming surface, advisor and handoff share this view semantics. The advisor generates the complete view first, then takes the delta against host-owned delivery facts. Its first consult receives the whole view. `prefixUntil` does not say what the advisor has already seen.
