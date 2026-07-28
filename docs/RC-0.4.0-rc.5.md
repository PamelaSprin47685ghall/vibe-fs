# 0.4.0-rc.5 — release candidate

| Field | Value |
|---|---|
| Version | `0.4.0-rc.5` |
| Base | merge of `worktree-0-branch` into rc.4 lineage |
| Date | 2026-07-28 |
| Status | Release candidate. **Not** final `0.4.0`. |

## Included from worktree-0-branch

- Full role system prompts (`next/prompts/*.md`)
- Session-wide formal A accumulation for terminal completion / join `finalText`
- Session-wide B documented as companion LatestB work record

## Gate

Requires clean `npm ci` + `test:release` (`CANARY_REPEAT=3`) on this version cut before distribution claims.
