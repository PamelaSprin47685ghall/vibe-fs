# stale-documentation — Enforcer

## Definition
Documentation is stale when an authoritative specification, schema, example, or diagram still describes a contract that implementation no longer obeys.

## Governing Principle
Authoritative documentation and code are two representations of one agreement. If they can diverge independently, readers face an epistemic fork: trust the prose or trust observed behavior. The damage is larger than inconvenience because future design decisions may be logically correct relative to the wrong version of the contract. An authoritative document must therefore change atomically with the behavior it defines.

## Trigger When
Trigger when a code change alters a documented API, invariant, lifecycle, schema, command, or architecture while the owning documentation remains on the previous meaning.

## Do Not Trigger When
- The prose is clearly informal or historical and does not claim current authority.
- An established atomic generation/update process keeps the authoritative artifact synchronized in the same delivery.
- Only implementation comments that are not the owning contract drifted (prefer comment-theater if they fake structure).
- The change did not alter any documented contract, only private internals.

## Distinguish From
comment-theater concerns prose substituting for structure. misleading-name concerns identifiers. This rule is disagreement between two surfaces that both claim to describe the current contract. Tie-break: fire here when authoritative docs and behavior diverge; fire comment-theater when comments pretend to be the design; fire misleading-name when the wrong surface is an identifier, not a spec.

## Decision Procedure
Locate every authoritative representation of the changed contract. If any would lead a competent reader to a different conclusion than the new behavior, update it in the same change.

## Examples
- positive: a command’s flags change in code while `docs/how` still documents the old CLI as current.
- near-miss: a dated `history.md` describes the old API and is labeled historical, not current.
- counterexample: schema, examples, and how-docs update in the same delivery as the behavior change.

## Nudge
A contract cannot have two current versions. Change authoritative prose, schemas, examples, and diagrams with the behavior they govern so readers never have to guess which reality is newer.
