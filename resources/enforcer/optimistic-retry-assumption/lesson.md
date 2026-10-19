# optimistic-retry-assumption — Lesson

## Opinion

### Definition

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

### Governing Principle

Collapsing `unknown` into failure creates duplicate-history risk.

### Trigger When

Fire this rule when:

- a payment/publication/create/send/write operation is repeated after timeout with no proof the original failed before effect;
- an interrupted tool/process is assumed not to have mutated the world because the caller never saw completion;
- a retry uses a fresh logical identity after an acknowledgement-loss path;
- external work is reissued because local state says “not completed,” even though remote state was never reconciled;
- a crash/restart path replays commands solely because their success acknowledgement was absent;
- code comments say “safe to retry on timeout” without naming the idempotency/reconciliation protocol that makes it safe.

### Do Not Trigger When

Do not fire when failure is genuinely known to precede the effect: validation rejected locally, connection establishment failed before any request bytes could be accepted under the protocol, transaction explicitly aborted, provider returned a typed rejection known to be pre-effect. Also do not fire for read-only/naturally idempotent operations or operations protected by a stable deduplication identity.

### Distinguish From

This rule is adjacent to, but not the same as, `retry-not-idempotent`.

- `optimistic-retry-assumption` diagnoses the **knowledge error**: unknown was treated as failed.
- `retry-not-idempotent` diagnoses the **operation property**: repeated attempts of one logical intent can create multiple effects.

They often co-occur. A timeout may create unknown; a non-idempotent retry then turns that unknown into duplicate effect.

`partial-write-assumption` goes the opposite direction: it invents an internal partial state not exposed by the boundary. This rule invents certainty that an uncertain external attempt failed.

### Decision Procedure

The decisive scenario is acknowledgement loss:

1. remote effect commits;
2. response is dropped;
3. caller times out;
4. recovery executes.

Ask what recovery knows at step 4. If its only evidence is “we did not receive success,” it knows **nothing about whether the remote effect committed**.

A correct protocol either resolves the original identity (status lookup/reconciliation), safely retries under the same idempotency identity, or preserves `UnknownOutcome` and refuses to create another effect automatically.

Backoff does not solve this. Waiting longer changes load and retry timing; it does not change whether the first effect already happened.

### Examples


## Advice

### What To Do Now

Give `UnknownOutcome` first-class status.

Do not let timeout/disconnect/crash paths fall through the same branch as a known pre-effect failure. Separate them in the type/control flow and require a recovery protocol before another externally visible effect may be issued.

### Why This Matters

The recovery order should be:

1. preserve the original logical operation identity;
2. ask an authoritative source whether that identity committed;
3. if committed, recover/use that outcome;
4. if known not committed, retry if policy allows;
5. if still unknown, either retry under a protocol that guarantees same-intent idempotency or keep the outcome unknown and escalate/reconcile later.

### Repair Strategy

Possible resolving mechanisms include provider idempotency lookup, transaction status, business-key query, durable command inbox, externally observable state, or a domain-specific reconciliation operation.

If the provider gives no way to identify or query an attempt and duplicate effect is unacceptable, automatic retry is not safe. That limitation belongs in the product/operation contract, not behind “best effort” optimism.

### Decision Branches


### Common Wrong Fixes

Common fake repairs:

- exponential backoff with a fresh request identity;
- retrying only once and calling duplication unlikely;
- assuming a timeout shorter than the provider's normal latency means the request never reached it;
- checking only local state after restart while the uncertain effect happened remotely;
- generating a compensation for the first attempt and also retrying, when the system does not know whether there is anything to compensate;
- marking the command failed in local DB before reconciling remote state;
- catching `Cancelled`/`Timeout` and mapping both to `NotExecuted`;
- querying a stale cache and treating absence there as proof of remote non-commit.

### Verification

Verification must make uncertainty real. Build a fault where the remote effect commits but the acknowledgement is lost. Recovery must not create a second logical effect. Then test known pre-effect rejection separately and prove that it can retry without unnecessary reconciliation.

Also test the unresolved case: status lookup fails or provider is unavailable. The system should remain in an honest unknown state rather than inventing success or failure merely to make the state machine terminal.

This matters for operator UX too. “Failed” invites users to repeat an action. If the real state is “we do not yet know whether your payment/order/publication committed,” say that. A false failure label can itself trigger the duplicate effect the backend was trying to avoid.

### Done When

You are done when every retry decision can cite evidence for one of two claims:

- the previous effect definitely did not commit; or
- repeating under the same logical identity cannot create another business effect.

If neither claim can be established, do not manufacture certainty.

> Recovery after uncertainty is an epistemology problem before it is a retry-policy problem.

### Nudge

> Silence is not a negative acknowledgement. Unknown is a real state and deserves to remain one until evidence resolves it.
