# status-announcement-noise — Enforcer

## Definition
Status announcement noise is communication that repeatedly reports routine motion without adding a decision, changed fact, failure, uncertainty, or action the recipient can use.

## Governing Principle
Attention is a finite channel. Messages that carry no new state consume the same interruption budget as messages that matter, reducing the signal-to-noise ratio until important information becomes easier to miss. Progress communication earns its place when it changes the receiver’s model of the work, not merely when another internal step happened.

## Trigger When
Trigger when logs, comments, production output, or agent/user messages repeatedly narrate ordinary progress such as “starting,” “still working,” or each mechanical substep without a meaningful state transition.

## Do Not Trigger When
- Protocol-required progress heartbeats mandated by a transport, job, or orchestration contract.
- User interfaces where progress itself is a product need (percent complete, remaining work).
- Concise updates at genuine phase boundaries during long work, each carrying a new fact or risk.
- A single start/end pair for an otherwise silent operation.

## Distinguish From
`comment-theater` is redundant prose around code. `debug-print-left` is accidental investigation output. Tie-break: if leftover diagnostic prints remain after investigation, use `debug-print-left`; if the messages are intentional status with no new information, use this rule.

## Decision Procedure
For each message ask what new decision, result, risk, failure, or required action the recipient learns. If the answer is none, remove or aggregate it.

## Examples
- positive: an agent posts “starting step 3”, “still working”, and “step 3 done” for every internal substep with no decision, failure, or changed fact.
- near-miss: a long migration reports once per documented phase with counts, remaining risk, and the next required action.
- counterexample: a leftover `console.log` from a failed investigation — that is `debug-print-left`, not status-announcement-noise.

## Nudge
Report changes in meaning, not motion. Preserve attention for decisions, material progress, failures, uncertainty, and actions the recipient can actually use.
