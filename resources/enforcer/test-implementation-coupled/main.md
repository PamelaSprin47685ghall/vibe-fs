# test-implementation-coupled — Main

## What To Do Now
Rewrite assertions against observable outputs, state transitions, and public APIs. Drop spies on private helpers unless the interaction is the contract.

## Repair Strategy
For each assertion, ask what user-visible or API-visible fact it protects. Replace internal white-box checks with black-box outcomes.

## Decision Branches
If only internals are testable, the design may lack a seam—introduce a pure function or port rather than testing privates.

## Wrong Fixes
Expecting exact call order into private methods. Snapshotting full private object graphs. Failing tests after a pure rename of helpers.

## Verification
Refactor internals without changing behavior; tests stay green. Break the public contract; tests go red.

## Done When
Tests encode durable behavior; incidental structure can change freely.

## Scope and Authority
Unit and collaboration tests. Not characterization tests explicitly marked as temporary during a rewrite.
