# translator-layer-bloat — Main

## What To Do Now
Delete pass-through wrappers. Call the real owner directly, or move a genuine policy into the layer so it earns its name.

## Repair Strategy
Trace call chains. Collapse identity forwards. Keep only layers that change type, trust, lifetime, or transaction boundaries.

## Decision Branches
If a layer exists for future growth with no present invariant, remove it until the invariant appears. If generated stubs forward, generate thinner bindings.

## Wrong Fixes
Manager of managers that only delegate. Renaming a forwarder instead of deleting it. Adding logging-only wrappers as "architecture".

## Verification
Call graphs show fewer hops; remaining layers have stated invariants tested at that boundary.

## Done When
No load-bearing name exists solely to forward; surviving layers own a real boundary.

## Scope and Authority
Structural layering in application code.
