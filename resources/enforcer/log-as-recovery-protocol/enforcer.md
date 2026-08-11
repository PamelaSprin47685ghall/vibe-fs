# log-as-recovery-protocol — Enforcer

## Definition
A diagnostic log becomes a false recovery protocol when recovery decides what durable work happened by parsing log messages, their order, or their presence.

## Governing Principle
Logs are observations about execution; durable facts are commitments by the system. The distinction matters because logging usually has weaker guarantees: messages may be dropped, reordered, reformatted, sampled, duplicated, or emitted before the effect they describe is committed. Treating such commentary as history grants authority to a channel that was never designed to bear it.

## Trigger When
Trigger when restart/recovery logic parses logs or infers completed business effects from diagnostic messages rather than a journal, transaction outcome, or authoritative external state.

## Do Not Trigger When
- Do not trigger when the “log” is in fact the deliberately designed append-only event journal with explicit schema, durability, ordering, and replay semantics.
- Do not trigger when logs are attached only after recovery as human explanation of already-decided facts.
- Do not trigger for operator dashboards that display logs but do not drive restart decisions.

## Distinguish From
memory-before-disk orders volatile state against durable facts. recovery-by-filesystem-state infers lifecycle from residue. This rule specifically elevates diagnostic output into recovery authority. Tie-break: if truth is inferred from log prose, use this rule; if from incidental files, use recovery-by-filesystem-state; if memory outruns disk, use memory-before-disk.

## Decision Procedure
Ask whether the channel has a contractual guarantee that every committed fact appears exactly as recovery requires. If not, it may aid diagnosis but cannot define truth.

## Examples
- positive: Restart scans `INFO committed order` lines to decide which orders exist.
- near-miss: Recovery reads the journal; logs are printed afterward to explain the same facts to operators.
- counterexample: Recovery uses the durable event store; logs never enter the decision path.

## Nudge
Diagnostics may explain history; they must not create it. Recover from committed facts and authoritative external state, never from prose emitted along the way.
