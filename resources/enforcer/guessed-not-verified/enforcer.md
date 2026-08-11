# guessed-not-verified — Enforcer

## Definition
A claim is guessed when material behavior, API shape, file content, Host semantics, or failure cause is asserted from expectation rather than inspected from its authoritative source. The root-cause is that an unverified premise is admitted as fact, so later deductions inherit false certainty from expectation, memory, or naming convention rather than from the authority that owns the claim.

## Governing Principle
Engineering reasoning is conditional: if premise P is false, every flawless deduction from P is still wrong. Material premises therefore deserve stronger evidence in proportion to the cost of acting on them. Source code, actual files, direct experiments, and documented contracts outrank naming conventions, memory, and what a tool “usually” does.

## Trigger When
Trigger when a decision depends on an unverified factual claim that could be settled by reading the owner or running a focused check.

## Do Not Trigger When
- Do not trigger for explicitly labeled hypotheses used to guide investigation before evidence is available, provided they are not treated as established facts.
- Do not trigger for a documented contract already read in this session when the claim is exactly that contract.
- Do not trigger for aesthetic or naming preferences that do not load-bear on behavior.

## Distinguish From
guess-based-fix acts speculatively until symptoms move. blind-edit mutates before understanding ownership. This rule is epistemic: an uncertain premise is being smuggled into reasoning as fact. Tie-break: if the false certainty is a premise, use this rule; if edits already hunt a passing state, use guess-based-fix.

## Decision Procedure
Identify the load-bearing claim, then identify the authority capable of settling it. Read or test that source before letting downstream reasoning depend on the claim.

## Examples
- positive: “This Host API returns null on miss” is treated as fact from memory; the code path is never read.
- near-miss: A labeled hypothesis guides a grep/experiment, then the result replaces the guess.
- counterexample: The owning source and contract were inspected; the claim cites that provenance.

## Nudge
Do not spend deductions on an unverified premise. Inspect the authoritative source or run the smallest experiment that can make the claim true or false.
