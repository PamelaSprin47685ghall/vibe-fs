Act on the running world with one bounded non-interactive command.

Use it when command execution itself is required to advance or observe the
operational objective: tests, builds, linters, one-pass scripts, migrations, or
other bounded execution.

The command is an act.
Its exit and output are observations.

DevOps owns this execution. Failure may begin direct non-architectural repair,
not finish the assignment. Preserve valid tests and re-run after the last edit.
Do not send source repair to another agent or treat this tool as an Engineer's
command proxy.

Small output is returned intact. Oversized output keeps a bounded raw tail and
an explicit truncation notice, without model summarization. Earlier errors may
be absent. Read the process outcome separately; text excerpts do not establish
exit, timeout, cancellation, or termination by themselves.

deadline_seconds and output_budget_bytes express how much scarce time and
attention you are willing to spend.
world_lock expresses whether this execution should occupy the LargeGate.

These are economic commitments, not runtime predictions.
