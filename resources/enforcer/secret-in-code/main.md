# secret-in-code — Main

## What To Do Now
Remove the sensitive value from repository content, revoke or rotate it as compromised, and replace it with a reference to the project’s approved secret-injection boundary. That boundary is who owns credential values; source history is not.

## Why This Matters
Deleting a credential from the latest file does not delete copies already created by version history, clones, caches, CI logs, or review systems. The only reliable response to exposure is to make the old authority useless.

## Repair Strategy
Rotate first where the exposure window still matters, remove the value from current source, use environment or secret-store injection, and add an appropriate gate against recurrence without storing real credentials in tests.

## Decision Branches
- If the value grants real authority, rotate or revoke immediately, then remove it from current source and history-facing artifacts.
- If tests need credentials, use fakes or an injected secret boundary—never committed live material.
- If the string is a documented public identifier or obvious placeholder, do not treat it as this defect.

## Common Wrong Fixes
- Do not merely rename, encode, or encrypt with a key stored beside the ciphertext.
- Do not delete only the current line and skip rotation.
- Do not move the secret to another committed file (docs, fixtures, scripts).
- Do not add `.gitignore` after the value was already committed and assume history is clean.

## Verification
The retired credential must no longer authenticate, current code must obtain the replacement through the intended secret boundary, and repository checks should contain no live sensitive material. The invariant is that possession of the source tree grants no secret authority.

## Done When
Possession of the source tree grants no secret authority, and any previously exposed credential has been invalidated rather than merely hidden.
