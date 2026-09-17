import assert from 'node:assert/strict'
import test from 'node:test'
import * as retirement from '../../../dist/Mission/Relay/Retirement/Surface.js'



test('WHAT[RETIRE-001] suicide retires without any quality progress or test gate', () => {
  assert.deepEqual(
    retirement.decide([], {
      assessed: false,
      openObligations: 3,
      testsPassing: false,
      dirty: false,
      unmerged: false,
    }),
    { decision: 'Retire' },
  )
})
