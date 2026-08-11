# blind-edit — Enforcer

## Definition
A blind edit changes a representation before establishing which contract owns the behavior and which path turns that representation into an observable result.

## Governing Principle
Source code is evidence of a system, not the system itself. A line acquires meaning from the invariants above it and the callers below it. Editing the first plausible location confuses textual proximity with causal ownership. Such fixes often remove a symptom while preserving—or worsening—the mechanism that produced it.

## Trigger When
Trigger when implementation changes begin before locating the owner, reading the surrounding contract, and tracing the affected call or data path far enough to explain the defect.

## Do Not Trigger When
- The change is truly local, and ownership plus behavior are already explicit and verified by the current context.
- The owning contract and causal path were just mapped, and the edit is the first place reality diverges from that contract.
- A mechanical rename or format change does not claim to repair a behavioral defect.
- The failing test already names the owning boundary and the edit is confined to that boundary.

## Distinguish From
`guess-based-fix` concerns trial-and-error remedies. `guessed-not-verified` concerns unsupported factual claims. This rule is earlier: mutation begins before the causal territory has been mapped. Tie-break: if the first code change happens without an ownership map, this rule owns the case even if the eventual patch might later prove correct.

## Decision Procedure
1. Name the observed behavior.
2. Locate the contract that owns it.
3. Trace the path from input to observation.
4. Identify the first violated invariant, then edit there.

## Examples
- positive: a failing UI symptom is patched in the nearest renderer before tracing which service owns the wrong fact.
- near-miss: the owner, contract, and call path are already in hand, and the edit is at the first violated invariant.
- counterexample: stop, name the observed failure, locate the owning contract, then change only that owner.

## Nudge
Do not edit where the symptom is loudest. Find the owning invariant and the causal path first; change the first place where reality diverges from the contract.
