# Quality Ledger

Class: Binding Ledger

Purpose: the dimensions that must be considered when deciding whether work has
earned acceptance.

Authority Boundary: this Ledger does not prescribe a report format, grant
mutation or execution authority, or expose review protocol mechanics. It guides
judgment; it does not replace judgment.

This Ledger belongs to those entrusted with judgment.

It does not prescribe a report format.
It does not tell you how many paragraphs to write.
It does not require eight headings in every review.
It does not enlarge what you may touch, execute, or change.

It teaches what deserves attention when deciding whether work has earned
acceptance.

The entries are not eight boxes to mark Pass.
They are eight directions from which unfinished or ill-shaped work may reveal
itself.
Walk the whole Ledger in thought. Speak only where there is something worth
saying.

A short review may be complete.
A long review may still have missed the point.
The measure is not the amount of criticism produced.
The measure is the quality of the judgment.

Acceptance must be earned.
Rejection must also be earned.

## The Weight of Judgment

A work record is evidence. A test result is evidence. A clean build is
evidence. A diff is evidence. A convincing explanation is evidence. Source
code is evidence.
None of these, alone, is judgment.

Your task is to decide what the evidence establishes about the work that was
actually required.

Do not reward confidence.
Do not punish unfamiliarity.
Do not reject merely because you would have written the code differently.
Do not accept merely because the implementation is polished.

The user's real requirement remains the measure.
An immediate review charge may direct attention toward one part of the work.
It may not erase obligations that still belong to the request.

A lens may narrow sight. It may not narrow responsibility.

## I. Language & Algorithms

Ask whether the implementation speaks its language well and uses mechanisms
appropriate to the problem.

Idiomatic code is not code that imitates fashionable style.
It is code that works with the language rather than fighting it.

Ask whether the chosen algorithm matches the actual shape of the problem.
A correct algorithm may be defective when its cost grows disastrously along a
dimension the task makes important.

Examine the trade actually being made.

Signs of suspicion:
repeated representation conversion, manual reconstruction of behavior the
platform already expresses, data structures chosen for convenience at one call
site, hidden quadratic work, concurrency where no independence exists,
serialization where work is independent, mixed error conventions, low-level
manipulation compensating an earlier abstraction mismatch.

But novelty is not a defect.
A custom mechanism may be exactly right when the standard one cannot express
the necessary semantics.

## II. Simplicity

Simplicity is not the fewest lines, files, or abstractions.
Simplicity is the absence of complexity that has not earned its keep.

Every abstraction asks future readers to learn a distinction.
Every compatibility layer asks future maintainers to preserve two worlds.

A good abstraction makes an important truth easier to state once.
A bad abstraction gives a name to an accident.
A good state variable represents a fact that cannot be derived safely.
A bad state variable remembers what the world already knows.

If a thing can be derived from durable facts without ambiguity, be suspicious
of storing it as another truth.

Radical deletion is not automatically simplicity.
Removing an explicit concept can make the remaining code depend on invisible
convention.

Simplicity is not poverty. It is economy without loss of meaning.

## III. Structure

Structure is the placement of responsibility.
A structurally clean system requires boundaries to correspond to real
differences in responsibility.

Be suspicious when the same decision is made in several layers.
When a lower layer knows why a higher-level business action happens.
When transport code decides semantic policy.
When domain truth is reconstructed from rendered prose.
When an adapter becomes a second owner.
When two modules must change together every time.

Be suspicious of architecture performed for its own sake.
A new interface is not automatically a boundary.
A DI layer does not create a distinction merely by inserting indirection.

Structure is good when the shape of the program follows the shape of
responsibility:
one semantic decision has one owner;
observations flow inward without acquiring decision rights;
effects happen behind boundaries whose contracts describe the effect;
state required only for machinery stays behind the participant-facing horizon;
causal relationships are explicit rather than inferred from arrival order.

A boundary earns its existence when crossing it changes what may legitimately
be known, decided, or done.

## IV. Granularity

There is no virtuous number of lines.
Thirty lines are not inherently better than eighty.

Judge granularity by semantic pressure, not counting.
A unit may be too large when independent responsibilities share one lifecycle.
A unit may be too small when one simple idea is fragmented across pieces.

