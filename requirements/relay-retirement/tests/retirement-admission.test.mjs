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

test('WHAT[RETIRE-003] live recursive resources are the only business blockers', () => {
  assert.deepEqual(
    retirement.decide(
      [
        { id: 'child-1', kind: 'ChildAgent', owner: 'inc-1' },
        { id: 'pty-1', kind: 'Pty', owner: 'child-1' },
      ],
      { assessed: true, openObligations: 0, testsPassing: true, dirty: false, unmerged: false },
    ),
    {
      decision: 'BlockedByResources',
      blockers: [
        { id: 'child-1', kind: 'ChildAgent', owner: 'inc-1' },
        { id: 'pty-1', kind: 'Pty', owner: 'child-1' },
      ],
    },
  )
})

test('WHAT[RETIRE-004] freeze fence rejects resource creation racing after retirement begins', () => {
  const frozen = retirement.freeze('inc-1', 41)
  assert.deepEqual(retirement.admitResource(frozen, 41), { ok: false, error: 'IncumbencyAdmissionsFrozen' })
  assert.deepEqual(retirement.admitResource(frozen, 40), { ok: false, error: 'StaleIncumbencyAdmissionFence' })
})

