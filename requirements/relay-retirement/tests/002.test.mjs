import assert from 'node:assert/strict'
import test from 'node:test'
import * as retirement from '../../../dist/Mission/Relay/Retirement/Surface.js'



test('WHAT[RETIRE-002] dirty work quality state and conflicts never block suicide', () => {
  assert.deepEqual(
    retirement.decide([], {
      assessed: false,
      openObligations: 99,
      testsPassing: false,
      dirty: true,
      unmerged: true,
    }),
    { decision: 'Retire' },
  )
})
