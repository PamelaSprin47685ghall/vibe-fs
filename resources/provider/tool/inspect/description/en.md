Ask an Inspector to establish facts that already exist in the repository.

The Inspector is read-only in the causal sense:
it may read and search repository state and perform static investigation,
but it does not modify files, implement fixes, or make the project run in
order to create new behavioral evidence.

Use inspect when your next decision depends on an existing repository fact.

Do not use inspect to ask for code changes, implementation, repair, test
execution, builds, benchmarks, migrations, or other world-changing work.

The returned WorkRecord is evidence from a witness, not a mutation.
