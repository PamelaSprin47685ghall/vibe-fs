import assert from 'node:assert/strict'
import test from 'node:test'
import * as retirement from '../../../dist/Mission/Relay/Retirement/Surface.js'



test('WHAT[relay-retirement-004] freeze fence rejects retirement races without crossing the next iteration boundary', () => {
  const frozen = retirement.freeze('inc-1', 41)
  assert.deepEqual(retirement.admitResource(frozen, 41), { ok: false, error: 'IncumbencyAdmissionsFrozen' })
  assert.deepEqual(retirement.admitResource(frozen, 40), { ok: false, error: 'StaleIncumbencyAdmissionFence' })
  assert.equal(retirement.fenceAppliesTo(frozen, 'inc-1'), true)
  assert.equal(retirement.fenceAppliesTo(frozen, 'inc-2'), false)
})
