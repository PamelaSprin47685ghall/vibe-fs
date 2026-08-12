# framework-tax — Main

## What To Do Now
Strip the operation down to the native language and domain concepts that actually matter, then reintroduce framework machinery only where it buys a concrete boundary capability.

Do not create another wrapper whose sole job is hiding the framework tax from callers. Pay off the tax by removing unnecessary framework ownership.

## Why This Matters
Framework tax is dangerous because every individual piece looks locally reasonable.

One interface seems harmless. One provider is idiomatic. One decorator is convenient. One config entry is standard. One generated class is “free.” But architecture cost is cumulative: behavior becomes the emergent result of constructs that were each added for consistency rather than necessity.

The system then becomes easy to operate through the framework and hard to understand without it. That is dependency at the level that matters most — the problem model itself.

Framework churn makes the debt visible, but churn is not the only cost. Debugging, testing, onboarding, static reasoning, and refactoring all pay the tax every day.

## Repair Strategy
Find the framework boundary and push it outward:

1. express the core operation as ordinary domain inputs, outputs, and explicit effects;
2. identify which framework features carry real semantics: transaction, request cancellation, host lifecycle, plugin discovery, authentication context, etc.;
3. keep those semantics in narrow adapters/ports;
4. remove registrations/interfaces/providers that exist only because a convention expected them;
5. replace ambient framework context with explicit values when only a small subset is needed;
6. keep framework exceptions/entities/DTOs from leaking into the domain;
7. write core tests without booting the framework wherever the decision itself does not depend on it.

When dynamic substitution is real, keep the abstraction. When there is exactly one implementation and no independent consumer, a named function/module may be the better abstraction.

## Decision Branches
- **Framework owns a real protocol/lifecycle:** retain the adapter and make that boundary explicit.
- **Framework object leaks inward for convenience:** translate/extract at ingress and pass only semantic values.
- **DI abstraction has one implementation and no runtime substitution need:** collapse to direct construction/reference unless test isolation requires a smaller explicit port.
- **Cross-cutting behavior is scattered across hooks/middleware:** choose one semantic owner or make ordering/interaction explicit instead of relying on framework invocation folklore.
- **Generated code mirrors declarations exactly:** treat it as build artifact, not as another domain layer engineers must reason through.
- **Removing framework code would recreate substantial correct machinery:** keep it. Tax is not the same as framework presence.

## Common Wrong Fixes
- Add a “service layer” over framework-heavy code while all real decisions remain inside hooks/entities/controllers.
- Introduce your own mini-framework to abstract the existing framework.
- Wrap every framework type in a one-to-one project type with no semantic translation.
- Create interfaces solely for mocking. Prefer testing stable behavior or injecting the actual effect boundary.
- Ban framework APIs categorically and rebuild mature platform functionality badly. The goal is proportional ownership, not purity theater.
- Move registration/configuration into a different directory and call the tax reduced.

## Verification
A core behavior change should now be explainable and testable mostly in domain terms.

Check that:

- framework types stop at intentional edges;
- removing/replacing the framework adapter would not require rewriting domain decisions;
- each remaining registration/hook/config item can name the capability it buys;
- behavior ordering no longer depends on undocumented framework magic where that ordering matters;
- end-to-end/integration tests remain at the boundaries where framework behavior is genuinely part of the contract.

Invariant:

> Framework machinery carries framework responsibilities; domain machinery carries domain meaning.

## Done When
The framework is again a tool the system uses, not the language in which the system must explain itself.
