# secret-in-code — Enforcer

## Definition
A secret is in code when authentication material is embedded in source, fixtures, logs, prompts, or committed configuration, placing confidential authority inside a medium designed for replication and history. The root-cause is that a credential is stored where copies are the point, so deletion of the visible line cannot restore secrecy.

## Governing Principle
Source control optimizes for copying, retention, review, and recovery—the opposite properties required by secrets. Once a credential enters repository history, clones, caches, diffs, and logs may already preserve it. The security boundary is therefore temporal: exposure requires revocation, not cosmetic removal.

## Trigger When
Trigger when passwords, API tokens, private keys, signing secrets, session credentials, or equivalent sensitive material appears in committed or broadly replicated project content.

## Do Not Trigger When
- The value is an unmistakably nonfunctional placeholder (`CHANGEME`, `dummy`, a documented fake).
- The identifier is explicitly public by protocol design (an OAuth client id that is not a secret, a well-known JWKS URL).
- A test uses a locally generated ephemeral key that never grants real authority and is not a production credential.
- The file references a secret store or environment name without containing the secret value.

## Distinguish From
`debug-print-left` may accidentally expose values through temporary diagnostics. `leftover-scaffolding` may retain fixtures that happen to include credentials. This rule concerns confidential authority being stored in a replicated code or document medium at all. Tie-break: if a live secret is in replicated content, this rule owns the case even if a debug print put it there.

## Decision Procedure
1. Determine whether possession of the value grants authority or reveals protected material.
2. If yes, treat repository exposure as compromise.
3. Remove the value from current content.
4. Rotate or revoke the exposed authority and replace the source with the approved secret-reference boundary.

## Examples
- positive: a committed `.env` or test fixture contains a live API token that authenticates against a real service.
- near-miss: config lists `API_TOKEN_ENV=STRIPE_SECRET` and CI injects the value from a secret store.
- counterexample: docs show `sk_test_xxx` as a clearly fake placeholder that cannot authenticate.

## Nudge
A repository is a distribution system, not a vault. Remove exposed credentials, rotate them, and inject secrets only through the boundary designed to keep authority out of source history.
