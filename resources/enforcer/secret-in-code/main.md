# secret-in-code — Main

## What To Do Now
Remove the sensitive value from repository content, revoke or rotate it as compromised, and replace it with a reference to the project’s approved secret-injection boundary.

## Why This Matters
Deleting a credential from the latest file does not delete copies already created by version history, clones, caches, CI logs, or review systems. The only reliable response to exposure is to make the old authority useless.

## Repair Strategy
Rotate first where continued exposure window matters, remove the value from current source, use environment/secret-store injection, and add an appropriate gate against recurrence without storing real credentials in tests.

## Wrong Fixes
Do not merely rename, encode, encrypt with a key stored beside it, or delete only the current line. Obfuscation changes appearance, not authority.

## Verification
The retired credential must no longer authenticate, current code must obtain the replacement through the intended secret boundary, and repository checks should contain no live sensitive material.

## Done When
Possession of the source tree grants no secret authority, and any previously exposed credential has been invalidated rather than merely hidden.
