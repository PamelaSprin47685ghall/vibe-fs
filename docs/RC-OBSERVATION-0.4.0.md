# 0.4.0 RC Observation Criteria

Event-driven exit (no fixed calendar length). Applies to sealed `0.4.0-rc.5` or a later sealed RC.

## Scope to exercise

- Ordinary Coder work
- Manager multi-child fork/join
- DevOps PTY long-running tasks
- Reviewer REVISE + dual PERFECT
- Provider retry
- OpenCode restart
- Orchestrator multi-worktree publish
- Companion context replacement
- User cancel and parent abort

## Must not observe

- Authority mismatch
- Logical Run cross-talk
- Duplicate completion
- Permanent hang on `join`
- Fifth provider request in one Logical Run Fallback epoch
- Blogger mutating frozen prefix
- Review witness misbind
- Duplicate ff
- Git fail-open
- PID / PTY / port / worktree / lock leak after dispose
- Restart treating unproven Busy children as still running
- Missing prompts or package assets after install

## Change rules during observation

| Change type | Action |
|---|---|
| Docs / evidence only | Stay on same RC |
| Test-only / diagnostic, no product behavior change | Re-run full clean gate |
| Any production code change | Cut next RC and restart observation |
| Frozen semantic change | Restart observation and re-freeze scope |

## Exit

- Immutable sealed RC commit with full evidence
- Full release gate green on that commit
- No open P0/P1 from real observation
- Every P2 has accept / defer / fix decision
- Final cut needs no production code change
