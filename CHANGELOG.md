# Changelog

## 0.4.0-rc.3-dev — development gate after PR0–PR9

This is still a **development marker**, not a distribution RC. It records that the 0.4.0 critical path through Manager dogfood + restart + Orchestrator publish has green evidence on this tree.

### Verified on this tree

- Prompt Authority single service, stable LogicalRunId, AgentOwnerRoot two-phase send
- Typed join/list JSON (`AgentCompletionOutcome`), no F# Result arrays at join
- Manager full-loop Host canary
- Companion eligibility from ActiveLogicalRun only; basic/cache/replacement canaries
- Review physical confirmation path + reviewer/manager canaries
- Fallback Logical-Run epoch + omit-model BaseModel inheritance + fallback canary
- Process/PTY stress canaries; Executor partial summary recovery
- Host/reviewer restart recovery; incomplete children marked Interrupted when Busy is unproven
- Orchestrator publish / restart-publish canaries
- `tests-next`: 275 passed
- `npm run test:e2e:p0` (17 canaries, 1 iteration): all passed
- `CANARY_REPEAT=3` staggered suite: all passed
- `npm pack ./build` + empty-dir install + `import('wanxiangshu')`: passed

### Frozen product rules

- Fallback belongs to a Logical Run. New Authority Root starts Failures=0, Side=A.
- Omit-model inherits LastAuthorityProfile.BaseModel only, never previous Side B.
- Companion eligibility reads only ActiveLogicalRun.Profile.Agent.
- Plugin user-shaped messages go through PromptAuthorityService.
- PTY completion is backend onExit only.
- Orchestrator publish is ff-only under integration lock with crash recovery facts.

### Still blocking a distributed 0.4.0-rc.3 / 0.4.0

- Commit dirty worktree (PR7–PR11 fixes currently uncommitted)
- Clean-checkout rebuild on that commit: `git clean -xfd && npm ci && npm run test:release`
- Version cutover `0.4.0-rc.3-dev` → `0.4.0-rc.3` only after clean-checkout green
- Optional: same-run provider request traces proving strict A/A/B/B under real provider retries
- Commercial distribution license/private package policy review
- Final `0.4.0` only after RC observation + second clean-checkout gate

## 0.4.0-rc.2 / 0.4.0-rc.1 — historical development markers

Earlier labels were development markers only. Do not treat `docs/RC-0.4.0-rc.2.md` as a green release gate.

## Distribution policy

The package remains private (`private: true`) under the provisional commercial license in `LICENSE`. It must not be published to the public npm registry without a signed commercial agreement and a real release gate.
