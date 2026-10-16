# retry-not-idempotent — Lesson

## Opinion

### Definition

A retry becomes dangerous when several physical attempts are allowed to represent one logical intention, but the effect boundary has no stable way to recognize that they are the same intention.

The defect is not "POST is bad" or "retries are bad." The defect is **identity missing across repetition**.

Networks can lose acknowledgements. Processes can crash after effect and before reply. Clients can time out while the server is still committing. Once any of those are possible, a retry loop is forced to answer a business question:

> Is this another attempt at the same operation, or a brand-new operation?

If the receiver cannot tell, transport uncertainty leaks into business history as duplicate charges, duplicate publications, duplicate prompts, duplicate resource creation, duplicate journal facts, or repeated external side effects.

### Governing Principle

A robust design allocates logical identity **before the first effect**, propagates the same identity through every retry, and deduplicates at the boundary that actually owns the side effect. Client-only dedupe is insufficient if two requests can already reach the remote system.

### Trigger When

Fire this rule when:

- an effectful call is automatically retried after timeout/connection loss/5xx and repeating it can create another durable effect;
- each retry generates a fresh request ID even though the business intention is unchanged;
- deduplication happens only in logs/metrics after duplicate effects already escaped;
- client code assumes "the first attempt probably failed" because no response arrived;
- a workflow replays a command after crash but the receiver cannot recognize it as the same logical command;
- the API has natural business identity available, but retry code does not carry it through.

### Do Not Trigger When

Do not fire merely because an operation is retried. Pure reads, truly idempotent PUT/set-by-key operations, monotonic set membership, or APIs with a stable idempotency key may be perfectly safe.

Also distinguish **idempotency of intent** from equality of response bytes. A replay may legitimately return a cached/original result, a newer representation of the same committed object, or an "already applied" acknowledgement. The key property is that the business history contains one logical effect.

### Distinguish From

Nearby rules:

- `optimistic-retry-assumption` — the previous attempt's outcome is unknown, but code assumes failure and proceeds;
- `partial-write-assumption` — interrupted effect is assumed all-or-nothing without proof;
- `lost-update` — different intents collide through stale replacement;
- `repeat-until-pass` — verification samples attempts until green rather than retrying an effect safely.

### Decision Procedure

Use this rule when the structural problem is: **same intent can be executed twice because physical retry has no stable identity at the effect owner.**

The decisive thought experiment is simple: take one logical request identity and deliver it twice, including a case where the first effect committed but its acknowledgement was lost. What business history remains?

If the answer is "two effects unless we're lucky," the operation is not retry-safe.

### Examples

A workflow replays a command after crash but the receiver cannot recognize it as the same logical command. Each retry generates a fresh request ID even though the business intention is unchanged.

## Advice

### What To Do Now

Make logical identity survive transport repetition.

Allocate an idempotency identity before the first side effect and reuse it on every retry of the same intention. The receiver — or the closest boundary that truly owns the side effect — must use that identity to collapse duplicate attempts into one effect.

The invariant is:

> **Many physical attempts for one logical intention produce at most one business effect.**

That usually requires more than a client-side retry wrapper. If the remote system can receive both attempts, deduplication must exist where the effect is committed or at a protocol layer that can prevent duplicate commitment.

### Why This Matters

Good patterns include:

- provider-supported idempotency keys persisted before first attempt;
- natural business keys with create-if-absent semantics;
- command IDs recorded atomically with the committed effect;
- inbox/dedup tables where `command_id` and effect commit share one transaction boundary;
- replay returning the original committed outcome or an explicit already-applied result.

When the provider cannot support idempotency and the first attempt has uncertain outcome, the honest repair may be **do not retry automatically**. Surface `UnknownOutcome`, reconcile by reading authoritative state, and require a new explicit intention before creating another effect.

Do not invent a new request ID on every attempt. That tells the receiver each retry is a new business action — exactly the opposite of what retry means.

### Repair Strategy

If a dedupe record and effect are stored separately, inject failure between them. If that produces either duplicate effect or permanent false suppression, the atomicity boundary is wrong.

### Decision Branches

Also test concurrent duplicate delivery of K, restart between effect and replay, and a truly new request K2 with identical payload. K2 must remain capable of creating a second effect; dedupe must collapse identity, not merely similar content.

### Common Wrong Fixes

Common fake repairs:

- "duplicates are rare";
- retry only once;
- dedupe by a short time window or payload similarity;
- search logs after the fact and remove duplicate records while an external charge/email/publication already escaped;
- assign the idempotency key inside the retry loop;
- rely on process-local memory for dedupe when restart can replay the command;
- record the dedupe key after performing the effect, leaving a crash window between effect and identity record;
- treat HTTP method names as proof (`PUT` can still be non-idempotent if the server gives it additive semantics).

### Verification

Verification must include acknowledgement loss, because that is the case naive retry logic is designed to mishandle:

1. deliver request K;
2. commit the side effect;
3. suppress/drop the response;
4. retry K;
5. assert exactly one logical effect exists and the second attempt resolves consistently.

### Done When

You are done when retry policy can be aggressive within its transport budget without altering business multiplicity. The system may see one attempt or ten; the domain still sees one intention.

> Idempotency is not "running twice usually looks okay." It is a protocol-level proof that repetition preserves business meaning.

### Nudge

> Retries duplicate transport. A correct protocol must collapse those attempts back into one business intention.
