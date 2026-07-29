# Changelog

## 0.5.0-rc.1 — docs freeze / RC development

Phase 0 documentation freeze for the 0.5.0 track. Product law now points at infinite AABB agent-pair fallback and explicit `fast-*`/`deep-*` Managed Agents. Runtime code, tests, and evidence folders are still catching up under RC development (`0.5.0-rc.1` → `0.5.0-rc.N` → `0.5.0`).

### Breaking changes

- All Wanxiangshu agents now require explicit `fast-*` or `deep-*` names.
- Unprefixed agent names are no longer supported.
- `build` and `plan` aliases have been removed.
- Agent-to-model bindings are read exclusively from OpenCode `opencode.json`.
- All Wanxiangshu model environment variables have been removed.
- Wanxiangshu no longer persists or overrides model IDs.
- Provider fallback now cycles A/A/B/B indefinitely.
- Provider retry count no longer kills a Logical Run.
- Blogger and Executor Agent are now internal fast/deep pairs.
- Pre-0.5.0 runtime journals are not supported.

### Docs freeze

- Normative freeze text: `next/Doc/SSOT.md` and `0.5.0.md` §23
- Migration steps: `MIGRATION.md`
- E2E gate retarget: `docs/E2E_RELEASE_GUIDE.md`
- Surgical AGENTS.md updates for Prompt Authority / Fallback Host contract

### Runtime fixes

- Fixed the reviewer `inspector` callback returning a curried function instead of a Promise, which OpenCode displayed as a red tool error.
- Fixed `coder`/`inspector` argument schemas leaking Zod internals into provider JSON, which made DevOps requests fail immediately with `invalid_request_error`; all custom tool arguments are now guarded against raw-schema mixing.
- Fixed ReviewGuard requiring a third `PERFECT` when `chat.message` accepted the confirmation before the asynchronous guard-send callback; the second distinct `PERFECT` now accepts the durable `ReviewConfirmation` identity.
- Fixed reviewers immediately repeating `PERFECT` inside the unconfirmed physical root: the first call now returns `AWAITING_CONFIRMATION`, requires ending the turn, and premature repeats are not journaled, so the second valid call releases the review.
- Flattened every OpenCode Session family: agents, one-shot workers, Blogger, Executor, and restored descendants all use the family root as physical `parentID`, making former grandchildren visible as direct children and root cancellation comprehensive.
- Fixed `join()` ownership isolation: only direct `AgentForked` children of the caller's ForkRuntime are restored into its mailbox; Companion/system associations, foreign completions, and PTYs owned by another runtime cannot be joined.

### Not in this RC cut

- No F#/JS production code delta yet for Managed Agent / infinite AABB
- No test rewrite or `docs/evidence/0.5.0/` folder yet

## 0.4.0 — final (private delivery)

Final 0.4.0 on sealed `0.4.0-rc.7` after observation exit.

### Scope

- Structured Agent DSL (OpenCode plugin): Manager/Orchestrator fork-join, static roles
- Prompt Authority / Logical Run / synthetic continuation fail-closed
- Companion Blogger + ActivePrefixEpoch prefix cache protection
- Session-wide formal A; `join.finalText` = session-wide A; B = Companion LatestB
- Logical-Run Fallback with provider-visible same-run A→A→B→B wire path
- ReviewGuard dual PERFECT with physical confirmation binding
- Inspector `{executor}` only; DevOps `fork-pty`; Process 3× estimate + large gate
- Orchestrator clean gate, worktree, serial publish, rebase, re-review, ff-only

### vs rc.7

- Version number only: `0.4.0-rc.7` → `0.4.0` (no product code delta)
- Observation exit recorded: `docs/evidence/0.4.0-rc.7/OBSERVATION-EXIT.md`

### Release gates

- Sealed RC: `docs/evidence/0.4.0-rc.7/` (281 tests-next, 29 gate-testkit, 18 canaries ×3)
- Provider AABB wire: `docs/evidence/0.4.0-rc.7/provider-aabb-trace.txt`
- Final version gate: `docs/evidence/0.4.0/` (second clean-checkout `test:release`)

### Known limits

- Host may double-fire the first user prompt under some APIError paths; Logical Run still A→A→B→B for four durable failures
- PluginFallbackRetry waits ~250ms after SessionIdle for host runner settle
- Package remains **private** under provisional commercial `LICENSE`

### Distribution

- `private: true`, `license: SEE LICENSE IN LICENSE`
- Deliver `wanxiangshu-0.4.0.tgz` only under written commercial agreement
- No public npm publish

### Migration

See `MIGRATION.md` and `docs/RELEASE-0.4.0.md`.

## 0.4.0-rc.7 — release candidate

Provider-visible same-run A/A/B/B wire path on top of sealed rc.6.

### Changes

- Debounced PluginFallbackRetry after SessionIdle (host runner settle)
- Non-retryable session.error → durable failure → EffectiveModel continue
- Mock reseal on fallback A→B system-prompt cold boundary
- Canary `fallback-aabb-trace` proves wire models `A → A → B → B` (no 5th attempt)

### Sealed

- Evidence: `docs/evidence/0.4.0-rc.7/`
- tests-next 281, gate-testkit 29, 18 canaries ×3, pack+import
- `provider-aabb-trace.txt`: models `[test-model, test-model, test-model-b, test-model-b]`

### Still blocking final 0.4.0

- RC observation period with event-driven exit
- Final `0.4.0` cut + second clean-checkout gate
- Private distribution only unless license decision changes

Scope freeze: `docs/SCOPE-0.4.0-FREEZE.md`.

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
