# blind-edit — Lesson

## Opinion

### Definition

A blind edit changes a representation before establishing which contract owns the behavior and which path turns that representation into an observable result. The root-cause is that mutation starts at the loudest symptom before the owning contract and causal path are mapped, so the patch changes a witness rather than the violated invariant.

### Governing Principle

Source code is evidence of a system, not the system itself. A line acquires meaning from the invariants above it and the callers below it. Editing the first plausible location confuses textual proximity with causal ownership. Such fixes often remove a symptom while preserving—or worsening—the mechanism that produced it.

### Trigger When

Trigger when implementation changes begin before locating the owner, reading the surrounding contract, and tracing the affected call or data path far enough to explain the defect.

### Do Not Trigger When

- The change is truly local, and ownership plus behavior are already explicit and verified by the current context.
- The owning contract and causal path were just mapped, and the edit is the first place reality diverges from that contract.
- A mechanical rename or format change does not claim to repair a behavioral defect.
- The failing test already names the owning boundary and the edit is confined to that boundary.

### Distinguish From

`guess-based-fix` concerns trial-and-error remedies. `guessed-not-verified` concerns unsupported factual claims. This rule is earlier: mutation begins before the causal territory has been mapped. Tie-break: if the first code change happens without an ownership map, this rule owns the case even if the eventual patch might later prove correct.

### Decision Procedure

1. Name the observed behavior.
2. Locate the contract that owns it.
3. Trace the path from input to observation.
4. Identify the first violated invariant, then edit there.

### Examples

- positive: a failing UI symptom is patched in the nearest renderer before tracing which service owns the wrong fact.
- near-miss: the owner, contract, and call path are already in hand, and the edit is at the first violated invariant.
- counterexample: stop, name the observed failure, locate the owning contract, then change only that owner.

## Advice

### What To Do Now

Stop changing the visible symptom. Read the owner, the contract around it, and the caller/data path until you can state a causal explanation for the defect. The contract at the first violated invariant is who owns the fix; downstream renderers and guards are not.

### Why This Matters

The cheapest edit is not the fewest changed lines; it is the edit that restores the violated invariant without creating a second story elsewhere. Without ownership, every patch is a wager that the chosen file happens to be the source rather than merely a witness.

### Repair Strategy

Work backward from the observable failure to the governing contract, then forward through the implementation. Preserve known-correct structure and modify the smallest point that owns the wrong fact or transition.

### Decision Branches

- If the owner and causal path are unknown, stop editing and map them first.
- If the owner is known and the first violated invariant is identified, edit there and nowhere downstream.
- If a downstream guard would hide the symptom, reject it; repair the source of the wrong fact.

### Common Wrong Fixes

- Do not add downstream guards, adapters, or fallbacks because they make the symptom disappear.
- Do not infer an API or lifecycle from naming alone.
- Do not spray similar patches across every file that mentions the symptom.
- Do not treat a green local tweak as proof of ownership without a causal account.

### Verification

The regression test should fail at the original behavior, pass after the owner-level repair, and make sense from the contract without knowledge of the patch. The invariant is that the edit restores the owning contract rather than silencing a witness of the violation.

### Done When

You can explain why the defect occurred, why this boundary owns the correction, and why the same mechanism cannot silently reappear downstream.

### Nudge

Do not edit where the symptom is loudest. Find the owning invariant and the causal path first; change the first place where reality diverges from the contract.
