# contract-test-missing — Enforcer

A contract test is missing when two independently implemented sides can each be locally correct and still disagree at the exact place where they meet.

This is the essential boundary problem:

```text
producer believes it emitted X
consumer believes it accepts Y
X ≠ Y
```

Unit tests on both sides can stay perfectly green because each side is proving its own assumptions. The missing proof is the **intersection**: bytes, framing, identity, ordering, defaults, lifecycle, failure semantics, versioning, and capability rules that actually cross the boundary.

Fire this rule when a change touches boundaries such as:

- plugin ↔ Host hook objects;
- F# ↔ generated JS/Fable representation;
- process ↔ stdout/stdin framing;
- client ↔ provider HTTP/tool schema;
- application ↔ database/store transaction semantics;
- service ↔ queue/message contract;
- package ↔ consumer import/export surface;
- CLI ↔ subprocess exit/status/output protocol;
- network protocol ↔ adapter decoder/encoder.

A useful trigger is independence. If producer and consumer can change separately, use different languages/runtimes, or are owned by different release cycles, the risk that both sides encode different “obvious” assumptions rises sharply.

Do not demand a new contract test for every internal refactor. If the observable agreement is unchanged and an existing boundary test already fails on incompatible representation/identity/failure behavior, add no theater. Contract tests are not a ritual tax on every adapter edit.

Also do not freeze every incidental byte. Exact wire details should be asserted **only where they are contractual**. Otherwise test semantic properties: required field presence, stable identity, accepted alternatives, ordering guarantees, failure category, idempotency key reuse, capability projection, etc. Over-specifying private serialization accidents produces `test-implementation-coupled` at system scale.

Distinguish from `behavioral-boundary-untested`: that rule can apply within one product at a supported public entrance. This rule is specifically about an agreement where independent sides can drift. `canary-skipped` applies when the external side's behavior cannot be faithfully represented locally and the real environment is needed.

The decisive test design question is:

> What is the smallest execution in which **both sides' assumptions are simultaneously present**?

Use the real encoder/parser/adapter where practical. Avoid hand-building a fixture from the same spec interpretation as the production side, because the fixture can share the same mistake.

A strong contract test should fail under realistic incompatibilities:

- renamed/missing field;
- wrong union/case/tag representation;
- changed default;
- wrong status/error mapping;
- identity regenerated instead of preserved;
- ordering changed;
- old/new version mixed;
- unsupported capability advertised;
- acknowledgement semantics changed.

> Two correct components can still be incompatible. Test the agreement, not merely the confidence of each side.