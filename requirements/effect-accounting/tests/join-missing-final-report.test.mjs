// Join completion — MISSING_FINAL_REPORT / empty-terminal must be observation-only.
//
// A manager's fork subagent that stops with an empty or missing final report is
// NOT concluded as failed. Per FALLBACK-008 an empty/XML-only terminal earns an
// interaction repair (RepairOnce / AbandonRoundProduct), never a slot failure; the
// subagent auto-retries and continues. `HostForkRunLifecycle.complete` must keep
// the pending run Active for a later proven terminal (P0-RECOVERY-JOIN-001) instead
// of delivering a proven MISSING_FINAL_REPORT failure. This mirrors the `Aborted`
// branch ("Observation only. Keep pending run Active for a later proven terminal.").

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  EventsModule,
  Outcome,
  authorityRoot,
  pendingRunLifecycle,
  providerRun,
  roles,
  sessionId,
  unionCase,
} from '../../verification-system/tests/support/domain.mjs'

// ── pending-runs dictionary + run helpers (mirror join-completion.test.mjs) ───
// `HostForkRunLifecycle.complete` only ever calls Map methods (has/get/set/delete),
// so a plain JS Map is the JS-native dictionary for the pending-runs slot.

function makeRun(agentId, childId) {
  const source = pendingRunLifecycle.completionSource()
  return {
    Token: {},
    AgentId: agentId,
    ChildId: childId,
    Role: roles.of('Coder'),
    Source: source,
    Subscription: undefined,
    Finished: false,
  }
}

// TerminalOutcome.Completed carries an AgentRunResult; Failed carries a reason.
const terminalOutcome = unionCase(EventsModule.TerminalOutcome, 'TerminalOutcome')

async function failedOutcome(error) {
  return terminalOutcome('Failed', [error])
}

// AgentRunResult record (dist/Foundation/Outcome.js) — field order:
// SessionId, AuthorityRootUserMessageId, ProviderRun, Role, Directory,
// TerminalText, TurnFormalText. TerminalText empty => IsValid=false.
async function completedOutcome(terminalText) {
  const result = new Outcome.AgentRunResult(
    sessionId('ses_child'),
    authorityRoot('root_1'),
    providerRun('run_1'),
    roles.of('Coder'),
    null, // Directory: string option -> null is None in Fable output
    terminalText,
    terminalText,
  )
  return terminalOutcome('Completed', [result])
}

// ── MISSING_FINAL_REPORT is observation-only ─────────────────────────────────

test('WHAT[EFFECT-ACCOUNTING-002] EXEC_join_MissingFinalReport_Failed_keeps_run_pending_not_failed', async () => {
  const pendingRuns = new Map()
  const gate = {}
  const agentId = 'agent-mfr'
  const child = sessionId('ses_mfr_child')
  const parent = sessionId('ses_mfr_parent')
  const run = makeRun(agentId, child)
  pendingRuns.set(agentId, run)

  const mfrFailed = await failedOutcome('MISSING_FINAL_REPORT')
  pendingRunLifecycle.complete(gate, pendingRuns, undefined, parent, null, run, mfrFailed, undefined)

  // Observation only (like Aborted): the run is not claimed.
  assert.equal(run.Finished, false, 'MISSING_FINAL_REPORT must not settle the run')
  assert.equal(pendingRuns.has(agentId), true, 'run must stay in pendingRuns for a later proven terminal')

  // The completion cell must NOT be resolved — a later proven terminal will.
  const resolved = await Promise.race([
    run.Source.get_Task().then(() => true, () => true),
    new Promise((resolve) => setTimeout(() => resolve(false), 50)),
  ])
  assert.equal(resolved, false, 'MISSING_FINAL_REPORT must not resolve the run completion Source')
})

test('WHAT[EFFECT-ACCOUNTING-002] EXEC_join_empty_Completed_keeps_run_pending_not_failed', async () => {
  const pendingRuns = new Map()
  const gate = {}
  const agentId = 'agent-empty'
  const child = sessionId('ses_empty_child')
  const parent = sessionId('ses_empty_parent')
  const run = makeRun(agentId, child)
  pendingRuns.set(agentId, run)

  const emptyCompleted = await completedOutcome('')
  pendingRunLifecycle.complete(gate, pendingRuns, undefined, parent, null, run, emptyCompleted, undefined)

  assert.equal(run.Finished, false, 'empty Completed must not settle the run')
  assert.equal(pendingRuns.has(agentId), true, 'empty Completed must keep the run pending')

  const resolved = await Promise.race([
    run.Source.get_Task().then(() => true, () => true),
    new Promise((resolve) => setTimeout(() => resolve(false), 50)),
  ])
  assert.equal(resolved, false, 'empty Completed must not resolve the run completion Source')
})

// ── genuine failures still settle the run ────────────────────────────────────

test('WHAT[EFFECT-ACCOUNTING-002] EXEC_join_interaction_repair_exhausted_settles_the_run', async () => {
  const pendingRuns = new Map()
  const gate = {}
  const agentId = 'agent-repair-exhausted'
  const child = sessionId('ses_repair_exhausted_child')
  const parent = sessionId('ses_repair_exhausted_parent')
  const run = makeRun(agentId, child)
  pendingRuns.set(agentId, run)

  const exhausted = await failedOutcome('INTERACTION_REPAIR_EXHAUSTED')
  pendingRunLifecycle.complete(gate, pendingRuns, undefined, parent, null, run, exhausted, undefined)

  assert.equal(run.Finished, true, 'bounded repair exhaustion is a terminal failure, not another repair opportunity')
  assert.equal(pendingRuns.has(agentId), false)
  await Promise.race([
    run.Source.get_Task(),
    new Promise((_, reject) => setTimeout(() => reject(new Error('Source never completed')), 1000)),
  ])
})

test('WHAT[EFFECT-ACCOUNTING-002] EXEC_join_real_Failed_still_claims_run', async () => {
  const pendingRuns = new Map()
  const gate = {}
  const agentId = 'agent-real'
  const child = sessionId('ses_real_child')
  const parent = sessionId('ses_real_parent')
  const run = makeRun(agentId, child)
  pendingRuns.set(agentId, run)

  const realFailed = await failedOutcome('provider timeout')
  pendingRunLifecycle.complete(gate, pendingRuns, undefined, parent, null, run, realFailed, undefined)

  assert.equal(run.Finished, true, 'real failure must still claim the run')
  const agentOutcome = await Promise.race([
    run.Source.get_Task(),
    new Promise((_, reject) => setTimeout(() => reject(new Error('Source never completed')), 1000)),
  ])
  assert.equal(agentOutcome.name, 'AgentFailed')
})
