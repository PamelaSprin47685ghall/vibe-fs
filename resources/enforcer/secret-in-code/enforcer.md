# secret-in-code — Enforcer

## Definition
A password, token, private key, credential, or other sensitive value is embedded in source, fixtures, logs, prompts, or committed configuration.

## Trigger When
A password, token, private key, credential, or sensitive value is embedded in source, fixtures, logs, prompts, or committed configuration.

## Do Not Trigger When
Do not fire for clearly fake placeholders in docs that cannot authenticate anything, or for public client IDs that are not secrets by design.

## Distinguish From
debug-print-left may leak data accidentally; this tip is embedding or committing credentials as material.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Sensitive material appears in code or committed data. Remove and rotate it, then use the approved secret boundary.

## Examples
### Positive
A password, token, private key, credential, or sensitive value is embedded in source, fixtures, logs, prompts, or committed configuration.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire for clearly fake placeholders in docs that cannot authenticate anything, or for public client IDs that are not secrets by design.
