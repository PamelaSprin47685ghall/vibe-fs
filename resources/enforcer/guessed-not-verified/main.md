# guessed-not-verified — Main

## What To Do Now
Stop spending design effort downstream of the guess.

Name the load-bearing premise, identify the source that actually owns it, and verify the premise there before making further irreversible decisions. If the fact cannot yet be settled, downgrade the statement back to a hypothesis and choose only actions safe under that uncertainty.

## Why This Matters
Unverified premises are multiplicative debt.

One wrong assumption about a hook, schema, lifecycle, persistence rule, permission surface, or API can generate pages of perfectly coherent implementation. The coherence makes the mistake harder to notice because every downstream piece agrees with the same false premise.

This is why “but the reasoning made sense” is not comforting. Reasoning quality cannot compensate for missing provenance.

The cheapest time to discover a premise is false is before architecture crystallizes around it. Reading twenty lines of owner code can save hundreds of lines of adapters, tests, compatibility logic, and migration machinery built for a world that never existed.

## Repair Strategy
Classify the claim and go to the closest owner:

- **repository behavior/type/API:** read the owning source and current installed interface;
- **external library/runtime contract:** read current primary documentation/source or run the smallest discriminating experiment when behavior is the true contract;
- **durable data/schema:** inspect actual versioned records/samples and migration rules;
- **host/framework lifecycle:** inspect the hook/event implementation or capture a focused trace;
- **security/capability:** inspect runtime enforcement, not prose alone;
- **failure cause:** collect an observation capable of distinguishing the named cause from a plausible alternative.

Prefer the cheapest source strong enough to settle the decision. Do not fetishize experiments when the contract is directly written in source, and do not fetishize documentation when live behavior is the property that matters.

When the fact will recur, encode the newly established knowledge in a contract test, type, invariant, or durable documentation at the rightful owner so future work does not pay the same discovery cost.

## Decision Branches
- **Owner is easy to inspect:** inspect it now; do not continue reasoning from memory.
- **Only runtime can settle the fact:** design the smallest falsifiable observation and record the result.
- **Several authoritative sources disagree:** preserve the disagreement and determine which source governs the actual boundary/version in use.
- **Verification is currently impossible:** keep the claim explicitly uncertain, prefer reversible design, and avoid hardening the guess into schema/API/state.
- **The premise is immaterial:** stop investigating it; not every uncertainty deserves payment.
- **The question is normative, not factual:** return to the rightful decision authority instead of searching for a source that cannot decide values.

## Common Wrong Fixes
- Search only for snippets that confirm the expected answer.
- Ask the same model/person again and treat repeated plausibility as independent evidence.
- Encode the assumption into a type/comment/abstraction before verifying it, making future readers inherit the guess as doctrine.
- Cite generic documentation while the installed version or host fork differs materially.
- Build a compatibility layer “just in case” for an unverified historical shape.
- Run a huge end-to-end experiment when five lines of canonical source settle the contract more directly.
- Read source that is adjacent to, but does not own, the claim and call that verification.

## Verification
The repaired decision should have a provenance chain short enough to state:

> We rely on X because owner/source Y establishes X under condition/version Z.

Where behavior rather than written contract is decisive, the observation must be reproducible enough to distinguish X from a realistic alternative.

Then check downstream work for assumptions derived from the old guess. Remove compatibility, branches, comments, or abstractions that existed only because the false premise was allowed to propagate.

Invariant:

> Load-bearing facts acquire provenance before they acquire architecture.

## Done When
Hypotheses are free to be bold and cheap.

Facts are expensive enough to deserve evidence.

The codebase no longer contains a confident structure whose foundation is “I thought it probably worked that way.”
