# lost-update — Enforcer

## Definition
A lost update occurs when concurrent writers derive new state from the same old version and later writes can overwrite earlier committed changes without detecting the conflict.

## Governing Principle
Read-modify-write contains an unstated premise: “the state I read is still the state I am modifying.” Concurrency invalidates that premise unless a protocol proves it. Locks, compare-and-swap, versions, or a single writer are not implementation decorations; they are mechanisms for preserving the causal link between the premise and the commit.

## Trigger When
Trigger when multiple writers can read the same mutable record/state, compute independent updates, and write back without version checking, serialization, or a merge law.

## Do Not Trigger When
- Do not trigger when ownership guarantees one writer, or the storage operation is an atomic commutative update whose semantics do not depend on a stale read.
- Do not trigger for append-only logs where concurrent writes are distinct facts and reads never overwrite.
- Do not trigger when production already serializes writers by construction and the overlapping path cannot exist.

## Distinguish From
shared-mutable-concurrency concerns coordination architecture broadly. optimistic-retry-assumption concerns unknown external effects. This rule is specifically stale-read overwrite of another writer’s accepted update. Tie-break: if the defect is a missing concurrency protocol in general, use shared-mutable-concurrency; if a committed update can be silently erased by a stale write, use this rule.

## Decision Procedure
For each read-modify-write ask what proves the read version is still current at commit time. If nothing does, the root-cause is a stale premise that can erase another writer’s accepted update: serialize ownership or include the version in an atomic compare-and-swap. Prefer this over a generic missing-lock smell when a committed change can vanish silently.

## Examples
- positive: Two workers load the same counter, each add one, both write the result; one increment vanishes.
- near-miss: An atomic increment or single-writer queue makes the update independent of a stale snapshot.
- counterexample: Each write carries the read version; CAS rejects the stale writer, who recomputes.

## Nudge
A write derived from version N is valid only against version N. Enforce that fact with a single writer, CAS/version check, or a true merge operation.
