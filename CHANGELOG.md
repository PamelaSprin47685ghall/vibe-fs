# Changelog

## 0.4.0-rc.4 — release candidate

Amended from a premature `0.4.0` final cut. Current ship marker is **`0.4.0-rc.4`**, not final `0.4.0`.

### Gates

- clean `npm ci` rebuild
- tests-next green
- manager-tools + gate-testkit green
- `CANARY_REPEAT=3` 17-canary staggered suite green
- pack + empty-dir install green

### Still blocking final 0.4.0

- Observation period on this RC
- Optional real-provider same-run A/A/B/B request traces
- Commercial distribution policy if publishing outside private license terms
- Final promotion requires a later clean-checkout gate on a true `0.4.0` version cut

## 0.4.0-rc.3 — release candidate after PR0–PR11

First real release candidate for the 0.4.0 track. Cut only after clean-checkout `npm ci` + `test:release` (three-round canaries) and empty-dir install evidence.

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

### Still blocking final 0.4.0

- Optional: same-run provider request traces proving strict A/A/B/B under real provider retries
- Commercial distribution license/private package policy review
- Final `0.4.0` only after RC observation + second clean-checkout gate on this RC tag

## 0.4.0-rc.2 / 0.4.0-rc.1 — historical development markers

Earlier labels were development markers only. Do not treat `docs/RC-0.4.0-rc.2.md` as a green release gate.

## Distribution policy

The package remains private (`private: true`) under the provisional commercial license in `LICENSE`. It must not be published to the public npm registry without a signed commercial agreement and a real release gate.
