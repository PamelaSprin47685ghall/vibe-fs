// Join completion — MISSING_FINAL_REPORT / empty-terminal are observation-only.
// The HostForkRunLifecycle owner receives plain terminal observations and keeps
// its pending run, completion source, and pending-runs dictionary behind an
// opaque capability.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Execution/Delegation/Fork/Host/HostForkRunLifecycleSurface.js'

const makeRun = (agentId, childId, parentId) => lifecycle.create({ agentId, childId, parentId })

const remainsPending = async (run, message) => {
  const observed = lifecycle.observe(run)
  assert.equal(observed.finished, false, `${message} must not settle the run`)
  assert.equal(observed.pending, true, 'run must stay pending for a later proven terminal')

  const resolved = await Promise.race([
    lifecycle.completion(run).then(() => true, () => true),
    new Promise((resolve) => setTimeout(() => resolve(false), 50)),
  ])
  assert.equal(resolved, false, `${message} must not resolve the run completion cell`)
}

test('WHAT[EFFECT-ACCOUNTING-002] EXEC_join_MissingFinalReport_Failed_keeps_run_pending_not_failed', async () => {
  const run = makeRun('agent-mfr', 'ses_mfr_child', 'ses_mfr_parent')
  await lifecycle.complete(run, { kind: 'Failed', message: 'MISSING_FINAL_REPORT' })
  await remainsPending(run, 'MISSING_FINAL_REPORT')
})

test('WHAT[EFFECT-ACCOUNTING-002] EXEC_join_empty_Completed_keeps_run_pending_not_failed', async () => {
  const run = makeRun('agent-empty', 'ses_empty_child', 'ses_empty_parent')
  await lifecycle.complete(run, { kind: 'Completed', terminalText: '' })
  await remainsPending(run, 'empty Completed')
})

test('WHAT[EFFECT-ACCOUNTING-002] EXEC_join_interaction_repair_exhausted_settles_the_run', async () => {
  const run = makeRun('agent-repair-exhausted', 'ses_repair_exhausted_child', 'ses_repair_exhausted_parent')
  await lifecycle.complete(run, { kind: 'Failed', message: 'INTERACTION_REPAIR_EXHAUSTED' })

  const observed = lifecycle.observe(run)
  assert.equal(observed.finished, true, 'bounded repair exhaustion is a terminal failure, not another repair opportunity')
  assert.equal(observed.pending, false)
  assert.deepEqual(await lifecycle.completion(run), {
    status: 'failed',
    agentId: 'agent-repair-exhausted',
    code: 'ERROR',
    message: 'INTERACTION_REPAIR_EXHAUSTED',
  })
})

test('WHAT[EFFECT-ACCOUNTING-002] EXEC_join_real_Failed_still_claims_run', async () => {
  const run = makeRun('agent-real', 'ses_real_child', 'ses_real_parent')
  await lifecycle.complete(run, { kind: 'Failed', message: 'provider timeout' })

  const observed = lifecycle.observe(run)
  assert.equal(observed.finished, true, 'real failure must still claim the run')
  assert.equal(observed.pending, false)
  assert.deepEqual(await lifecycle.completion(run), {
    status: 'failed',
    agentId: 'agent-real',
    code: 'ERROR',
    message: 'provider timeout',
  })
})
