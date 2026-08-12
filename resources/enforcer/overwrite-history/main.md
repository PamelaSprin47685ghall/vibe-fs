# overwrite-history — Main

Stop correcting the past by replacing it.

Preserve the original committed fact and append a new fact that states what changed: correction, compensation, revocation, supersession, reclassification, reversal, redaction marker, or another domain-specific transition.

The goal is to make two questions answerable at the same time:

- **What was recorded/believed/done at time T?**
- **What is the current interpretation now?**

If one answer requires destroying the other, the history model is too weak.

For event/journal systems, fold current state from original facts plus correcting facts. A correction event should normally identify what it corrects and carry enough reason/provenance to explain the transition without modifying the earlier event.

For ledgers/accounting, prefer compensating entries over rewriting balances that were already posted. For audit records, append who/what/why changed the interpretation. For migrations, distinguish “repair malformed storage representation without changing semantic history” from “rewrite what the system claims happened.” The latter requires explicit semantic migration policy, not a silent SQL update.

Common fake repairs:

- update old event rows in place and preserve the same event ID;
- delete the original and insert a replacement so replay sees only the corrected story;
- run a migration that normalizes historical values without recording which semantic facts changed;
- copy current truth backward into every old snapshot/record to “make reports consistent”;
- mutate history because current-state queries are inconvenient; fix the query/projection instead;
- keep an audit log of the overwrite while the authoritative event itself is still rewritten — the audit log cannot restore causal replay if the source fact changed;
- use “soft delete” flags on historical events without modeling what deletion means to replay.

Verification should prove temporal fidelity. Replay/query history just before the correction: it must still expose the old recorded fact. Replay after the correction: current interpretation must reflect the new fact. Both views must be derivable without lying about what happened at either point.

Also test downstream causality. If earlier effects were triggered by the old fact, they should remain explainable. A correction may require compensation, but it must not make those effects appear causeless.

For redaction, test the policy separately: sensitive content becomes unavailable as required, while permitted metadata still demonstrates that a redaction occurred and replay has a deterministic rule for the redacted fact.

You are done when the system can tell a truthful story of change:

```text
we believed/recorded X
then evidence/authority Y arrived
therefore X was corrected by Z
current interpretation is W
```

not:

```text
we have always believed W
```

when that second sentence is false.

> Auditability is not keeping old rows forever. It is preserving the causal difference between “was true to the system then” and “is believed now.”
