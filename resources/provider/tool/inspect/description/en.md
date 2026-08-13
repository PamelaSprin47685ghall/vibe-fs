Ask an Inspector to establish facts that already exist in the repository.

The Inspector is read-only in the causal sense.

It may inspect source, history, configuration, metadata, and artifacts already
produced by earlier events. It may perform static investigation needed to
establish those facts.

It does not modify files.
It does not implement or repair code.
It does not compile, build, test, benchmark, migrate, start the application, or
otherwise make the project run in order to create new behavioral evidence.

Use inspect when your next decision depends on what is already true in the
local repository.

Do not use inspect to ask for code changes.
Do not use inspect for implementation, runtime verification, or operational work.

The returned WorkRecord is evidence from a witness.
It is not a mutation and it is not behavioral execution evidence.
