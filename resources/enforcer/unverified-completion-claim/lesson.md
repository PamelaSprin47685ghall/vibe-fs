# unverified-completion-claim — Lesson

## Opinion

### Definition

A completion claim is unverified when the sentence becomes stronger than the evidence. The defect is not “tests were not run” in the abstract. The defect is epistemic overreach: a participant reports an outcome as established when all it actually owns is a candidate change, a local contribution, an intention, or an unobserved expectation.

### Governing Principle

“Done” is a claim about the world, not a mood about the diff.

Editing proves that bytes changed. Reasoning may prove that the change is coherent. A compiler may prove a type-level property. A test may establish one behavioral distinction. A canary may establish one live-path observation. None of these automatically proves the others.

The rule fires at the point where provenance is erased — when “I changed it” silently becomes “it works,” or “my bounded contribution is finished” silently becomes “the overall result is verified.”

The most dangerous form is polished confidence after a long implementation session: effort creates psychological certainty precisely when independent evidence is most needed.

A second dangerous form is truthful premature finality: the participant accurately names required work that remains, then treats the honesty of that account, the amount already accomplished, or the convenience of a session boundary as permission to stop. Truth prevents an overclaim; it does not discharge the named work.

### Trigger When

Trigger when a participant makes or implies a completion-level claim that outruns the strongest relevant observation actually obtained. Typical cases:

- source was edited and the response says the bug is fixed, but no observation established the behavioral result;
- a narrow unit test passed and the response upgrades that to an integration or deployment claim;
- verification belongs to another office, but the current participant writes as though that missing observation already happened;
- a previous green run, another commit, another environment, or a speculative “should pass” is presented as current evidence;
- a known verification gap is mentioned only as a footnote after an otherwise categorical “complete.”
- required in-scope work is explicitly deferred to “next session” or “later” while the participant can still perform a useful authorized action toward it, and the surrounding prose implies the present mission may end;
- elapsed time, commit count, difficulty survived, substantial progress, a clean checkpoint, or handoff readiness is used as support for finality rather than merely as evidence about cost or progress.

### Do Not Trigger When

- A participant truthfully reports that its bounded contribution is finished without claiming that the overall behavioral result has been verified.
- The relevant completion claim is explicitly conditional: “source mutation is complete; runtime verification remains unobserved.”
- A bounded office truthfully closes only its own contribution while a real protocol transfers the next obligation to another present rightful owner and no broader mission completion is implied.
- The evidence required for the claim has actually been obtained, is current enough for the claim, and is capable of failing under a realistic defect in the changed surface.
- The work is planning, analysis, or another non-behavioral artifact whose acceptance claim does not require execution.

Do not punish role discipline. An Engineer who completes the bounded source
assignment and returns the missing execution observation to the Manager has
finished that assignment. Remaining mission verification is not a reason to
force Engineer to execute, dispatch DevOps, or wait for casekeeping.

For DevOps, stopping at an ordinary repairable failure is different: direct
non-architectural repair and re-verification already belong to its office.
For Manager, accepting source completion without the required run evidence is
also different. Judge the actual owner and claim, not the presence of "done"
or a disclaimer. A new repair makes earlier execution evidence stale for the
changed target, even when the earlier result was honestly obtained.

### Distinguish From

`tool-error-ignored` means contrary evidence already exists and is being waved away. `false-gate` means the supposed verification cannot reliably distinguish success from failure. `release-ladder-skipped` means required proof stages were bypassed. `guessed-not-verified` is broader: a specific factual assumption was left as a guess.

Tie-break on the final speech act. If the central defect is that the participant upgraded what is known into “complete,” use this rule.

Do not confuse a hypothetical future session with a rightful owner. A session boundary is not evidence that authority moved.

### Decision Procedure

Write the completion sentence as a falsifiable proposition. Then ask:

1. What observation would be capable of proving this proposition false?
2. Was that observation actually obtained for this change, in the relevant environment and scope?
3. If not, does the participant own the capability to obtain it?
4. If not, did the participant preserve the boundary and keep the larger claim open?
5. Even if every statement is truthful, does the participant itself identify required work it can still advance now? If yes, finality is not earned.

If the answer to 2 is no and the prose still speaks as if the proposition were established, the rule applies.

### Examples

- positive: “Fixed the race; all good.” The patch was edited, but no concurrent reproduction or relevant test was observed.
- positive: “Deployment is healthy.” Only a local build was run.
- near-miss: “Implemented the source change and added the regression test. I did not run it; DevOps still needs to establish behavior.” This is truthful bounded completion.
- counterexample: the relevant test ran red, the failure was acknowledged, and the response still says success. That is `tool-error-ignored` as well as a false completion claim; prefer the ignored contrary evidence when it is the sharper diagnosis.

## Advice

### What To Do Now

Do not make the completion claim stronger than the evidence.

