# behavioral-boundary-untested — Enforcer

A behavioral boundary is untested when the suite proves the pieces behind a supported entry point but never proves the behavior **through the entry point callers actually use**.

The trap is seductive because helper tests often look stronger: they are fast, precise, easy to arrange, and produce excellent coverage. But a public behavior is more than the sum of its helpers. The boundary contains wiring, defaults, identity, authorization, normalization, serialization, effect ordering, error mapping, and dependency composition — exactly the places where individually correct pieces can become collectively wrong.

Think of internal tests as lemmas and the supported boundary as the theorem. Ten correct lemmas do not prove a theorem whose composition is wrong.

Fire this rule when:

- tests call private/internal helpers directly while the real public method/route/tool/hook remains unexercised;
- fixtures mutate internal state into the desired setup instead of entering through supported behavior;
- a test-only export bypasses the production adapter, decoder, permission gate, or workflow owner;
- helper coverage is cited as evidence that public defaults/wiring work;
- public identity or failure semantics changed but tests still stop one layer below them;
- integration wiring can be broken while every unit test stays green.

Do not fire when the public behavior already has a strong boundary test and helper tests merely localize failures. Do not demand a huge end-to-end environment for every pure helper change. The question is whether the **caller-visible promise has at least one failing-capable proof at its owning entrance**.

This differs from `contract-test-missing`: that rule is for a boundary between independent systems/runtimes whose agreement can drift. This rule also applies inside one product whenever callers depend on a supported behavioral surface. `test-implementation-coupled` is the opposite failure: tests cross too far inward and freeze private choreography.

The decisive mutation is simple. Keep every helper correct, but break the boundary wiring: wrong default, swapped field, missing permission, wrong serializer, forgotten adapter, stale route, different ID. Would any test turn red? If not, the public theorem is unproved.

A good boundary test does not need to be broad. It should be the **narrowest test that still enters where the real caller enters** and observes what the caller is entitled to rely on.

> Prove behavior at the place where the promise becomes real, not only at the places where implementation happens to be convenient to test.