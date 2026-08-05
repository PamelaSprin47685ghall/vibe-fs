// tests/unit/verify/orchestrator-reuse-contract.test.mjs — PENDING 2 (PR B): sub-session reuse prompts.
//
// Layer 0 static resource contract. No dist import, no build needed: the prompt resource and the
// ForkTool source are the physical facts under test.
//
// RED by construction: the Orchestrator tool surface is exactly [fork-manager, join] (AGENT-006)
// with no `list` / `fork(existing_id)` reuse API, so reuse guidance must be executable without
// them. The first test requires the prompt to carry three executable rules: continue the same
// Manager job for same-goal follow-ups; fork-manager only for a truly independent target
// (different worktree / different lane); and an explicit denial of `list` / `fork-manager(existing_id)`
// / `reuse` tools — no invented APIs. The second and third tests are regression protection: the
// "continue the existing Manager job" clauses and ForkTool's manager description (reuse/nudge + tdd).

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const PROMPT_PATH = new URL('../../../resources/prompts/orchestrator-system.md', import.meta.url)
const FORK_TOOL_PATH = new URL(
  '../../../src/Wanxiangshu/Infrastructure/OpenCode/Tools/ForkTool.fs',
  import.meta.url,
)

const promptText = () => readFileSync(PROMPT_PATH, 'utf8')

// ── PENDING 2: executable reuse discipline (continue same job; no invented API) ──

test('ORCHESTRATOR_REUSE_001_prompt_carries_executable_same_job_continue_rules', () => {
  const prompt = promptText()
  const required = [
    // same delivery goal → continue the same Manager job (explicit directive).
    /continue the same Manager job|continuing the same Manager job/i,
    // the continuation target is the originating Manager session.
    /originating Manager session|originating Manager/i,
    // same-goal work stays in the same worktree.
    /same worktree/i,
    // new fork-manager is reserved for truly independent goals.
    /truly independent/i,
    // independence is defined as a different worktree / different lane.
    /different worktree|different lane/i,
    // the tool surface is exactly [fork-manager, join]: no list tool exists.
    /exactly two tools: `fork-manager` and `join`/,
    // no fork-manager(existing_id) reuse API — the denial is explicit.
    /fork-manager\(existing_id\)/,
    // no reuse tool, and inventing tools is forbidden.
    /`reuse` tool|no reuse API/i,
    // explicit prohibition of invented APIs.
    /do not invent tools|invent tools/i,
  ]
  const failures = []
  for (const pattern of required) {
    if (!pattern.test(prompt)) failures.push(`orchestrator prompt is missing ${pattern}`)
  }

  // No list() / fork(existing_id) invocation may be prescribed: the surface is [fork-manager, join].
  if (/list\(\)|fork\(existing_agent_id|fork\(agent_id\)/.test(prompt)) {
    failures.push('orchestrator prompt must not prescribe list()/fork(existing_id) invocations')
  }

  assert.deepEqual(failures, [], 'orchestrator reuse discipline must be executable without list/fork(existing_id)')
})

// ── PENDING 2: same-goal follow-ups continue the existing Manager job ────────

test('ORCHESTRATOR_REUSE_002_prompt_continues_existing_manager_job_on_conflict_followup_recovery', () => {
  const prompt = promptText()
  const required = [
    // PENDING 2: publish conflict → same Manager job, not a new one.
    /Publish conflicts|发布冲突/,
    // PENDING 2: supplemental edits → same Manager job.
    /follow-up edits|补充修改/,
    // PENDING 2: recovery / resume execution → same Manager job.
    /recovery|恢复执行/,
    // the "prefer continuing" directive itself, bound to the delivery goal.
    /continuing the same Manager|existing Manager job|originating Manager|Prefer continuing/i,
    // new Manager is the exception: parallel independent goal only.
    /truly independent|真正并行|parallel independent/i,
    // no invented reuse API: continuation is the mechanism, not a fork-manager(existing_id).
    /no reuse API|not.*invent tools|不存在.*API/i,
  ]
  const failures = []
  for (const pattern of required) {
    if (!pattern.test(prompt)) failures.push(`orchestrator prompt is missing ${pattern}`)
  }
  assert.deepEqual(failures, [], 'same-goal follow-ups must return to the existing Manager job')
})

// ── regression protection: ForkTool manager description keeps reuse/nudge + tdd ──

test('ORCHESTRATOR_REUSE_003_fork_tool_manager_description_keeps_reuse_nudge_and_tdd', () => {
  const source = readFileSync(FORK_TOOL_PATH, 'utf8')
  const managerSpecStart = source.indexOf('let managerSpec')
  const orchestratorSpecStart = source.indexOf('let orchestratorSpec')
  assert.ok(managerSpecStart !== -1, 'ForkTool.fs must define managerSpec')
  assert.ok(
    orchestratorSpecStart !== -1 && orchestratorSpecStart > managerSpecStart,
    'managerSpec must precede orchestratorSpec',
  )
  const managerBlock = source.slice(managerSpecStart, orchestratorSpecStart)
  assert.match(managerBlock, /reuse\/nudge/, 'manager description must advertise reuse/nudge')
  assert.match(managerBlock, /\btdd\b/, 'manager description must advertise tdd')
})
