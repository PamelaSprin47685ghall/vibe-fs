# guessed-not-verified — Lesson

## Opinion

### Definition

A claim is guessed-not-verified when a load-bearing factual premise enters engineering reasoning with **borrowed certainty** — from memory, naming, convention, analogy, model prior, or “how this framework usually works” — even though an authoritative source or focused observation could settle it.

The defect is not uncertainty. Good engineering begins with hypotheses constantly. The defect is laundering a hypothesis into a fact before downstream decisions consume it.

### Governing Principle

Perfect reasoning from a false premise is still wrong.

Architecture amplifies premises. A small unverified assumption about an API return shape, lifecycle hook, persistence guarantee, scheduler order, compatibility contract, file format, or ownership boundary can become dozens of coherent downstream decisions. The later the premise is checked, the more expensive truth becomes.

Modern tooling increases this risk because plausible answers are cheap. Documentation snippets, autocomplete, search summaries, generated code, and language models can all produce a story that looks internally consistent. Plausibility is useful for forming hypotheses. It is not provenance.

### Trigger When

Trigger when a material decision depends on a factual claim that has not been verified from the source capable of owning or directly observing that fact. Common forms:

- “this Host hook includes `sessionID`” is assumed from a neighboring hook or type name without reading the actual interface;
- “this API returns null on miss” comes from memory while current version/source is available;
- a file/schema/config is described without reading the actual artifact;
- a framework lifecycle/order guarantee is inferred from intuition instead of source/docs/observation;
- a migration assumes old data shape based on current code rather than inspecting durable samples/version rules;
- an AI/model answer about a library, compiler output, runtime behavior, or repository state is treated as authoritative without checking the owner;
- a security/capability boundary is inferred from prompt text or naming while runtime permissions say something else;
- a failure cause is asserted because it is familiar, before discriminating evidence exists.

### Do Not Trigger When

- The statement is explicitly labeled a hypothesis and is used only to choose the next investigation.
- The authoritative contract/source was already inspected and remains current for the exact claim.
- The claim is immaterial: being wrong would not change the decision or behavior under consideration.
- Direct verification is genuinely impossible or disproportionately expensive, and the uncertainty is preserved as uncertainty while a reversible decision is made.
- The issue is normative (“we should choose X”), not factual (“the system already does X”). Normative disagreement needs judgment, not source lookup.

### Distinguish From

`guess-based-fix` mutates the system until symptoms improve. `guessed-not-verified` occurs earlier: an uncertain premise is silently promoted into truth.

`blind-edit` means mutation begins before enough local ownership/context is understood. The guessed fact may be one cause of blind editing, but this rule specifically names the epistemic violation.

`unverified-completion-claim` concerns the final strength of a completion statement. Here the unsupported fact may appear anywhere in the reasoning chain, long before completion.

### Decision Procedure

Find the sentence on which later decisions depend.

Ask:

1. Is this statement descriptive/factual rather than merely a proposal?
2. If false, would it change the design, patch, migration, review, or conclusion?
3. Who or what owns the fact — source code, current docs/spec, durable artifact, runtime observation, external authoritative system?
4. Has that owner actually been inspected or observed?

If the claim is load-bearing and step 4 is no, it is still a hypothesis regardless of how plausible it sounds.

### Examples

- positive: “OpenCode `tool.definition` has session context, so descriptions can be localized per session.” The actual hook type is never read and in fact contains only `toolID`.
- positive: “The database column is never null” is inferred from application types while old rows are not inspected before migration.
- positive: “This test failure is a timeout issue” is asserted because increasing timeout worked once; no timing trace or completion signal is inspected.
- positive: a generated answer says a dependency exposes an option; code is written against it without checking the installed version.
- near-miss: “Hypothesis: the event can fire before subscription; inspect hook order.” The statement remains explicitly provisional until evidence arrives.
- counterexample: the owning type/source was read and the implementation is based on its exact current contract.

## Advice

### What To Do Now

Stop spending design effort downstream of the guess.

Name the load-bearing premise, identify the source that actually owns it, and verify the premise there before making further irreversible decisions. If the fact cannot yet be settled, downgrade the statement back to a hypothesis and choose only actions safe under that uncertainty.

### Why This Matters

Unverified premises are multiplicative debt.

One wrong assumption about a hook, schema, lifecycle, persistence rule, permission surface, or API can generate pages of perfectly coherent implementation. The coherence makes the mistake harder to notice because every downstream piece agrees with the same false premise.

This is why “but the reasoning made sense” is not comforting. Reasoning quality cannot compensate for missing provenance.

The cheapest time to discover a premise is false is before architecture crystallizes around it. Reading twenty lines of owner code can save hundreds of lines of adapters, tests, compatibility logic, and migration machinery built for a world that never existed.

### Repair Strategy

Classify the claim and go to the closest owner:

- **repository behavior/type/API:** read the owning source and current installed interface;
- **external library/runtime contract:** read current primary documentation/source or run the smallest discriminating experiment when behavior is the true contract;
- **durable data/schema:** inspect actual versioned records/samples and migration rules;
- **host/framework lifecycle:** inspect the hook/event implementation or capture a focused trace;
- **security/capability:** inspect runtime enforcement, not prose alone;
- **failure cause:** collect an observation capable of distinguishing the named cause from a plausible alternative.

Prefer the cheapest source strong enough to settle the decision. Do not fetishize experiments when the contract is directly written in source, and do not fetishize documentation when live behavior is the property that matters.

When the fact will recur, encode the newly established knowledge in a contract test, type, invariant, or durable documentation at the rightful owner so future work does not pay the same discovery cost.

### Decision Branches

- **Owner is easy to inspect:** inspect it now; do not continue reasoning from memory.
- **Only runtime can settle the fact:** design the smallest falsifiable observation and record the result.
- **Several authoritative sources disagree:** preserve the disagreement and determine which source governs the actual boundary/version in use.
- **Verification is currently impossible:** keep the claim explicitly uncertain, prefer reversible design, and avoid hardening the guess into schema/API/state.
- **The premise is immaterial:** stop investigating it; not every uncertainty deserves payment.
- **The question is normative, not factual:** return to the rightful decision authority instead of searching for a source that cannot decide values.

### Common Wrong Fixes

- Search only for snippets that confirm the expected answer.
- Ask the same model/person again and treat repeated plausibility as independent evidence.
- Encode the assumption into a type/comment/abstraction before verifying it, making future readers inherit the guess as doctrine.
- Cite generic documentation while the installed version or host fork differs materially.
- Build a compatibility layer “just in case” for an unverified historical shape.
- Run a huge end-to-end experiment when five lines of canonical source settle the contract more directly.
- Read source that is adjacent to, but does not own, the claim and call that verification.

### Verification

The repaired decision should have a provenance chain short enough to state:

> We rely on X because owner/source Y establishes X under condition/version Z.

Where behavior rather than written contract is decisive, the observation must be reproducible enough to distinguish X from a realistic alternative.

Then check downstream work for assumptions derived from the old guess. Remove compatibility, branches, comments, or abstractions that existed only because the false premise was allowed to propagate.

Invariant:

> Load-bearing facts acquire provenance before they acquire architecture.

### Done When

Hypotheses are free to be bold and cheap.

Facts are expensive enough to deserve evidence.

The codebase no longer contains a confident structure whose foundation is “I thought it probably worked that way.”

### Nudge

A familiar story is not a fact.

Before you build deductions on a premise, make the premise pay rent in evidence.
