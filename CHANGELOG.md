# Changelog

## 0.4.0-rc.6 — release candidate

Production authority + host-signal fixes on top of sealed rc.5.

### Changes

- Host-stable prompt correlation: part metadata + unique pending-claim recovery
- ReviewConfirmation / Guard continuations no longer become HumanRoot
- AgentOwnerRoot accept when host strips top-level metadata
- Non-retryable `session.error` drives PluginFallbackRetry (AABB decision path)
- Dual-source signal subscription for session.error; ProviderError dedupe
- Optional `WANXIANGSHU_CHAT_MAX_RETRIES`; `chat.params` EffectiveModel hook

### Tests

- PromptAuthority chat.message correlation regressions
- resolveForSession same-run A/A/B/B before Dead

### Sealed

- Clean-checkout three-round gate evidence: `docs/evidence/0.4.0-rc.6/`
- tests-next 281, gate-testkit 29, CANARY_REPEAT=3 17 canaries, pack + empty-dir import

### Still blocking final 0.4.0

- RC observation period with event-driven exit (no open P0/P1)
- **Blocking**: provider-visible same-run A/A/B/B **request** trajectory under host re-prompt
- Private distribution only unless a separate license decision replaces `LICENSE`
- Final `0.4.0` cut requires a second clean-checkout gate on the real version number

Scope freeze: `docs/SCOPE-0.4.0-FREEZE.md`.

## 0.4.0-rc.5 — release candidate

Merge `worktree-0-branch` role system prompts and session-wide A semantics on top of the rc.4 clean gate lineage.

### Changes

- Role `AgentConfig.prompt` assets under `next/prompts/*.md` for all roles
- Session-wide formal assistant A accumulation (`TerminalSessionA`) used as join `finalText`
- A/B defined as full-session formal text / work log (not last-turn only)
- Manager tool-contract coverage for DevOps / role surfaces

### Sealed

- Clean-checkout three-round gate evidence: `docs/evidence/0.4.0-rc.5/`
- tests-next 277, gate-testkit 29, CANARY_REPEAT=3 17 canaries, pack + empty-dir import

### Still blocking final 0.4.0

- RC observation period with event-driven exit (no open P0/P1)
- **Blocking**: provider-visible same-run A/A/B/B request trajectory (`A → A → B → B`, no 5th request) plus omit-model BaseModel inheritance reset
- Private distribution only unless a separate license decision replaces `LICENSE`
- Final `0.4.0` cut requires a second clean-checkout gate on the real version number

Scope freeze: `docs/SCOPE-0.4.0-FREEZE.md`.

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
- **Blocking** provider-visible same-run A/A/B/B request trajectory (policy aligned 2026-07-28)
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

- **Blocking**: same-run provider request traces proving strict A/A/B/B under real provider retries
- Commercial distribution license/private package policy review (default remains private)
- Final `0.4.0` only after RC observation + second clean-checkout gate on this RC tag

## 0.4.0-rc.2 / 0.4.0-rc.1 — historical development markers

Earlier labels were development markers only. Do not treat `docs/RC-0.4.0-rc.2.md` as a green release gate.

## Distribution policy

The package remains private (`private: true`) under the provisional commercial license in `LICENSE`. It must not be published to the public npm registry without a signed commercial agreement and a real release gate.
