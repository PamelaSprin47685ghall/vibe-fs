# todo-bomb — Main

## What To Do Now
Implement the missing behavior now, or refuse to ship the incomplete path (feature flag off, API not exposed, hard error at boundary with no false success).

## Repair Strategy
Inventory TODO/FIXME/unimplemented on the change path. Close each that correctness depends on. Delete false success stubs.

## Decision Branches
If scope truly excludes the path, make it unreachable and document the out-of-scope edge—do not leave a soft TODO on a live branch.

## Wrong Fixes
throw new Error("TODO") on a production branch that can be hit. Returning empty success from unimplemented handlers. "Fix later" on security or durability paths.

## Verification
No live path depends on placeholder behavior; tests cover the finished branch or prove it is unreachable.

## Done When
Required behavior is implemented or the incomplete change is not shippable.

## Scope and Authority
Correctness-critical paths in delivered code.
