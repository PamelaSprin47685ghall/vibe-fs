# destructive-without-authorization — Main

## What To Do Now
Establish explicit authorization for the destructive operation and independently verify the concrete target immediately before executing it.

## Why This Matters
Deletion removes evidence and options. The expected cost of a mistaken destructive action is therefore dominated by rare catastrophic cases, not by average convenience. Safety comes from refusing to compress “may delete” and “this is the thing to delete” into one assumption.

## Repair Strategy
Resolve the target through authoritative identity, surface the destructive effect clearly, and require the appropriate authorization boundary. For automation, make the target and scope machine-checkable rather than relying on path similarity or operator memory.

## Wrong Fixes
Do not treat broad project access as permission for every destructive act. Do not rely on a guessed path, current directory, branch name, or stale listing to establish identity.

## Verification
Before execution, the displayed/checked identity must match the authorized target and scope. Afterward, verify that no sibling resource was affected.

## Done When
Every irreversible change is traceable to explicit authority over a precisely identified target, with no destructive step depending on guesswork.
