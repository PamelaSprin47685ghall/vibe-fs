# order-dependent-test — Main

Make each test own its premises.

For every mutable input the case relies on, choose one of three honest models:

1. **local ownership** — the test creates it and cleans it up;
2. **isolated lease/namespace** — expensive infrastructure is shared, but each test gets independent keys/schema/transaction/state;
3. **one explicit scenario** — the steps truly depend on one lifecycle, so they belong in one ordered test instead of several fake-independent cases.

Start by inventorying hidden state:

- module/static mutable values;
- singleton registries/caches;
- environment variables;
- process cwd/locale/provider preference;
- global fake clocks/random seeds/ID counters;
- reused files/temp dirs/worktrees;
- database rows/schema;
- ports/processes/subscriptions;
- stateful mocks and captured call cursors.

Then scope or reset each one at the test boundary. Prefer fresh identity over “reset everything” when isolation is cheaper and safer: unique session IDs, per-test temp dirs, transactions rolled back after each case, namespaced keys, fresh runtime instances.

If a global must be mutated, save/restore it in a `try/finally`/scoped helper so failure itself cannot leak the mutation into the next test.

Common fake repairs:

- force alphabetical/numeric execution order;
- mark the suite serial / `--runInBand` while retaining hidden shared state;
- add retries because reordering only “sometimes” breaks it;
- move more setup into `beforeAll`, making suite-history coupling stronger;
- add a giant `afterAll` cleanup that means an individual failed test can contaminate every following case;
- reset only the obvious database while caches, env, current directory, static registries, or fake clocks still leak;
- call order dependence “integration realism” when the actual product does not require those cases to share one lifecycle.

Serial execution may be a legitimate resource choice, but it is not proof of test independence. If a case's semantics require another case to run first, the proposition is still non-local even when the runner hides the problem deterministically.

Verification should be adversarial:

```text
run case alone
run it first
run it last
randomize order repeatedly
parallelize where architecture permits
fail a neighbor halfway through setup/cleanup
```

The case must keep the same meaning and verdict under every ordering that the suite claims is legal.

When two cases truly form one causal story, merge them and make that order explicit inside the scenario. A longer honest test is better than two short tests connected by invisible residue.

Also inspect failure isolation. A case that throws halfway through should still release/restore everything it owns; otherwise one failing test can create a cascade of misleading red cases after it.

You are done when every test can explain its premises without the phrase “assuming test X already ran,” and suite scheduling affects throughput only — never semantics.

> Test order may choose when evidence is collected. It must not choose what the evidence means.