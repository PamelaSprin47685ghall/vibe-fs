# guessed-not-verified — Enforcer

## Definition
A claim is guessed-not-verified when a load-bearing factual premise enters engineering reasoning with **borrowed certainty** — from memory, naming, convention, analogy, model prior, or “how this framework usually works” — even though an authoritative source or focused observation could settle it.

The defect is not uncertainty. Good engineering begins with hypotheses constantly. The defect is laundering a hypothesis into a fact before downstream decisions consume it.

## Governing Principle
Perfect reasoning from a false premise is still wrong.

Architecture amplifies premises. A small unverified assumption about an API return shape, lifecycle hook, persistence guarantee, scheduler order, compatibility contract, file format, or ownership boundary can become dozens of coherent downstream decisions. The later the premise is checked, the more expensive truth becomes.

Modern tooling increases this risk because plausible answers are cheap. Documentation snippets, autocomplete, search summaries, generated code, and language models can all produce a story that looks internally consistent. Plausibility is useful for forming hypotheses. It is not provenance.

## Trigger When
Trigger when a material decision depends on a factual claim that has not been verified from the source capable of owning or directly observing that fact. Common forms:

- “this Host hook includes `sessionID`” is assumed from a neighboring hook or type name without reading the actual interface;
- “this API returns null on miss” comes from memory while current version/source is available;
- a file/schema/config is described without reading the actual artifact;
- a framework lifecycle/order guarantee is inferred from intuition instead of source/docs/observation;
- a migration assumes old data shape based on current code rather than inspecting durable samples/version rules;
- an AI/model answer about a library, compiler output, runtime behavior, or repository state is treated as authoritative without checking the owner;
- a security/capability boundary is inferred from prompt text or naming while runtime permissions say something else;
- a failure cause is asserted because it is familiar, before discriminating evidence exists.

## Do Not Trigger When
- The statement is explicitly labeled a hypothesis and is used only to choose the next investigation.
- The authoritative contract/source was already inspected and remains current for the exact claim.
- The claim is immaterial: being wrong would not change the decision or behavior under consideration.
- Direct verification is genuinely impossible or disproportionately expensive, and the uncertainty is preserved as uncertainty while a reversible decision is made.
- The issue is normative (“we should choose X”), not factual (“the system already does X”). Normative disagreement needs judgment, not source lookup.

## Distinguish From
`guess-based-fix` mutates the system until symptoms improve. `guessed-not-verified` occurs earlier: an uncertain premise is silently promoted into truth.

`blind-edit` means mutation begins before enough local ownership/context is understood. The guessed fact may be one cause of blind editing, but this rule specifically names the epistemic violation.

`unverified-completion-claim` concerns the final strength of a completion statement. Here the unsupported fact may appear anywhere in the reasoning chain, long before completion.

## Decision Procedure
Find the sentence on which later decisions depend.

Ask:

1. Is this statement descriptive/factual rather than merely a proposal?
2. If false, would it change the design, patch, migration, review, or conclusion?
3. Who or what owns the fact — source code, current docs/spec, durable artifact, runtime observation, external authoritative system?
4. Has that owner actually been inspected or observed?

If the claim is load-bearing and step 4 is no, it is still a hypothesis regardless of how plausible it sounds.

## Examples
- positive: “OpenCode `tool.definition` has session context, so descriptions can be localized per session.” The actual hook type is never read and in fact contains only `toolID`.
- positive: “The database column is never null” is inferred from application types while old rows are not inspected before migration.
- positive: “This test failure is a timeout issue” is asserted because increasing timeout worked once; no timing trace or completion signal is inspected.
- positive: a generated answer says a dependency exposes an option; code is written against it without checking the installed version.
- near-miss: “Hypothesis: the event can fire before subscription; inspect hook order.” The statement remains explicitly provisional until evidence arrives.
- counterexample: the owning type/source was read and the implementation is based on its exact current contract.

## Nudge
A familiar story is not a fact.

Before you build deductions on a premise, make the premise pay rent in evidence.
