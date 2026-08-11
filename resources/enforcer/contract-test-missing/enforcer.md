# contract-test-missing — Enforcer

## Definition
A contract test is missing when a boundary owned by another runtime, process, language, store, provider, or plugin changes without a proof of the observable agreement at that boundary.

## Governing Principle
A boundary is where independent implementations meet. Internal tests can prove each side is self-consistent while both sides disagree about bytes, ordering, identity, defaults, failure, or lifetime. The contract is therefore not the code on either side; it is the intersection of what one emits and the other accepts.

## Trigger When
Trigger when a Host, provider, storage, process, network, plugin, wire, or language boundary changes and no test exercises the exact supported input/output and failure semantics.

## Do Not Trigger When
Do not trigger when an existing contract-level test already covers the changed behavior through the same boundary and would fail on incompatibility.

## Distinguish From
behavioral-boundary-untested concerns a public boundary within the product. canary-skipped concerns undocumented behavior requiring the real environment. This rule concerns a declared inter-system contract.

## Decision Procedure
Name the producer, consumer, exchanged representation, identity rules, and failure semantics. Add a test at the narrowest point where both sides’ assumptions become observable.

## Nudge
Two correct components can still disagree. Test the agreement itself: exact shape, identity, ordering, and failure semantics at the real contract boundary.
