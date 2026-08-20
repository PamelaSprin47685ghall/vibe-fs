# unverified-completion-claim — Enforcer

## Definition
A completion claim is unverified when the sentence becomes stronger than the evidence. The defect is not “tests were not run” in the abstract. The defect is epistemic overreach: a participant reports an outcome as established when all it actually owns is a candidate change, a local contribution, an intention, or an unobserved expectation.

## Governing Principle
“Done” is a claim about the world, not a mood about the diff.

Editing proves that bytes changed. Reasoning may prove that the change is coherent. A compiler may prove a type-level property. A test may establish one behavioral distinction. A canary may establish one live-path observation. None of these automatically proves the others.

The rule fires at the point where provenance is erased — when “I changed it” silently becomes “it works,” or “my bounded contribution is finished” silently becomes “the overall result is verified.”

The most dangerous form is polished confidence after a long implementation session: effort creates psychological certainty precisely when independent evidence is most needed.

A second dangerous form is truthful premature finality: the participant accurately names required work that remains, then treats the honesty of that account, the amount already accomplished, or the convenience of a session boundary as permission to stop. Truth prevents an overclaim; it does not discharge the named work.

## Trigger When
Trigger when a participant makes or implies a completion-level claim that outruns the strongest relevant observation actually obtained. Typical cases:

- source was edited and the response says the bug is fixed, but no observation established the behavioral result;
- a narrow unit test passed and the response upgrades that to an integration or deployment claim;
- verification belongs to another office, but the current participant writes as though that missing observation already happened;
- a previous green run, another commit, another environment, or a speculative “should pass” is presented as current evidence;
- a known verification gap is mentioned only as a footnote after an otherwise categorical “complete.”
- required in-scope work is explicitly deferred to “next session” or “later” while the participant can still perform a useful authorized action toward it, and the surrounding prose implies the present mission may end;
- elapsed time, commit count, difficulty survived, substantial progress, a clean checkpoint, or handoff readiness is used as support for finality rather than merely as evidence about cost or progress.

## Do Not Trigger When
- A participant truthfully reports that its bounded contribution is finished without claiming that the overall behavioral result has been verified.
- The relevant completion claim is explicitly conditional: “source mutation is complete; runtime verification remains unobserved.”
- A bounded office truthfully closes only its own contribution while a real protocol transfers the next obligation to another present rightful owner and no broader mission completion is implied.
- The evidence required for the claim has actually been obtained, is current enough for the claim, and is capable of failing under a realistic defect in the changed surface.
- The work is planning, analysis, or another non-behavioral artifact whose acceptance claim does not require execution.

Do not punish role discipline. A Coder who correctly says “the source change is coherent; execution remains for DevOps” is not incomplete in its own office merely because the world still needs another observation.

## Distinguish From
`tool-error-ignored` means contrary evidence already exists and is being waved away. `false-gate` means the supposed verification cannot reliably distinguish success from failure. `release-ladder-skipped` means required proof stages were bypassed. `guessed-not-verified` is broader: a specific factual assumption was left as a guess.

Tie-break on the final speech act. If the central defect is that the participant upgraded what is known into “complete,” use this rule.

Do not confuse a hypothetical future session with a rightful owner. A session boundary is not evidence that authority moved.

## Decision Procedure
Write the completion sentence as a falsifiable proposition. Then ask:

1. What observation would be capable of proving this proposition false?
2. Was that observation actually obtained for this change, in the relevant environment and scope?
3. If not, does the participant own the capability to obtain it?
4. If not, did the participant preserve the boundary and keep the larger claim open?
5. Even if every statement is truthful, does the participant itself identify required work it can still advance now? If yes, finality is not earned.

If the answer to 2 is no and the prose still speaks as if the proposition were established, the rule applies.

## Examples
- positive: “Fixed the race; all good.” The patch was edited, but no concurrent reproduction or relevant test was observed.
- positive: “Deployment is healthy.” Only a local build was run.
- near-miss: “Implemented the source change and added the regression test. I did not run it; DevOps still needs to establish behavior.” This is truthful bounded completion.
- counterexample: the relevant test ran red, the failure was acknowledged, and the response still says success. That is `tool-error-ignored` as well as a false completion claim; prefer the ignored contrary evidence when it is the sharper diagnosis.

## Nudge
A candidate solution is not yet a verified outcome.

Do not make the claim stronger than the evidence, and do not use that distinction as permission to cross an unrelated role boundary.
