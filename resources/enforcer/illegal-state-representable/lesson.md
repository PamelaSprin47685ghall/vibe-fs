# illegal-state-representable — Lesson

## Opinion

### Definition

An illegal state is representable when the program can construct a value that has no legitimate meaning in the domain, then relies on later guards to pretend that value never existed.

The root cause is a mismatch between **representation space** and **valid state space**. A product of flags, nullable fields, stage markers, or loosely related properties admits more combinations than reality does, so every consumer inherits a proof obligation the constructor refused to discharge.

### Governing Principle

A type is a claim about what may exist.

If `Paid=true` can coexist with `Receipt=None`, or `status="completed"` can coexist with an unfinished payload, the type has not merely stored data flexibly. It has invented a world the business says is impossible.

That invented world has a carrying cost. Every reader adds `if` guards. Tests multiply around contradictory combinations. Serialization must decide what to do with them. Recovery code starts treating impossible values as ordinary input because the type itself says they are allowed.

The strongest repair is not “validate more often.” It is to move the proof to construction and make downstream code entitled to trust what it receives.

### Trigger When

Trigger when one or more of these are true:

- callers repeatedly state invariants such as “if A then B must be present”;
- several flags encode one lifecycle and some truth-table rows have no domain meaning;
- nullable fields exist only because different lifecycle states need different data;
- a record has a `validate()` method that every consumer is expected to remember to call;
- recovery or persistence can deserialize combinations that policy code immediately declares impossible;
- the same contradiction is guarded against in multiple modules.

### Do Not Trigger When

- A transport DTO intentionally represents untrusted external bytes and is converted through one fail-closed constructor before entering the domain.
- Every representable combination genuinely has a meaning, even if some are rare.
- A dynamic business rule cannot be encoded statically and one atomic constructor returns a typed rejection without leaking an invalid instance.
- Temporary builder state is private, cannot escape, and is not itself presented as the domain value.

### Distinguish From

`boolean-blindness` is the specific case where boolean representation erases named alternatives. `null-ambiguity` loses the reason for absence. `runtime-checked-builder` concerns invalid **construction stages**. This rule owns the broader defect: a completed value itself can express a world the domain forbids.

Tie-break: if the contradiction survives into the domain value, classify here. If only the in-progress builder can be incomplete, use `runtime-checked-builder`. If the main loss is true/false vocabulary, use `boolean-blindness`.

### Decision Procedure

Write the legitimate states without looking at the current fields. Then enumerate the combinations the current representation permits. The difference between those sets is the defect.

Ask where the invariant is first fully knowable. That boundary owns the proof. Encode valid alternatives there as a sum type, state-specific records, or a constructor that cannot return an invalid value.

### Examples

- positive: `{ isPaid: bool; receiptId: string option }` permits paid-without-receipt and unpaid-with-receipt, while every caller rejects both.
- positive: `{ status: "open"|"done"; completedAt?: Instant; failure?: Error }` admits `open + completedAt + failure` even though no such lifecycle state exists.
- near-miss: an HTTP DTO has optional fields because malformed input must be representable long enough to reject it; `Order.parse` converts it into closed domain cases before policy code sees it.
- counterexample: `PaymentState = Unpaid | Paid of ReceiptId` makes the receipt exist exactly where the state requires it.

## Advice

### What To Do Now

Model the legitimate states directly and move the invariant to the constructor that first has enough information to prove it. Replace products of unrelated flags/nullable fields with explicit cases or state-specific records so invalid combinations cannot escape.

The owner is the **construction boundary** that knows which combinations are real. Downstream guards are consumers of the invariant, not its owner.

### Why This Matters

Every impossible value admitted by a type becomes recurring work. Readers branch defensively. Serializers invent fallback behavior. Tests cover contradictions nobody wants. Recovery learns to carry garbage farther because “the type allowed it.”

This is not robustness. It is a proof obligation multiplied across the codebase.

A truthful type shrinks the reasoning surface. Once `Paid of ReceiptId` exists, every downstream function can reason from that fact without asking whether the receipt is mysteriously missing. One proof at ingress replaces a forest of repeated checks.

### Repair Strategy

1. List valid states in domain language, independent of the current fields.
2. Identify the data that belongs only to each state.
3. Replace the Cartesian product with a closed sum, state-specific type, or validated constructor.
4. Move transition logic to functions that produce only legal successor states.
5. Keep untrusted DTOs outside the domain; parse them once and fail closed.
6. Delete defensive branches that became unreachable after the stronger construction boundary exists.

### Decision Branches

- If the state space is finite and named, use explicit cases and attach state-specific data to those cases.
- If validity depends on runtime facts, use one atomic constructor returning `Result`; do not leak the rejected representation as a domain value.
- If the loose shape is required for wire compatibility, keep it in the adapter and translate before policy code.
- If every combination is actually meaningful, do nothing. A large state space is not automatically an illegal one.

### Common Wrong Fixes

- Add `validate()` and rely on every caller to remember it.
- Scatter `assert` statements around consumers while leaving construction unchanged.
- Hide the same illegal record behind a helper or facade.
- Add another status flag to explain combinations created by the previous flags.
- Create a “strong” wrapper that still exposes a public constructor for contradictory fields.

### Verification

Attempt to construct each formerly illegal combination through every public construction path, including deserialization/recovery. It must be impossible by type or rejected at the single ingress boundary before a domain value exists.

Then remove a downstream defensive check in a test fixture: the type/constructor should already make the bad case unreachable. The invariant is **representable domain state = legitimate domain state**.

### Done When

Impossible combinations cannot enter domain/application logic, transitions preserve that property, and downstream code no longer spends branches re-proving construction coherence.

### Nudge

Do not make every reader prove that its input came from reality. Make construction prove it once.

A guard against an impossible state is often evidence that the type should stop being able to create that state.
