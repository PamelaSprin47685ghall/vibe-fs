# retry-not-idempotent — Enforcer

A retry becomes dangerous when several physical attempts are allowed to represent one logical intention, but the effect boundary has no stable way to recognize that they are the same intention.

The defect is not “POST is bad” or “retries are bad.” The defect is **identity missing across repetition**.

Networks can lose acknowledgements. Processes can crash after effect and before reply. Clients can time out while the server is still committing. Once any of those are possible, a retry loop is forced to answer a business question:

> Is this another attempt at the same operation, or a brand-new operation?

If the receiver cannot tell, transport uncertainty leaks into business history as duplicate charges, duplicate publications, duplicate prompts, duplicate resource creation, duplicate journal facts, or repeated external side effects.

Fire this rule when:

- an effectful call is automatically retried after timeout/connection loss/5xx and repeating it can create another durable effect;
- each retry generates a fresh request ID even though the business intention is unchanged;
- deduplication happens only in logs/metrics after duplicate effects already escaped;
- client code assumes “the first attempt probably failed” because no response arrived;
- a workflow replays a command after crash but the receiver cannot recognize it as the same logical command;
- the API has natural business identity available, but retry code does not carry it through.

Do not fire merely because an operation is retried. Pure reads, truly idempotent PUT/set-by-key operations, monotonic set membership, or APIs with a stable idempotency key may be perfectly safe.

Also distinguish **idempotency of intent** from equality of response bytes. A replay may legitimately return a cached/original result, a newer representation of the same committed object, or an “already applied” acknowledgement. The key property is that the business history contains one logical effect.

Nearby rules:

- `optimistic-retry-assumption` — the previous attempt's outcome is unknown, but code assumes failure and proceeds;
- `partial-write-assumption` — interrupted effect is assumed all-or-nothing without proof;
- `lost-update` — different intents collide through stale replacement;
- `repeat-until-pass` — verification samples attempts until green rather than retrying an effect safely.

Use this rule when the structural problem is: **same intent can be executed twice because physical retry has no stable identity at the effect owner.**

The decisive thought experiment is simple: take one logical request identity and deliver it twice, including a case where the first effect committed but its acknowledgement was lost. What business history remains?

If the answer is “two effects unless we're lucky,” the operation is not retry-safe.

A robust design allocates logical identity **before the first effect**, propagates the same identity through every retry, and deduplicates at the boundary that actually owns the side effect. Client-only dedupe is insufficient if two requests can already reach the remote system.

> Retries duplicate transport. A correct protocol must collapse those attempts back into one business intention.
