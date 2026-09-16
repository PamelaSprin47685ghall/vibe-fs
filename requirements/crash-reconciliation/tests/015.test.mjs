import assert from 'node:assert/strict'
import test from 'node:test'
import * as forkRestart from '../../../dist/Execution/Delegation/Fork/Host/HostForkRestartSurface.js'

test('WHAT[CRASH-015] HFR_restart_multiple_children_recovered_in_link_order', () => {
  const res = forkRestart.recoverMultiple(['c1', 'c2'])
  assert.deepEqual(res.order, ['c1', 'c2'])
})

test('WHAT[CRASH-015] HFR_restart_legacy_false_abort_waits_with_rejection_fact', () => {
  const res = forkRestart.recoverLegacyFalseAbort('ses-1')
  assert.equal(res.status, 'Waiting')
})

test('WHAT[CRASH-015] HFR_restart_retired_legacy_false_abort_refuses_without_replacement', () => {
  const res = forkRestart.recoverRetiredLegacyFalseAbort('ses-1')
  assert.equal(res.refused, true)
})

test('WHAT[CRASH-015] HFR_restart_invalid_completion_blob_waits', () => {
  const res = forkRestart.recoverInvalidBlob('ses-1')
  assert.equal(res.status, 'Waiting')
})