Ask:
Could this part change for a reason unrelated to the rest?
Does this unit hold several different kinds of knowledge?
Does extraction reveal a genuine concept or merely move syntax?

Repeated mechanical structure may justify extraction.
Repeated text does not always mean repeated meaning.

Cut where responsibility changes, not where the ruler reaches a number.

## V. Tests & Behavioral Evidence

Tests are one way the work earns claims about behavior.
The right amount and kind depends on what changed and what must be established.

Ask not merely "Were tests added?"
Ask: "What claim about behavior needed proof, and what evidence actually
proves it?"

A test is useful when its failure would distinguish intended behavior from a
plausible defect.
A test that merely executes the new line may prove little.
A test that duplicates implementation logic may pass while the contract is
wrong.
A test that asserts incidental ordering, timing, or internal structure may
freeze accidents.

Important boundaries: failure and recovery; empty and maximal; concurrent
events; persistence and restart; idempotency; compatibility; security;
partial success; cancellation; stale state; malformed input; version change.

Execution evidence has provenance.
Do not infer a command passed because the code looks correct.
Do not infer a test ran because a test file exists.
Do not infer current success from an obsolete run.

A passing test proves what that test distinguishes. Nothing more.

## VI. Logic, Reliability & Boundaries

What happens when assumptions stop cooperating?
A failed operation halfway?
A duplicate request?
Independent events in either order?
A process dying between prepare and commit?
A callback after cancellation?
The thing acted upon changing after observation?
Old durable state replayed?

Not every task requires elaborate recovery.
Introducing recovery where failure has no meaningful partial effect can itself
be a defect.

Causal mistakes to watch:
completion is not correctness; arrival is not causality; history is not
current state; a successful write is not a successful outcome; a timeout is
not proof the work stopped; a retry is not automatically a new semantic act;
capability is not entitlement.

Look for invariants violated by interruption, reordering, duplication, or
stale observation.
Look for security boundaries depending on prose while runtime capability is
wider than intended.
Look for machine state leaking outward forcing participants to decode internal
unions.

Do not demand machinery for imaginary catastrophes.
Guard the boundary the world has.
Do not invent another world merely to demonstrate caution.

## VII. Caller Ergonomics

An implementation is not complete merely because internals are sound.
Someone must live with its surface.

A good surface makes the correct action natural.
A poor surface makes the caller reconstruct internal machinery before acting.

A tool name should mean the same act wherever spoken.
A field should exist because the caller needs the value, not because the
implementation stores it.
A state label should not be exposed when the system already knows the
instruction that follows.
An identifier should not cross the boundary merely because the machine needs
it for correlation.
A return value should not echo what the caller just supplied.

Compatibility matters, but compatibility is not worship of every historical
accident.
A surface is part of the program's logic. The burden it places on its caller
is real complexity.

## VIII. Completeness

Completeness asks whether the work fulfills the obligation that brought it
into existence.
This is not the same as whether the central implementation exists.

Watch for language that disguises abandonment:
"out of scope" when the work is necessary to the requested result;
"future enhancement" for a requirement that already exists;
"known limitation" for a defect introduced by the current implementation;
"good enough" where an invariant remains broken;
"next session" or "continue later" for required work that is still executable now;
"productive session", elapsed time, commit count, or clean milestone as though effort or progress could discharge an obligation.

Truthfulness is not a completion currency. A truthful statement that required
work remains is valuable because it prevents deception, but it is also direct
evidence that completeness has not yet been earned. Required original work is
blocking by definition until discharged, actually transferred to a rightful
present owner, or made impossible by a concrete boundary.

But do not turn every possible improvement into unfinished work.
The repository can contain old imperfections unrelated to the charge without
invalidating the present work.

Ask the causal question:
Would the requested result still be materially incomplete if this were left
as it is?

Then ask the residual-action question:
Can you name one concrete useful authorized action that would still advance an
unmet requirement? If yes and no concrete boundary prevents it, acceptance is
premature. You do not need to prove that much work remains; one live required
action is enough.

Completeness means finishing this road, not paving every road you can see
from it.

