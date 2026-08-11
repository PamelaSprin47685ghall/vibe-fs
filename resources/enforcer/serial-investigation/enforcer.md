# serial-investigation — Enforcer

## Definition
Investigation is unnecessarily serial when independent questions are answered one after another despite having no informational dependency.

## Governing Principle
Inquiry has a dependency graph just as computation does. Serializing independent evidence gathering imposes an artificial critical path: later facts wait for earlier facts they do not need. Parallel investigation is not haste; it is faithful execution of the epistemic graph, followed by synthesis only where evidence actually converges.

## Trigger When
Trigger when unrelated file reads, searches, source inspections, logs, or diagnostics are performed sequentially while each could be issued from the same current knowledge.

## Do Not Trigger When
Do not trigger when each result determines the next question, shared tooling imposes a limit, or the investigation must preserve ordering to avoid destructive interference.

## Distinguish From
serial-when-parallel is the general execution smell. This rule focuses on evidence gathering, where unnecessary serialization delays the moment competing hypotheses can be compared.

## Decision Procedure
Draw the current questions as nodes and add an edge only when one answer is needed to formulate another. Issue all edge-free questions concurrently, then synthesize before the next dependent wave.

## Nudge
Investigate according to dependency, not habit. Gather independent evidence in parallel and spend serial attention only where one fact truly determines the next question.
