# secret-in-code — Enforcer

## Definition
A secret is in code when authentication material is embedded in source, fixtures, logs, prompts, or committed configuration, placing confidential authority inside a medium designed for replication and history.

## Governing Principle
Source control optimizes for copying, retention, review, and recovery—the exact opposite properties required by secrets. Once a credential enters repository history, deletion of the visible line does not restore secrecy because clones, caches, diffs, and logs may already preserve it. The security boundary is therefore temporal: exposure requires revocation, not cosmetic removal.

## Trigger When
Trigger when passwords, API tokens, private keys, signing secrets, session credentials, or equivalent sensitive material appears in committed or broadly replicated project content.

## Do Not Trigger When
Do not trigger for unmistakably nonfunctional placeholders or identifiers explicitly public by protocol design.

## Distinguish From
debug-print-left may accidentally expose sensitive values through temporary diagnostics. This rule concerns confidential authority being stored in a replicated code/document medium at all.

## Decision Procedure
Determine whether possession of the value grants authority or reveals protected material. If yes, assume repository exposure compromises it, remove the value, rotate/revoke it, and replace the source with the approved secret reference boundary.

## Nudge
A repository is a distribution system, not a vault. Remove exposed credentials, rotate them, and inject secrets only through the boundary designed to keep authority out of source history.
