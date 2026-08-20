# unverified-completion-claim — Main

## What To Do Now
Do not make the completion claim stronger than the evidence.

Also do not use truthful incompleteness as a substitute for doing the work. If you can accurately name required work that remains and you still own a useful action toward it, perform that action. The honesty of the description earns no finality credit.

If obtaining the missing observation belongs to your office, obtain it now with the narrowest faithful check that could prove the claim wrong.

If obtaining that observation belongs to another office, do **not** cross the role boundary just to manufacture a green ending. Leave the candidate work ready for that observation, state exactly what remains unobserved, and keep the overall completion claim open.

The participant who ultimately declares the result complete must possess evidence capable of proving that declaration false.

## Why This Matters
The most common lie in engineering is not deliberate fraud. It is grammatical inflation.

“I wrote the patch” becomes “the bug is fixed.”
“The unit test is green” becomes “the workflow works.”
“The build passed” becomes “the deployment is safe.”
“The code looks right” becomes “done.”

Each sentence quietly crosses an evidentiary boundary while preserving the emotional certainty of the previous one. That is how competent teams ship unverified assumptions with impeccable prose.

Verification is valuable because it is allowed to disagree with the author. Evidence that cannot embarrass the implementation is decoration.

## Repair Strategy
First downgrade the prose to the strongest claim the current evidence actually supports. Then identify the missing observation and its rightful owner.

For a Coder, this usually means: make the source mutation coherent, write the executable regression evidence when required, and explicitly report that runtime behavior remains unobserved. Do not borrow DevOps authority.

For DevOps, this usually means: run the relevant observation, capture the actual result, and keep failure visible rather than laundering it through optimistic interpretation.

For a Manager or Reviewer, do not turn a subordinate's implementation report into independent execution evidence. Ask whether the evidence chain contains a real falsifier at the boundary the final claim depends on.

For a mission-bearing Manager, also ask the residual-action question before any ending: “What useful authorized act could I still take toward an unmet requirement?” If the answer names one, continue. A hypothetical future session is not a transfer target.

Prefer the lowest faithful check first, but climb the verification ladder when the claim itself lives at a higher boundary. A unit test cannot certify deployment. A deployment smoke test cannot certify a property it never exercises.

## Decision Branches
- **You own the missing observation:** obtain it. Report what happened, not what should happen.
- **Another office owns it:** hand off the ready candidate and name the missing observation. Keep the larger claim open.
- **The observation is impossible in the current environment:** state the concrete limitation and downgrade the claim accordingly.
- **Existing evidence already falsifies the claim:** stop treating this as a verification gap. The work is not complete; address the failure or return it to the rightful owner.
- **The claim is only about your bounded contribution:** say so explicitly. Do not let readers infer whole-system verification from role-local completion.
- **You truthfully identify required work for “next session” or “later”:** unless a concrete boundary prevents it or a real authority-bearing transfer has occurred, this is evidence against finality. Continue the work instead of polishing the handoff.

## Common Wrong Fixes
- Run an irrelevant easy test so the response can contain the word “passed.” That buys ceremony, not evidence.
- Re-run the same narrow check several times and call repetition “confidence.” Repeatedly asking the same witness does not create an independent witness.
- Quote an old green CI run from another commit or environment as current proof.
- Say “should pass,” “looks good,” “likely fixed,” or “no reason it would fail” and let modal language smuggle in a completion claim.
- Give a Coder shell access, or route execution through another role, merely to make the report feel self-contained. That repairs the prose by breaking the authority model.
- Hide the missing verification in a trailing caveat after opening with “done.” Readers act on the headline.
- Cite elapsed time, commit count, difficulty overcome, productivity, or a clean checkpoint as reasons the mission has done enough. Those facts price cost and progress; they do not discharge scope.

## Verification
The repaired state satisfies this invariant:

> Every completion-level claim is backed by a current, relevant observation capable of falsifying that claim, or is explicitly scoped so that no stronger claim is implied.

Check the actual final wording. If a reasonable reader could still walk away believing a stronger outcome was verified than the evidence establishes, the defect remains.

## Done When
The record cleanly separates:

- what was changed;
- what was reasoned about;
- what was actually observed;
- what remains unobserved;
- who owns the next observation.

The word “complete” is used only at the level the evidence has earned.
