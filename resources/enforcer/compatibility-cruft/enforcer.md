# compatibility-cruft — Enforcer

## Definition
Compatibility cruft is a second way of representing, calling, configuring, storing, or executing the same capability that survives without a concrete external obligation still requiring it.

It is architecture governed by an unnamed ghost consumer.

## Governing Principle
Compatibility is a debt instrument. It can be worth taking on, but the debt must have a creditor.

A real compatibility obligation names who would break, which old contract they still possess, how long overlap must exist, and what event permits removal. Without those facts, “backward compatibility” becomes a moral incantation that prevents simplification indefinitely.

The cost is not only extra code. Two live paths create two ontologies, two sets of tests, two failure modes, routing rules, migration ambiguity, and a permanent question in every future change: “which world am I changing?”

## Trigger When
Trigger when legacy aliases/adapters/formats/paths remain live primarily from unspecified fear rather than a named supported contract. Common forms:

- old and new API/tool names both remain after every repository-owned caller migrated;
- an old config key is accepted forever because “someone might still have it,” with no supported-version policy;
- dual read/write formats persist after migration, even though no durable old data or external producer still exists;
- compatibility adapters route between two internal models that are both repository-owned;
- a deprecated branch has no telemetry, consumer list, removal date/condition, or version boundary;
- “temporary” normalization accepts malformed historical shapes never observed in real data;
- new code must update both legacy and current representations to keep them synchronized;
- a clean-break product decision is undermined by hidden decode/alias fallbacks that preserve the old provider-facing ontology anyway.

## Do Not Trigger When
- A named external consumer/version still requires the old contract and removing it would violate a supported promise.
- Historical durable data genuinely requires old decode for recovery, while new writes use only the current format and legacy decode is quarantined at persistence ingress.
- A migration has an explicit overlap window, telemetry/consumer tracking, and concrete removal criterion.
- Standards/protocols require accepting multiple versions or representations as part of the actual current contract.
- Compatibility is itself the product requirement, not an implementation superstition.

## Distinguish From
`half-finished-refactor` concerns old/new ownership models both remaining authoritative inside the system. `compatibility-cruft` can exist even when ownership is clear, simply because obsolete external shapes are still accepted.

`legacy-cruft-retained` is broader historical debris. This rule specifically targets duplicate interfaces/representations justified as compatibility.

`guessed-migration` may create unnecessary compatibility because historical data was never inspected. Use that rule when the central failure is inventing a migration target; use this one when the duplicate path remains without a creditor.

## Decision Procedure
For each legacy path, demand four answers:

1. **Consumer:** who still uses it?
2. **Contract:** what supported promise requires it?
3. **Overlap:** why must old and new be live simultaneously?
4. **Exit:** what observable condition permits deletion?

If nobody can answer #1 or #2 concretely, the compatibility path is not protecting a contract. It is protecting anxiety.

If #1–#3 are real but #4 is missing, the migration has no mechanism for ever finishing.

## Examples
- positive: both `oldTool()` and `newTool()` remain indefinitely after all first-party callers moved; no external API exists.
- positive: decoder accepts three speculative legacy JSON shapes copied from old code comments, but no persisted sample or supported version contains them.
- positive: every write updates both v1 and v2 tables “for rollback safety” six months after rollback became impossible.
- near-miss: a public API supports clients on v1 for six months; usage telemetry and published deprecation date define the overlap.
- counterexample: current writes are v2-only, but recovery can still read real v1 durable records until the retention horizon expires.

## Nudge
Compatibility without a named consumer is fear with an API.

Name the creditor, name the exit, or delete the debt.
