# stringly-typed-error — Enforcer

## Definition
An error is stringly typed when program behavior depends on parsing, matching, or recognizing human-readable error prose rather than a stable closed error value. The root-cause is that human-readable wording is used as machine identity, so control flow is coupled to editorial phrasing that was never a protocol.

## Governing Principle
Presentation text and control information have different audiences and different stability requirements. Prose evolves for clarity, localization, and diagnostics; control values must remain unambiguous under those changes. Parsing text couples machine semantics to editorial wording, turning punctuation and phrasing into undocumented protocol fields.

## Trigger When
Trigger when callers branch on error substrings, regexes, exact messages, localization text, or exception prose to decide retry, status, authorization, or recovery behavior.

## Do Not Trigger When
- Strings are produced only after the caller has already matched a typed error code/case and are used solely for human display or diagnostics.
- Tests assert a stable typed error identity and treat the message as non-contractual prose.
- Logs record already-classified errors without later control flow matching on wording.
- Provider text is captured as diagnostic payload beside a mapped typed case, not as the branch key.

## Distinguish From
`weak-boundary-parsing` leaves general input shape untyped. `expected-failure-as-exception` chooses the wrong failure channel. Tie-break: if the defect is treating human error prose as machine identity, use this rule; if the defect is delayed parsing of external payload shape, use `weak-boundary-parsing`.

## Decision Procedure
List the program decisions derived from the message. Define one closed error case/code for each semantic distinction and format prose only after control flow has matched the case.

## Examples
- positive: retry logic matches `e.message.includes("timeout")` to decide whether to retry.
- near-miss: after matching `TimeoutError`, a formatter renders localized prose for logs and UI.
- counterexample: an adapter still passing loosely typed JSON bags inward is `weak-boundary-parsing`, not stringly-typed-error.

## Nudge
Machines need identities; humans need explanations. Branch on a typed error value and generate prose afterward—never make wording itself the protocol.
