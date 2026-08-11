# serial-investigation — Enforcer

## Definition
Investigation is unnecessarily serial when independent questions are answered one after another despite having no informational dependency. The root-cause is that already-specified evidence requests are forced onto one critical path, so competing hypotheses wait for facts they do not need and early findings can anchor the story first.

## Governing Principle
Inquiry has a dependency graph just as computation does. Serializing independent evidence gathering invents a critical path: later facts wait for earlier facts they do not need. Parallel investigation is not haste; it is faithful execution of the epistemic graph, followed by synthesis only where evidence actually converges.

## Trigger When
Trigger when unrelated file reads, searches, source inspections, logs, or diagnostics are performed sequentially while each could be issued from the same current knowledge.

## Do Not Trigger When
- Each result determines the next question, so later queries cannot be formulated yet.
- Shared tooling imposes a concurrency limit that is already saturated.
- The investigation must preserve ordering to avoid destructive interference.
- There is only one remaining question, so parallelism has no independent peer.

## Distinguish From
`serial-when-parallel` is the general execution smell for independent runtime or tool operations. `unbounded-fanout` is the opposite failure of respecting independence without a bound. `blind-edit` mutates before the territory is mapped. This rule focuses on evidence gathering. Tie-break: if the serialized work is inquiry, this rule owns the case.

## Decision Procedure
1. Draw the current questions as nodes.
2. Add an edge only when one answer is needed to formulate another.
3. Issue all edge-free questions concurrently.
4. Synthesize before launching the next dependent wave.

## Examples
- positive: three independent file reads about the same crash are issued one after another though each query is already fully specified.
- near-miss: a stack trace must be read before the next search query can be named; that edge is a real dependency.
- counterexample: independent greps and file reads are issued together, then synthesized before the next wave.

## Nudge
Investigate according to dependency, not habit. Gather independent evidence in parallel and spend serial attention only where one fact truly determines the next question.
