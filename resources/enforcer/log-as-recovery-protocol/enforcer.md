# log-as-recovery-protocol — Enforcer

## Definition
A diagnostic log becomes a false recovery protocol when recovery decides what durable work happened by parsing log messages, their order, or their presence.

## Governing Principle
Logs are observations about execution; durable facts are commitments by the system. The distinction matters because logging usually has weaker guarantees: messages may be dropped, reordered, reformatted, sampled, duplicated, or emitted before the effect they describe is committed. Treating such commentary as history grants authority to a channel that was never designed to bear it.

## Trigger When
Trigger when restart/recovery logic parses logs or infers completed business effects from diagnostic messages rather than a journal, transaction outcome, or authoritative external state.

## Do Not Trigger When
Do not trigger when the “log” is in fact the deliberately designed append-only event journal with explicit schema, durability, ordering, and replay semantics.

## Distinguish From
memory-before-disk orders volatile state against durable facts. recovery-by-filesystem-state infers lifecycle from residue. This rule specifically elevates diagnostic output into recovery authority.

## Decision Procedure
Ask whether the channel has a contractual guarantee that every committed fact appears exactly as recovery requires. If not, it may aid diagnosis but cannot define truth.

## Nudge
Diagnostics may explain history; they must not create it. Recover from committed facts and authoritative external state, never from prose emitted along the way.
