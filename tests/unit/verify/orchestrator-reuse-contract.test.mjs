// tests/unit/verify/orchestrator-reuse-contract.test.mjs — fork-manager reuse contract.
//
// Layer 0 static resource contract. No dist import, no build needed: the prompt resource and the
// ForkTool source are the physical facts under test.
//
// The Orchestrator tool surface is exactly [fork-manager, join] (AGENT-006). Reuse is a REAL API:
// `fork-manager(existing_job_id, prompt)` continues the existing Manager job in its worktree
// (GLORY-068, `reused=true` in the result), so the prompt must say so — an orchestrator that does
// not know the reuse API would fork duplicate jobs on every follow-up. The first test requires the
// prompt to carry the executable reuse rules: continue the same Manager job for same-goal
// follow-ups; fork-manager with an existing job id as the continuation mechanism; a new job only
// for a truly independent target (different worktree / different lane); and no invented APIs
// beyond that surface. The second and third tests are regression protection: the "continue the
// existing Manager job" clauses and ForkTool's manager description (reuse/nudge + tdd).

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const PROMPT_PATH = new URL('../../../resources/prompts/orchestrator-system.md', import.meta.url)
const FORK_TOOL_PATH = new URL(
  '../../../src/Wanxiangshu/Infrastructure/OpenCode/Tools/ForkTool.fs',
  import.meta.url,
)

const promptText = () => readFileSync(PROMPT_PATH, 'utf8')

// ── executable reuse discipline (continue same job via fork-manager(existing_job_id)) ──

test('ORCHESTRATOR_REUSE_001_prompt_carries_executable_same_job_continue_rules', () => {
  const prompt = promptText()
  const required = [
    // same delivery goal → continue the same Manager job (explicit directive).
    /continue the same Manager job|continuing the same Manager job/i,
    // the continuation target is the originating Manager session.
    /originating Manager session|originating Manager/i,
    // reuse is a REAL fork-manager API: an existing manager job id may be passed.
    /existing manager job id|existing_job_id/i,
    // the fork-manager result marks a continuation (executable claim, ForkTool emits reused=true).
    /reused=true/,
    // same-goal work stays in the same worktree.
    /same worktree/i,
    // new fork-manager is reserved for truly independent goals.
    /truly independent/i,
    // independence is defined as a different worktree / different lane.
    /different worktree|different lane/i,
    // the tool surface is exactly [fork-manager, join]: no list tool exists.
    /exactly two tools: `fork-manager` and `join`/,
    // explicit prohibition of invented APIs.
    /do not invent tools|invent tools/i,
  ]
  const failures = []
  for (const pattern of required) {
    if (!pattern.test(prompt)) failures.push(`orchestrator prompt is missing ${pattern}`)
  }

  // No list() / fork(existing_agent_id) invocation may be prescribed, and no separate
  // `reuse` tool may be invented: the surface is [fork-manager, join], reuse goes through
  // fork-manager(existing_job_id) only.
  if (/list\(\)|fork\(existing_agent_id|fork\(agent_id\)|`reuse` tool/.test(prompt)) {
    failures.push('orchestrator prompt must not prescribe list()/fork(existing_id)/a separate `reuse` tool')
  }

  assert.deepEqual(failures, [], 'orchestrator reuse discipline must be executable through fork-manager(existing_job_id)')
})

// ── same-goal follow-ups continue the existing Manager job ───────────────────

test('ORCHESTRATOR_REUSE_002_prompt_continues_existing_manager_job_on_conflict_followup_recovery', () => {
  const prompt = promptText()
  const required = [
    // publish conflict → same Manager job, not a new one.
    /Publish conflicts|发布冲突/,
    // supplemental edits → same Manager job.
    /follow-up edits|补充修改/,
    // recovery / resume execution → same Manager job.
    /recovery|恢复执行/,
    // the "prefer continuing" directive itself, bound to the delivery goal.
    /continuing the same Manager|existing Manager job|originating Manager|Prefer continuing/i,
    // new Manager is the exception: parallel independent goal only.
    /truly independent|真正并行|parallel independent/i,
    // no invented API beyond fork-manager(existing_job_id): no `reuse`/`list` tools.
    /do not invent tools/i,
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
