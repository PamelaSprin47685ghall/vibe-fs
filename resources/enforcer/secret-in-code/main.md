# secret-in-code — Main

## What To Do Now
Remove the secret from the tree and history if needed. Rotate the credential. Load secrets from the approved secret store or environment boundary at runtime.

## Repair Strategy
Scan the diff for tokens and keys. Replace with configuration references. Purge logs and fixtures. Rotate anything that may have been exposed.

## Decision Branches
If a test needs credentials, use ephemeral test doubles or a sealed secret fixture outside VCS. If a leak already shipped, rotate first, then clean code.

## Wrong Fixes
Commenting out the secret but leaving it in git history unrotated. Encoding secrets in Base64 and calling them safe. Printing secrets in "temporary" debug logs.

## Verification
Repo search shows no live secrets; runtime still authenticates via the secret boundary; rotation confirmed if exposure occurred.

## Done When
No sensitive values remain in source or committed artifacts; approved secret injection is used.

## Scope and Authority
Credentials and private material. Not public non-secret configuration.
