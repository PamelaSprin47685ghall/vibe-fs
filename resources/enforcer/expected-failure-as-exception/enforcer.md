# expected-failure-as-exception — Enforcer

## Definition
An expected failure is misrepresented when a foreseeable domain outcome—unauthorized, not found, insufficient balance, invalid transition, conflict—is thrown as an exception instead of returned as part of the operation’s contract. The root-cause is that a named, foreseeable business refusal is thrown instead of returned, so the signature overclaims success and callers can skip a required domain branch.

## Governing Principle
A function’s type should describe the worlds its caller must be prepared to inhabit. Foreseeable refusal is one of those worlds. Hiding it in an exception channel makes the signature overclaim success and lets callers accidentally ignore a required branch. Typed failure restores honesty: it turns policy from an ambient runtime surprise into an explicit obligation.

## Trigger When
Trigger when ordinary business rejection is thrown/caught as an exception or mapped to a generic exceptional channel.

## Do Not Trigger When
- The failure is infrastructure or programmer error that makes the requested operation impossible to reason about as an ordinary domain outcome.
- The throw is a broken invariant (illegal state the type should have made unrepresentable), not a named product refusal.
- An outer adapter maps already-typed domain refusals onto HTTP/UI codes without reintroducing exception policy in the core.
- A library boundary that cannot change still throws, and the first owned adapter immediately translates to a typed result.

## Distinguish From
`exception-driven-control-flow` covers ordinary branching generally. `null-ambiguity` hides several outcomes in absence. This rule specifically concerns expected business refusal. Tie-break: if the product can name the outcome before running (`Unauthorized`, `InsufficientBalance`), this rule owns the case.

## Decision Procedure
Ask whether the product can name the outcome before running the code and whether a caller has a legitimate response to it. If yes, give it a named result case.

## Examples
- positive: `withdraw` throws `InsufficientFundsException` that callers must catch to show a business message.
- near-miss: a disk full or panic-level invariant break throws because the operation cannot be a domain choice.
- counterexample: return a closed typed result that includes the named refusal beside success.

## Nudge
Foreseeable refusal belongs in the contract. Return a closed typed outcome so every caller must confront the business branch explicitly.
