# timeout-inflated-to-pass — Main

## What To Do Now
Revert the inflated timeout. Find the missing signal, deadlock, leak, or overload. Fix the cause; set timeouts from real budgets afterward.

## Repair Strategy
Capture traces under the hang. Check for missing await signals, lock cycles, unbounded work, and leaked resources. Restore a tight timeout that fails closed.

## Decision Branches
If load genuinely needs more time, document the measurement and SLO—distinct from greenwashing a flake.

## Wrong Fixes
timeout: 60000 because 5000 failed. Triple retries with triple timeouts until CI is quiet. Disabling timeouts entirely.

## Verification
With a correct fix, a reasonable timeout passes reliably; breaking the signal fails fast again.

## Done When
Timeouts reflect real budgets; hangs are fixed at the causal root.

## Scope and Authority
Tests and operational timeouts adjusted to hide failures.
