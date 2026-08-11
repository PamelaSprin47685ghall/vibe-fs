# unverified-completion-claim — Enforcer

## Definition
A completion claim is unverified when work is declared done before the relevant behavior, build, check, reproduction, or external observation has established that the promised result actually holds.

## Governing Principle
“Done” is not a statement about effort; it is a statement about evidence. Editing creates a candidate solution. Verification turns that candidate into a justified claim by confronting it with an independent condition capable of failure. The root-cause is equating effort with evidence. Without that step, completion collapses intention and reality into one assertion: because the change was made, the desired outcome is assumed.

## Trigger When
Trigger when implementation is reported complete without running the tests, checks, build, reproduction, or observable verification appropriate to the changed contract.

## Do Not Trigger When
- Planning-only work with no behavioral artifact to verify.
- Applicable verification has run and its actual results are part of the completion evidence.
- Remaining verification is listed explicitly and the work is not claimed complete.
- The changed surface has no executable check yet, and that gap is reported as incomplete rather than done.

## Distinguish From
`false-gate` produces unreliable verification. `tool-error-ignored` discards known failed evidence. `release-ladder-skipped` omits ordered proof stages. Tie-break: if the final claim is made before sufficient evidence exists at all, use this rule; if a red tool result was seen and skipped, use `tool-error-ignored`.

## Decision Procedure
Translate “done” into falsifiable acceptance statements, run the narrowest checks that can disprove each one, and report the observed result rather than the intended result.

## Examples
- positive: a patch is declared complete after editing files, with tests never run.
- near-miss: the relevant tests and build ran, failures are listed, and the claim is “not done.”
- counterexample: tests ran red and were ignored while still claiming success — that is `tool-error-ignored`.

## Nudge
Completion is a conclusion, not a feeling about the diff. Earn the word “done” with evidence that could have said “not done.”
