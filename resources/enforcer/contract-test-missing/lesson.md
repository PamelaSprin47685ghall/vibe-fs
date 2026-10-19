# contract-test-missing — Lesson

## Opinion

### Definition

A contract test is missing when two independently implemented sides can each be locally correct and still disagree at the exact place where they meet.

This is the essential boundary problem:

```text
producer believes it emitted X
consumer believes it accepts Y
X ≠ Y
```

Unit tests on both sides can stay perfectly green because each side is proving its own assumptions. The missing proof is the **intersection**: bytes, framing, identity, ordering, defaults, lifecycle, failure semantics, versioning, and capability rules that actually cross the boundary.

### Governing Principle

A useful trigger is independence. If producer and consumer can change separately, use different languages/runtimes, or are owned by different release cycles, the risk that both sides encode different “obvious” assumptions rises sharply.

### Trigger When

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

### Do Not Trigger When

Do not demand a new contract test for every internal refactor. If the observable agreement is unchanged and an existing boundary test already fails on incompatible representation/identity/failure behavior, add no theater. Contract tests are not a ritual tax on every adapter edit.

Also do not freeze every incidental byte. Exact wire details should be asserted **only where they are contractual**. Otherwise test semantic properties: required field presence, stable identity, accepted alternatives, ordering guarantees, failure category, idempotency key reuse, capability projection, etc. Over-specifying private serialization accidents produces `test-implementation-coupled` at system scale.

### Distinguish From

Distinguish from `behavioral-boundary-untested`: that rule can apply within one product at a supported public entrance. This rule is specifically about an agreement where independent sides can drift. `canary-skipped` applies when the external side's behavior cannot be faithfully represented locally and the real environment is needed.

### Decision Procedure

The decisive test design question is:

> What is the smallest execution in which **both sides' assumptions are simultaneously present**?

Use the real encoder/parser/adapter where practical. Avoid hand-building a fixture from the same spec interpretation as the production side, because the fixture can share the same mistake.

### Examples

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

## Advice

### What To Do Now

Add a test at the narrowest point where the independent sides actually meet.

### Why This Matters

Name the agreement first:

- producer;
- consumer;
- representation crossing the boundary;
- stable identity rules;
- allowed versions/alternatives;
- ordering/lifetime guarantees;
- failure/unknown semantics;
- capabilities/permissions if the boundary carries them.

### Repair Strategy

Then exercise the real boundary machinery on both sides wherever possible. Real serializer into real parser is stronger than two fixtures written from the same English description. Real generated Fable export into the JS facade is stronger than asserting the F# source contains a type with the right name.

Keep the test focused. It should not require a full production deployment if one adapter-level interaction exposes the agreement. But do not mock away the exact transformation the test is supposed to prove.

A useful contract test often contains both positive and negative evidence:

```text
supported producer output → consumer accepts and preserves semantics
plausible incompatible output → consumer rejects or maps according to contract
```

For versioned protocols, prove the migration matrix you actually support. For identity-bearing protocols, prove IDs/cursors/idempotency keys survive round trips instead of being regenerated. For capability surfaces, prove advertised capability and execution gate agree.

### Decision Branches

If the boundary depends on behavior of a real external service that a local double cannot truthfully reproduce, keep a narrow local contract test for what you own and add/retain a canary against the real environment for the undocumented/unstable piece. Do not make a local mock an oracle for another company's runtime.

### Common Wrong Fixes

Common fake repairs:

- unit-test producer encoder and consumer decoder separately with independently invented fixtures;
- copy a captured payload into a golden snapshot without stating which fields are contractual;
- assert only “serialization succeeds” or “parser returns something”;
- mock the external side using the same incorrect schema object production uses;
- pin incidental key order/whitespace when the protocol does not care;
- ignore error/failure cases and test only happy representation;
- test only the newest version while compatibility claims include old versions;
- run a huge E2E whose failure cannot distinguish contract drift from unrelated infrastructure.

### Verification

Verification should mutation-test the agreement. Make one realistic incompatible change on either side — rename a field, change a tag, alter default/error mapping, reorder a causally significant frame, regenerate identity — and confirm the contract test turns red before the other side is changed to match.

### Done When

You are done when either side can evolve internally, but any drift in the agreement the other side actually consumes becomes an immediate, localizable failure.

> A contract test is executable diplomacy between independent truths.

### Nudge

> Two correct components can still be incompatible. Test the agreement, not merely the confidence of each side.
