# stringly-typed-error — Main

## What To Do Now
Replace message parsing with a closed error identity owned by the producer or protocol adapter. Branch internally on that identity; render human-readable text only after the semantic case is known.

If the upstream provider exposes only prose, classify it **once at the external boundary**, preserve the raw message as evidence, and return a typed internal case such as `RateLimited | Unauthorized | Timeout | Unknown raw`.

## Why This Matters
Human wording is supposed to change. Better diagnostics add context. Localization changes every sentence. Providers revise phrasing. Punctuation moves. None of those edits should silently alter retry, authorization, fallback, or recovery policy.

Stringly typed errors turn copyediting into control-plane mutation.

They also invite false positives: a message can mention “timeout” while describing something that was *not* a timeout. A regex cannot recover semantic identity the producer failed to expose; at best it is a boundary heuristic that must be contained and named as such.

## Repair Strategy
1. Locate every machine branch over error prose.
2. Name the semantic distinctions those branches actually need.
3. Introduce a closed error type/code at the earliest owner that knows the distinction.
4. Translate external textual-only errors once in the adapter; include an `Unknown` case rather than pretending the classifier is omniscient.
5. Move formatting/localization outward.
6. Rewrite tests to assert typed identity for control behavior and prose separately only where copy itself is contractual.

## Decision Branches
- If you control the producer, emit structured identity directly.
- If you do not control a text-only upstream, isolate the unavoidable textual classifier in one adapter and make uncertainty explicit.
- If the outcome is expected ordinary control flow, also consider `expected-failure-as-exception` after identity is fixed.
- If the string is purely diagnostic and nobody parses it, leave it alone.

## Common Wrong Fixes
- Centralize regexes into `ErrorUtils` and call the problem solved.
- Freeze current English text forever as a de facto protocol.
- Add more substring alternatives for every provider revision.
- Convert every unknown message to a confident typed case; false certainty is worse than an explicit `Unknown raw`.
- Keep exact-message assertions in control-flow tests after typed identity exists.

## Verification
Change punctuation, add context, and switch EN/zh-CN rendering while holding the typed case constant. Machine behavior must remain identical.

Then change the typed case while keeping similar prose: control behavior must follow the case, not keyword coincidence.

Invariant: **control semantics are independent of human wording.**

## Done When
No internal machine decision depends on rendered error prose; unavoidable external text classification has one explicit boundary owner; messages can improve or localize without changing recovery, authorization, or routing.