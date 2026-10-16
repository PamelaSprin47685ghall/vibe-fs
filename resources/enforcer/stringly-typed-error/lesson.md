# stringly-typed-error — Lesson

## Opinion

### Definition

An error is stringly typed when machine behavior depends on the wording of a human-readable message.

The root cause is **identity encoded as prose**: retry, authorization, recovery, routing, or status decisions are made by substring, regex, exact text, punctuation, localization, or exception message instead of by a stable typed case/code owned by the producer.

### Governing Principle

Humans need explanations. Machines need identity.

Those surfaces have opposite evolution pressures. Human text should become clearer, more contextual, and localizable. Machine identity should remain closed, explicit, and stable. When the same string is forced to do both jobs, editorial improvement becomes a breaking protocol change.

`message.includes("timeout")` is not error handling. It is an undocumented parser for prose nobody promised to preserve.

The damage is deeper than brittleness. A string classifier usually cannot express the full semantic distinction: transient timeout vs caller deadline vs downstream cancellation vs a sentence that merely mentions a timeout. The code begins making control decisions from vocabulary coincidence rather than from the event that actually occurred.

### Trigger When

Trigger when:

- control flow matches `error.message`, rendered TOML/prose, log text, stderr, or provider wording;
- localized text could change retry/routing behavior;
- tests freeze exact human messages because callers secretly depend on them;
- several modules maintain regex/substrings for the same family of errors;
- a transport/provider error is passed inward as text and reclassified repeatedly;
- changing punctuation or wording can change whether recovery happens.

### Do Not Trigger When

- The machine first matches a typed error case and only then renders prose for a human.
- An adapter must inspect an external provider's textual-only error because no structured identity exists; it does so once, explicitly, maps to a typed internal case, and preserves the raw text as evidence.
- A test checks human-facing copy as a product requirement, while control semantics remain independent of that wording.
- Logs contain error text but no later machine branch parses those logs.

### Distinguish From

`weak-boundary-parsing` concerns loose external payload shape in general. `expected-failure-as-exception` concerns the wrong control channel for an expected outcome. `type-erosion-at-boundary` concerns dynamic representation leaking inward.

Tie-break: if human prose itself is being treated as semantic identity, use this rule. If the failure is already a stable exception/type but should be returned as an ordinary result, use `expected-failure-as-exception`.

### Decision Procedure

For every branch that inspects text, write down the semantic distinction it is trying to recognize. Ask which producer first knows that distinction without reading prose. Give that producer a typed case/code and map external text once at the boundary if necessary.

Then change the message wording in a test. If control flow changes while the error identity did not, the protocol is still stringly typed.

### Examples

- positive: retry code executes when `e.message.toLowerCase().includes("timeout")`.
- positive: a plugin parses `"permission denied"` from rendered tool output to decide whether to escalate authority.
- near-miss: a provider offers no structured code; one adapter classifies its documented phrases into `RateLimited | AuthFailed | Unknown raw`, and no inward code sees those phrases.
- counterexample: `Timeout { deadline; operation }` drives recovery while a formatter renders different EN/zh-CN messages.

## Advice

### What To Do Now

Replace message parsing with a closed error identity owned by the producer or protocol adapter. Branch internally on that identity; render human-readable text only after the semantic case is known.

If the upstream provider exposes only prose, classify it **once at the external boundary**, preserve the raw message as evidence, and return a typed internal case such as `RateLimited | Unauthorized | Timeout | Unknown raw`.

### Why This Matters

Human wording is supposed to change. Better diagnostics add context. Localization changes every sentence. Providers revise phrasing. Punctuation moves. None of those edits should silently alter retry, authorization, fallback, or recovery policy.

Stringly typed errors turn copyediting into control-plane mutation.

They also invite false positives: a message can mention “timeout” while describing something that was *not* a timeout. A regex cannot recover semantic identity the producer failed to expose; at best it is a boundary heuristic that must be contained and named as such.

### Repair Strategy

1. Locate every machine branch over error prose.
2. Name the semantic distinctions those branches actually need.
3. Introduce a closed error type/code at the earliest owner that knows the distinction.
4. Translate external textual-only errors once in the adapter; include an `Unknown` case rather than pretending the classifier is omniscient.
5. Move formatting/localization outward.
6. Rewrite tests to assert typed identity for control behavior and prose separately only where copy itself is contractual.

### Decision Branches

- If you control the producer, emit structured identity directly.
- If you do not control a text-only upstream, isolate the unavoidable textual classifier in one adapter and make uncertainty explicit.
- If the outcome is expected ordinary control flow, also consider `expected-failure-as-exception` after identity is fixed.
- If the string is purely diagnostic and nobody parses it, leave it alone.

### Common Wrong Fixes

- Centralize regexes into `ErrorUtils` and call the problem solved.
- Freeze current English text forever as a de facto protocol.
- Add more substring alternatives for every provider revision.
- Convert every unknown message to a confident typed case; false certainty is worse than an explicit `Unknown raw`.
- Keep exact-message assertions in control-flow tests after typed identity exists.

### Verification

Change punctuation, add context, and switch EN/zh-CN rendering while holding the typed case constant. Machine behavior must remain identical.

Then change the typed case while keeping similar prose: control behavior must follow the case, not keyword coincidence.

Invariant: **control semantics are independent of human wording.**

### Done When

No internal machine decision depends on rendered error prose; unavoidable external text classification has one explicit boundary owner; messages can improve or localize without changing recovery, authorization, or routing.

### Nudge

If changing a sentence can change control flow, the sentence has accidentally become an API.

Give machines a case. Give humans prose. Do not make one impersonate the other.