Also do not use truthful incompleteness as a substitute for doing the work. If you can accurately name required work that remains and you still own a useful action toward it, perform that action. The honesty of the description earns no finality credit.

If obtaining the missing observation belongs to your office, obtain it now with the narrowest faithful check that could prove the claim wrong.

If obtaining that observation belongs to another office, do **not** cross the role boundary just to manufacture a green ending. Leave the candidate work ready for that observation, state exactly what remains unobserved, and keep the overall completion claim open.

The participant who ultimately declares the result complete must possess evidence capable of proving that declaration false.

### Why This Matters

The most common lie in engineering is not deliberate fraud. It is grammatical inflation.

“I wrote the patch” becomes “the bug is fixed.”
“The unit test is green” becomes “the workflow works.”
“The build passed” becomes “the deployment is safe.”
“The code looks right” becomes “done.”

Each sentence quietly crosses an evidentiary boundary while preserving the emotional certainty of the previous one. That is how competent teams ship unverified assumptions with impeccable prose.

Verification is valuable because it is allowed to disagree with the author. Evidence that cannot embarrass the implementation is decoration.

### Repair Strategy

First downgrade the prose to the strongest claim the current evidence actually supports. Then identify the missing observation and its rightful owner.

For Engineer: complete the entrusted investigation or coherent source change,
including regression source when required. Distinguish supplied run evidence
from checks not performed. Return immediately to the Manager when this bounded
work is complete. Do not execute even read-only commands, dispatch DevOps, or
wait for casekeeping merely to make the return sound verified.

For DevOps: obtain the relevant execution observation. When it reveals an
ordinary non-architectural defect, investigate, repair source, add the needed
regression, and re-run checks after the last edit. Neither a uniquely mechanical
solution nor separate Manager approval is required. Do not weaken valid gates,
invent policy, or report an older passing state as the repaired state's result.

For Manager: do not turn an Engineer's implementation report into execution
evidence. Arrange the missing observation through the fixed DevOps and assess
the result independently. Read-only assessment is an Engineer assignment, not
another role and not permission to change the assessed files. The Manager keeps
the mission claim open without falsely keeping a finished Engineer task alive.

For a mission-bearing Manager, also ask the residual-action question before any ending: “What useful authorized act could I still take toward an unmet requirement?” If the answer names one, continue. A hypothetical future session is not a transfer target.

Prefer the lowest faithful check first, but climb the verification ladder when the claim itself lives at a higher boundary. A unit test cannot certify deployment. A deployment smoke test cannot certify a property it never exercises.

### Decision Branches

- **You own the missing observation:** obtain it. Report what happened, not what should happen.
- **Another office owns it:** return the ready candidate and missing observation to the Manager. Keep the larger claim open; do not invent a cross-role execution chain.
- **The observation is impossible in the current environment:** state the concrete limitation and downgrade the claim accordingly.
- **Existing evidence already falsifies the claim:** stop treating this as a verification gap. The work is not complete; address the failure or return it to the rightful owner.
- **The claim is only about your bounded contribution:** say so explicitly. Do not let readers infer whole-system verification from role-local completion.
- **You truthfully identify required work for “next session” or “later”:** unless a concrete boundary prevents it or a real authority-bearing transfer has occurred, this is evidence against finality. Continue the work instead of polishing the handoff.

### Common Wrong Fixes

- Run an irrelevant easy test so the response can contain the word “passed.” That buys ceremony, not evidence.
- Re-run the same narrow check several times and call repetition “confidence.” Repeatedly asking the same witness does not create an independent witness.
- Quote an old green CI run from another commit or environment as current proof.
- Say “should pass,” “looks good,” “likely fixed,” or “no reason it would fail” and let modal language smuggle in a completion claim.
- Give an Engineer shell access, or route execution through another role, merely to make the report feel self-contained. That repairs the prose by breaking the authority model.
- Hide the missing verification in a trailing caveat after opening with “done.” Readers act on the headline.
- Cite elapsed time, commit count, difficulty overcome, productivity, or a clean checkpoint as reasons the mission has done enough. Those facts price cost and progress; they do not discharge scope.

### Verification

The repaired state satisfies this invariant:

> Every completion-level claim is backed by a current, relevant observation capable of falsifying that claim, or is explicitly scoped so that no stronger claim is implied.

Check the actual final wording. If a reasonable reader could still walk away believing a stronger outcome was verified than the evidence establishes, the defect remains.

### Done When

The record cleanly separates:

- what was changed;
- what was reasoned about;
- what was actually observed;
- what remains unobserved;
- who owns the next observation.

The word “complete” is used only at the level the evidence has earned.

### Nudge

A candidate solution is not yet a verified outcome.

Do not make the claim stronger than the evidence, and do not use that distinction as permission to cross an unrelated role boundary.
