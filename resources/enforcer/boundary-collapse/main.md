# boundary-collapse — Main

## What To Do Now
Re-establish an explicit interface between the contexts and reduce what crosses it to the smallest set of facts required by the consumer.

## Why This Matters
Architecture is a theory of permitted knowledge. When one context can depend on another’s internals, private implementation choices become public obligations by accident. The cost appears later as synchronized migrations, defensive adapters, and changes that cannot be reasoned about locally.

## Repair Strategy
Give each context ownership of its own model and invariants. Define the crossing in consumer-relevant terms, translate explicitly, and prevent internal imports or mutation mechanically where the language/build system permits.

## Wrong Fixes
Do not add a facade that forwards the same internals unchanged. Do not share a “common” mega-model to avoid translation. Both preserve the collapsed knowledge boundary while making it less visible.

## Verification
A change to one context’s internal representation should not require edits in the other unless the declared cross-boundary fact itself changed.

## Done When
Each context can evolve behind its contract, cross-boundary data has an explicit owner and meaning, and no caller relies on another context’s private representation or lifecycle.
