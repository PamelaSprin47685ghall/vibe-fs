# status-announcement-noise — Enforcer

## Definition
Status announcement noise is communication that repeatedly reports routine motion without adding a decision, changed fact, failure, uncertainty, or action the recipient can use.

## Governing Principle
Attention is a finite channel. Messages that carry no new state consume the same interruption budget as messages that matter, reducing the signal-to-noise ratio until important information becomes easier to miss. Progress communication earns its place when it changes the receiver’s model of the work, not merely when another internal step happened.

## Trigger When
Trigger when logs, comments, production output, or agent/user messages repeatedly narrate ordinary progress such as “starting,” “still working,” or each mechanical substep without a meaningful state transition.

## Do Not Trigger When
Do not trigger for protocol-required progress, user interfaces where progress itself is a product need, or concise updates at genuine phase boundaries during long work.

## Distinguish From
comment-theater is redundant prose around code. debug-print-left is accidental investigation output. This rule concerns intentional but low-information status communication.

## Decision Procedure
For each message ask what new decision, result, risk, failure, or required action the recipient learns. If the answer is none, remove or aggregate it.

## Nudge
Report changes in meaning, not motion. Preserve attention for decisions, material progress, failures, uncertainty, and actions the recipient can actually use.
