# contract-test-missing — Main

Add a test at the narrowest point where the independent sides actually meet.

Name the agreement first:

- producer;
- consumer;
- representation crossing the boundary;
- stable identity rules;
- allowed versions/alternatives;
- ordering/lifetime guarantees;
- failure/unknown semantics;
- capabilities/permissions if the boundary carries them.

Then exercise the real boundary machinery on both sides wherever possible. Real serializer into real parser is stronger than two fixtures written from the same English description. Real generated Fable export into the JS facade is stronger than asserting the F# source contains a type with the right name.

Keep the test focused. It should not require a full production deployment if one adapter-level interaction exposes the agreement. But do not mock away the exact transformation the test is supposed to prove.

Common fake repairs:

- unit-test producer encoder and consumer decoder separately with independently invented fixtures;
- copy a captured payload into a golden snapshot without stating which fields are contractual;
- assert only “serialization succeeds” or “parser returns something”;
- mock the external side using the same incorrect schema object production uses;
- pin incidental key order/whitespace when the protocol does not care;
- ignore error/failure cases and test only happy representation;
- test only the newest version while compatibility claims include old versions;
- run a huge E2E whose failure cannot distinguish contract drift from unrelated infrastructure.

A useful contract test often contains both positive and negative evidence:

```text
supported producer output → consumer accepts and preserves semantics
plausible incompatible output → consumer rejects or maps according to contract
```

For versioned protocols, prove the migration matrix you actually support. For identity-bearing protocols, prove IDs/cursors/idempotency keys survive round trips instead of being regenerated. For capability surfaces, prove advertised capability and execution gate agree.

Verification should mutation-test the agreement. Make one realistic incompatible change on either side — rename a field, change a tag, alter default/error mapping, reorder a causally significant frame, regenerate identity — and confirm the contract test turns red before the other side is changed to match.

If the boundary depends on behavior of a real external service that a local double cannot truthfully reproduce, keep a narrow local contract test for what you own and add/retain a canary against the real environment for the undocumented/unstable piece. Do not make a local mock an oracle for another company's runtime.

You are done when either side can evolve internally, but any drift in the agreement the other side actually consumes becomes an immediate, localizable failure.

> A contract test is executable diplomacy between independent truths.