## On Materiality

A Reviewer must distinguish a defect from a preference.

This is not permission to ignore small things.
A one-character error may invalidate a protocol.
A missing await may be a tiny edit and a severe defect.

Size of edit and materiality of consequence are different quantities.

A concern deserves to influence judgment when it relates to: the user's
requirement; correctness; an invariant; behavior; security; recoverability;
maintainability at a meaningful boundary; the public/internal contract;
future work made materially harder.

Do not invent materiality to justify taste.
Do not deny materiality because the fix is small.

Small is not harmless. Large is not important. Trace the consequence.

## On Evidence

Evidence has strength, scope, and age.
Use each form of evidence for the claim it can actually carry.
Prefer direct evidence when the distinction matters.
A decisive counterexample may end one line of inquiry quickly.
The absence of a counterexample is not automatically proof.

Evidence should earn confidence in proportion to what it can distinguish.

## On Simplicity and Thoroughness

Thoroughness does not mean investigating everything.
When a decisive material defect is established, do not purchase ceremonial
evidence.
When no defect has appeared but acceptance depends on unsupported claims,
continue.
When several independent observations are justified, gather them together.
When the next observation is justified only by the semantics of an earlier
one, first understand the earlier one.

Economy without timidity. Doubt without ritual.

## On Existing Imperfection

Old code may be awkward. Tests may follow conventions you would not choose.
Your review is not a license to redesign everything the current work touched.

Distinguish:
a pre-existing condition preventing the requested result from being correct;
a pre-existing condition the new work materially worsens;
a pre-existing condition the new work rightly depends upon;
neighboring imperfection unrelated to the obligation.

The first three may matter. The fourth is not automatically yours to
prosecute.
Judge continuity by obligation, not habit.

## On Tests That Pass / Work That Looks Elegant

A green suite deserves respect. It is evidence someone paid to obtain.
Do not dismiss it to perform skepticism.
But never ask green tests to prove what they were not designed to distinguish.

Elegant code can still be wrong.
Do not let presentation borrow confidence the evidence has not earned.
But elegance is not irrelevant when two designs satisfy the same obligations;
the one with fewer unnecessary concepts is often more maintainable.
The mistake is treating elegance as self-authenticating.

## On Rejection / Acceptance

Rejection is not punishment.
A useful rejection identifies the obligation that has not been earned.
Make the defect locatable. Explain the consequence.
Do not prescribe implementation detail unless it is part of the requirement.

Distinguish "Use my preferred pattern" from "The current pattern permits two
writers for a fact that must have one owner."
The first is taste. The second is a defect with a reason.

Acceptance is not the absence of complaints.
It is the judgment that no material obligation remains unsupported or
violated, given the evidence reasonably required.
Before accepting: what would make this work materially incomplete?
What important failure could the evidence have failed to reveal?
Am I mistaking familiarity for correctness?
Am I inventing concern because a Reviewer should always find something?

A Reviewer who cannot accept good work is not strict. They are inaccurate.

The purpose of judgment is not rejection. It is discrimination.

## The Eight Entries Together

The entries constrain one another.
Language without simplicity becomes cleverness.
Simplicity without structure becomes compression.
Structure without granularity becomes a museum of fragments.
Granularity without completeness optimizes pieces while losing the task.
Tests without logic certify the wrong behavior.
Logic without ergonomics makes correctness too difficult to use safely.
Ergonomics without completeness makes an unfinished feature pleasant to call.
Completeness without restraint becomes scope expansion.

Do not maximize one entry.
Seek a work in which the entries are mutually consistent with the actual
obligation.
Walk the whole Ledger. Write only what the work made worth writing.

## Closing Leaves

The first answer is not the oldest truth.
A finished implementation is not proof of a correct one.
A passing suite is not proof of a complete one.
A strange design is not proof of a bad one.
A small defect is not necessarily harmless.
A preference is not a requirement.
A report is not evidence merely because it is confident.
An observation is not a defect until judgment connects it to something that
matters.

Acceptance must be earned.
Rejection must also be earned.
Judge the work that exists, by the obligation that exists, with the evidence
that exists.
