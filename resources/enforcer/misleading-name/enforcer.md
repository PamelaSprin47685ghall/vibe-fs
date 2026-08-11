# misleading-name — Enforcer

## Definition
A name is misleading when it claims a stronger guarantee, different owner, broader scope, or different domain meaning than the implementation actually provides.

## Governing Principle
Names are executable assumptions in human reasoning. Readers use them to skip re-reading implementation; that is precisely why a false name is more dangerous than a vague one. Each encounter imports a wrong premise into downstream reasoning, so the cost compounds with reuse. A name must therefore describe the contract, not the aspiration or history of the code.

## Trigger When
Trigger when an identifier suggests atomicity, durability, uniqueness, authorization, ownership, completion, scope, or domain identity that the implementation does not guarantee.

## Do Not Trigger When
- Do not trigger for concise names whose guarantee is clear from the immediate type/context and does not plausibly imply more than reality provides.
- Do not trigger for merely generic names (`handler`, `manager`) that assert no false guarantee—weak, not a lie.
- Do not trigger when a qualifier already cancels the overclaim (`InMemoryCache` versus a type named `DurableStore`).

## Distinguish From
domain-language-drift concerns inconsistent vocabulary across the system. abbreviation-anxiety concerns decoding cost. This rule is a semantic lie in a specific name. Tie-break: if the same concept has several names, use domain-language-drift; if one name claims a false contract, use this rule.

## Decision Procedure
Write the strongest reasonable claim a reader would infer from the name, then compare it with the actual contract. If the inferred claim is stronger or different, the root-cause is a name broadcasting a false contract: rename the concept or strengthen the implementation to make the claim true. Prefer this over domain-language-drift when one identifier lies rather than the vocabulary merely splitting.

## Examples
- positive: `commitDurable` writes only to a process-local map and never fsyncs.
- near-miss: `tryReserve` makes the attempt explicit; failure is part of the name’s contract.
- counterexample: `appendToJournal` appends to the journal and the tests prove that durability claim.

## Nudge
A name is a promise readers rely on without checking. Make that promise exactly as strong—and no stronger—than the implementation’s real contract.
