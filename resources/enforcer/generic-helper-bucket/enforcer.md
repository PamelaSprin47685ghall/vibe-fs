# generic-helper-bucket — Enforcer

## Definition
A generic helper bucket is a module whose organizing principle is **absence of ownership**.

Names such as `utils`, `helpers`, `common`, `misc`, or `core` are not inherently wrong. The rule triggers when the module answers “why does this code live here?” with “because nowhere else wanted it.” The bucket then becomes an architectural junk drawer: unrelated behavior accumulates because the name has no exclusion rule.

## Governing Principle
A module needs a reason to say **no**.

Good modules are organized by a concept, boundary, invariant, or capability. That organizing principle lets maintainers predict where code belongs and what dependencies are legitimate.

A generic bucket has no such force. String formatting arrives, then path logic, then retry policy, then JSON helpers, then database glue, then a domain-specific predicate. Because every new orphan fits the name, the module's dependency fan-in grows while its semantic cohesion falls.

The real defect is not aesthetic naming. It is that ownership decisions have been deferred and replaced with a location.

## Trigger When
Trigger when a generic module accumulates unrelated responsibilities or dependencies because no semantic owner was chosen. Common signs:

- functions in the file would naturally belong to different domains/boundaries if named honestly;
- adding a helper often requires importing a new unrelated dependency into the bucket;
- many modules depend on the bucket, so changing one helper risks broad rebuild/coupling despite unrelated semantics;
- helpers call back into higher-level domain modules, creating cycles or inverted dependencies;
- tests for the bucket are a grab bag with no shared invariant;
- names are generic enough that maintainers routinely ask “where should I put this?” and the answer is “utils”; 
- supposedly reusable helpers encode one product/domain convention but are called generic;
- the bucket becomes the easiest place to hide code during rushed changes, migrations, or AI-generated patches.

## Do Not Trigger When
- The module represents a genuinely narrow technical concept shared across domains: e.g. UTF-8 byte operations, stable hashing primitives, pure collection combinators, or a well-defined platform shim.
- A `common` package is itself a deliberately versioned public product with explicit scope and ownership.
- A small local helper file exists next to one owner and contains only implementation details of that owner.
- The generic name is unfortunate but contents have one clear invariant and dependency boundary; rename may be enough.
- Shared code is duplicated deliberately because extracting it would create a worse semantic dependency between otherwise independent domains.

## Distinguish From
`god-module` owns several unrelated sovereignties, often with substantial policy/effects. A generic helper bucket may begin smaller and mostly mechanical; its defining pathology is ownerless accumulation.

`dependency-bloat` concerns unnecessary external packages. The helper bucket may cause internal dependency bloat even with no new package.

`duplicated-control-flow` can tempt a generic extraction. Do not fix duplication by creating a bucket unless the extracted logic has a real concept that owns it.

## Decision Procedure
For every exported helper, finish this sentence:

> This operation belongs to ___ because ___ would be incomplete or inconsistent without it.

If answers point to different domains/boundaries, the bucket is not a coherent owner.

Then ask what rule prevents the next unrelated helper from being added. If no semantic exclusion rule exists, the module is structurally predisposed to sprawl.

## Examples
- positive: `utils.ts` contains currency rounding, HTTP retry, SQL escaping, feature-flag parsing, slug generation, and account authorization helpers.
- positive: `common.fs` imports both domain types and infrastructure SDKs, so nearly every layer depends on it and it depends back on several layers.
- positive: `helpers.js` grows after every incident because “temporary” recovery functions have nowhere else to go.
- near-miss: `Utf8.fs` contains only byte/string conversion with no domain knowledge and is shared by several adapters.
- counterexample: two domains intentionally keep similar local formatting logic because a shared abstraction would create false coupling.

## Nudge
A junk drawer is convenient because every orphan fits.

Architecture begins when code has somewhere it **belongs**, not merely somewhere it can be put.
