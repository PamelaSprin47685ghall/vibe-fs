# unverified-completion-claim — Enforcer

## Definition
A completion claim is unverified when work is declared done before the relevant behavior, build, check, reproduction, or external observation has established that the promised result actually holds.

## Governing Principle
“Done” is not a statement about effort; it is a statement about evidence. Editing creates a candidate solution. Verification turns that candidate into a justified claim by confronting it with an independent condition capable of failure. Without that step, completion collapses intention and reality into one assertion: because the change was made, the desired outcome is assumed.

## Trigger When
Trigger when implementation is reported complete without running the tests, checks, build, reproduction, or observable verification appropriate to the changed contract.

## Do Not Trigger When
Do not trigger for planning-only work with no behavioral artifact, or when applicable verification has run and its actual results are part of the completion evidence.

## Distinguish From
false-gate produces unreliable verification. tool-error-ignored discards known failed evidence. release-ladder-skipped omits ordered proof stages. This rule concerns making the final claim before sufficient evidence exists at all.

## Decision Procedure
Translate “done” into falsifiable acceptance statements, run the narrowest checks that can disprove each one, and report the observed result rather than the intended result.

## Nudge
Completion is a conclusion, not a feeling about the diff. Earn the word “done” with evidence that could have said “not done.”
