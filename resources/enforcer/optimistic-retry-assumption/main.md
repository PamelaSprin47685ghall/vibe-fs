# optimistic-retry-assumption — Main

Give `UnknownOutcome` first-class status.

Do not let timeout/disconnect/crash paths fall through the same branch as a known pre-effect failure. Separate them in the type/control flow and require a recovery protocol before another externally visible effect may be issued.

The recovery order should be:

1. preserve the original logical operation identity;
2. ask an authoritative source whether that identity committed;
3. if committed, recover/use that outcome;
4. if known not committed, retry if policy allows;
5. if still unknown, either retry under a protocol that guarantees same-intent idempotency or keep the outcome unknown and escalate/reconcile later.

Possible resolving mechanisms include provider idempotency lookup, transaction status, business-key query, durable command inbox, externally observable state, or a domain-specific reconciliation operation.

If the provider gives no way to identify or query an attempt and duplicate effect is unacceptable, automatic retry is not safe. That limitation belongs in the product/operation contract, not behind “best effort” optimism.

Common fake repairs:

- exponential backoff with a fresh request identity;
- retrying only once and calling duplication unlikely;
- assuming a timeout shorter than the provider's normal latency means the request never reached it;
- checking only local state after restart while the uncertain effect happened remotely;
- generating a compensation for the first attempt and also retrying, when the system does not know whether there is anything to compensate;
- marking the command failed in local DB before reconciling remote state;
- catching `Cancelled`/`Timeout` and mapping both to `NotExecuted`;
- querying a stale cache and treating absence there as proof of remote non-commit.

Verification must make uncertainty real. Build a fault where the remote effect commits but the acknowledgement is lost. Recovery must not create a second logical effect. Then test known pre-effect rejection separately and prove that it can retry without unnecessary reconciliation.

Also test the unresolved case: status lookup fails or provider is unavailable. The system should remain in an honest unknown state rather than inventing success or failure merely to make the state machine terminal.

This matters for operator UX too. “Failed” invites users to repeat an action. If the real state is “we do not yet know whether your payment/order/publication committed,” say that. A false failure label can itself trigger the duplicate effect the backend was trying to avoid.

You are done when every retry decision can cite evidence for one of two claims:

- the previous effect definitely did not commit; or
- repeating under the same logical identity cannot create another business effect.

If neither claim can be established, do not manufacture certainty.

> Recovery after uncertainty is an epistemology problem before it is a retry-policy problem.
