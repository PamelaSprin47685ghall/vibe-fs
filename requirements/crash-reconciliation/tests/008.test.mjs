import assert from 'node:assert/strict'
import test from 'node:test'
import * as quiescence from '../../../dist/Execution/Host/QuiescencePermitSurface.js'

test('WHAT[CRASH-008] ESC_P0_2_operator_abort_revokes_unconsumed_idle_permit', () => {
  const gate = quiescence.create()
  quiescence.recordIdle(gate, 'ses-1', 'att-1')
  quiescence.recordAbort(gate, 'ses-1')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), false)
})

test('WHAT[CRASH-008] ESC_P0_3_aborted_attempt_cannot_be_reminted_by_delayed_idle', () => {
  const gate = quiescence.create()
  quiescence.recordAbort(gate, 'ses-1')
  quiescence.recordIdle(gate, 'ses-1', 'att-old')
  assert.equal(quiescence.hasPermit(gate, 'ses-1'), false)
})
