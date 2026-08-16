import assert from 'node:assert/strict'
import test from 'node:test'
import { hostPolicy } from './support/host-surface.mjs'

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_idle_early_then_second_signal_completes', () => {
  const first = hostPolicy.reconcile({ snapshots: [{ finish: false }, { finish: false }, { finish: false }], maxReads: 3 })
  const second = hostPolicy.reconcile({ snapshots: [{ finish: false }, { finish: true }], maxReads: 3 })
  assert.equal(first.terminal, null)
  assert.equal(first.stopped, true)
  assert.deepEqual(second.terminal, { finish: true })
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_consecutive_errors_retry_until_ok_terminal', () => {
  const result = hostPolicy.reconcile({ snapshots: [{ error: 'e1' }, { error: 'e2' }, { finish: true }] })
  assert.deepEqual(result.terminal, { finish: true })
  assert.equal(result.reads, 3)
})

test('WHAT[HOST-BOUNDARY-005] EXEC_reconcile_persistent_errors_stop_pass_bounded', () => {
  const result = hostPolicy.reconcile({ snapshots: [{ error: 'e1' }, { error: 'e2' }, { error: 'e3' }, { error: 'e4' }], maxReads: 3 })
  assert.equal(result.reads, 3)
  assert.equal(result.terminal, null)
  assert.equal(result.stopped, true)
})
