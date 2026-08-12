# optimistic-retry-assumption — Enforcer

An optimistic retry assumption appears when lack of acknowledgement is silently reinterpreted as proof that the effect never happened.

The key epistemic error is:

```text
I do not know whether the remote side committed
            ↓
therefore it probably did not commit
            ↓
try again as a new effect
```

That middle step is invented knowledge.

Timeout, disconnect, process death, lost response, or tool interruption often destroy **our observation**, not the remote history. The provider may have committed one microsecond before the response vanished. Once that is possible, the system has at least three states:

```text
known success
known failure-before-effect
unknown outcome
```

Collapsing `unknown` into failure creates duplicate-history risk.

Fire this rule when:

- a payment/publication/create/send/write operation is repeated after timeout with no proof the original failed before effect;
- an interrupted tool/process is assumed not to have mutated the world because the caller never saw completion;
- a retry uses a fresh logical identity after an acknowledgement-loss path;
- external work is reissued because local state says “not completed,” even though remote state was never reconciled;
- a crash/restart path replays commands solely because their success acknowledgement was absent;
- code comments say “safe to retry on timeout” without naming the idempotency/reconciliation protocol that makes it safe.

Do not fire when failure is genuinely known to precede the effect: validation rejected locally, connection establishment failed before any request bytes could be accepted under the protocol, transaction explicitly aborted, provider returned a typed rejection known to be pre-effect. Also do not fire for read-only/naturally idempotent operations or operations protected by a stable deduplication identity.

This rule is adjacent to, but not the same as, `retry-not-idempotent`.

- `optimistic-retry-assumption` diagnoses the **knowledge error**: unknown was treated as failed.
- `retry-not-idempotent` diagnoses the **operation property**: repeated attempts of one logical intent can create multiple effects.

They often co-occur. A timeout may create unknown; a non-idempotent retry then turns that unknown into duplicate effect.

`partial-write-assumption` goes the opposite direction: it invents an internal partial state not exposed by the boundary. This rule invents certainty that an uncertain external attempt failed.

The decisive scenario is acknowledgement loss:

1. remote effect commits;
2. response is dropped;
3. caller times out;
4. recovery executes.

Ask what recovery knows at step 4. If its only evidence is “we did not receive success,” it knows **nothing about whether the remote effect committed**.

A correct protocol either resolves the original identity (status lookup/reconciliation), safely retries under the same idempotency identity, or preserves `UnknownOutcome` and refuses to create another effect automatically.

Backoff does not solve this. Waiting longer changes load and retry timing; it does not change whether the first effect already happened.

> Silence is not a negative acknowledgement. Unknown is a real state and deserves to remain one until evidence resolves it.
