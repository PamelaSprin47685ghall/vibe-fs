# test-implementation-coupled — Enforcer

A test is implementation-coupled when it punishes a correct refactor because it froze **how the code currently works** instead of **what the supported contract requires**.

The central diagnostic is substitutability:

> Could a different implementation satisfy every real caller-visible promise and still fail this test?

If yes, the test may be protecting private choreography rather than behavior.

Common coupling targets include:

- exact private helper call counts;
- internal method names or object layout;
- intermediate field values never exposed as contract;
- incidental sequence of pure computations;
- mocks asserting which helper called which helper;
- snapshots of internal JSON/state whose exact shape is not public;
- tests reaching private members through reflection/test-only exports;
- algorithm-specific steps where several equivalent algorithms are valid.

Why this is costly: such tests make the old implementation an unofficial second specification. A simpler algorithm, changed decomposition, removed helper, batched call, or equivalent data structure causes red even though users would observe no regression. Teams then stop refactoring because the suite charges a tax for changing details nobody promised.

Worse, implementation-coupled tests can still miss real bugs. Code may reproduce the expected helper choreography while returning the wrong public result. The suite has frozen motion, not meaning.

Do not overcorrect. Some interactions **are** contractual. Exactly-once publication, “zero external calls on rejection,” transaction boundaries, durable ordering, idempotency-key reuse, provider call sequence required by a real protocol — these can legitimately be observed. The criterion is whether a conforming implementation is allowed to vary the detail.

This differs from `weakened-test-to-pass`: that rule deletes or loosens a valid behavioral expectation because production fails it. Here the expectation was never a legitimate promise in the first place. Removing private choreography can strengthen the suite if the replacement assertion moves outward to the real contract.

`behavioral-boundary-untested` often coexists with this smell: the suite has hundreds of internal assertions yet no proof through the supported entrance.

A useful thought experiment is a semantics-preserving rewrite: replace a helper chain with one pure function, change a list to a map, batch two internal calls, inline/remove a private method, reorder independent calculations. If the test fails and nobody can name a contract violation, it is coupled.

> Tests should make wrong behavior expensive and correct refactoring cheap. If the reverse is true, the suite is guarding implementation nostalgia.