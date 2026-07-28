# wanxiangshu 0.4.0 — private final release

| Field | Value |
|---|---|
| Version | `0.4.0` |
| Distribution | **Private** (`private: true`) |
| License | `SEE LICENSE IN LICENSE` (provisional commercial) |
| Base RC | sealed `0.4.0-rc.7` |
| Observation exit | `docs/evidence/0.4.0-rc.7/OBSERVATION-EXIT.md` |
| Final evidence | `docs/evidence/0.4.0/` |

## What ships

OpenCode Agent DSL plugin: structured Manager/Orchestrator programs, static role matrix, Prompt Authority / Logical Run, Companion projection with prefix-cache protection, Logical-Run Fallback with provider-visible same-run A→A→B→B, dual PERFECT Review, Inspector/Executor/PTY, Orchestrator worktree publish (ff-only).

## How to install (private)

```bash
npm install /absolute/path/to/wanxiangshu-0.4.0.tgz
# entry: next/OpenCode/Plugin.js (package main/exports)
```

## Gates

1. RC seal: clean gate on `0.4.0-rc.7` — `docs/evidence/0.4.0-rc.7/`
2. Observation exit on that RC — no open P0/P1
3. Final version cut (this release) — second clean `npm ci` + `test:release` + pack/install on version `0.4.0` — `docs/evidence/0.4.0/`

## Not included

- Public npm registry publish
- Prefetcher / Enforcer / Sidecar Supervisor (`future/FEATURE.md`)
- New Host hooks beyond existing OpenCode plugin surface

## Tag

`v0.4.0` must point at the commit that passed the final clean gate and carries `docs/evidence/0.4.0/`.
