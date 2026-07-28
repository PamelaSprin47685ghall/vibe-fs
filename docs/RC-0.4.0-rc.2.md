# 0.4.0-rc.2 — historical development record (not a release)

| Field | Value |
|---|---|
| **Version marker** | `0.4.0-rc.2` (superseded by `0.4.0-rc.3-dev`) |
| **Date** | 2026-07-28 |
| **Status** | Historical development snapshot. **Not** a release candidate. |

## Why this is not RC

This document previously claimed a green release gate. That claim is withdrawn: there was no matching clean-checkout evidence package, and later audits found conflicting product semantics still open.

Current development marker: `0.4.0-rc.3-dev`.

The first real release candidate for this track should be:

```text
0.4.0-rc.3
```

Do not reuse the `rc.2` label after it already published a premature green claim.

## What did land in that development window

Useful progress, not ship criteria:

- Prompt Authority types, journal facts, UnknownOrigin fail-closed baseline
- tool-calls → TurnInProgress / length → TurnNeedsContinuation
- Reconciler chooses last assistant after current root
- Role prompts and parent LatestB injection for new children
- Busy nudge beginning to use continuation metadata
- Event-stagger canary runner shape

## Open blockers retained after that window

1. Prompt Authority does not fully replace `sessionRoles` / session-level model inference.
2. Agent completion is still collapsed to `Result<string,string>` / F# Result arrays at `join()`.
3. Manager → Inspector/Coder/Reviewer real Host loop is incomplete.
4. Companion eligibility still has production fallbacks outside `ActiveLogicalRun`.
5. Review second PERFECT still leans on confirmation text markers.
6. Fallback docs/code mixed Logical-Run attempts with cross-root Side B inheritance.

## Correct Fallback / Authority rules going forward

```text
Fallback belongs to a Logical Run.
New Authority Root creates a new Fallback epoch (Failures=0, Side=A).
Explicit human model always wins.
Continuation never creates a new epoch.
B attempt EffectiveModel never becomes the next human root default model.
Omit-model human root inherits LastAuthorityProfile.BaseModel only.
Companion eligibility reads only ActiveLogicalRun.Profile.Agent.
```

## Do not claim

- release gate green
- 16 × 3 all pass as ship evidence
- Fallback fully solved
- ready to promote to `0.4.0`

Those claims require a later `0.4.0-rc.3` evidence package: commit SHA, dependency lock hash, three-round logs, provider traces, journal, clean tarball install.
