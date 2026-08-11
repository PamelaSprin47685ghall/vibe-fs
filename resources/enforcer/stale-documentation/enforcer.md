# stale-documentation — Enforcer

## Definition
Documentation is stale when an authoritative specification, schema, example, or diagram still describes a contract that implementation no longer obeys.

## Governing Principle
Authoritative documentation and code are two representations of one agreement. If they can diverge independently, readers face an epistemic fork: trust the prose or trust observed behavior. The damage is larger than inconvenience because future design decisions may be logically correct relative to the wrong version of the contract. An authoritative document must therefore change atomically with the behavior it defines.

## Trigger When
Trigger when a code change alters a documented API, invariant, lifecycle, schema, command, or architecture while the owning documentation remains on the previous meaning.

## Do Not Trigger When
Do not trigger for clearly informal or historical notes that do not claim current authority, or when an established atomic generation/update process keeps the authoritative artifact synchronized in the same delivery.

## Distinguish From
comment-theater concerns prose substituting for structure. misleading-name concerns identifiers. This rule is disagreement between two surfaces that both claim to describe the current contract.

## Decision Procedure
Locate every authoritative representation of the changed contract. If any would lead a competent reader to a different conclusion than the new behavior, update it in the same change.

## Nudge
A contract cannot have two current versions. Change authoritative prose, schemas, examples, and diagrams with the behavior they govern so readers never have to guess which reality is newer.
