import assert from 'node:assert/strict'
import test from 'node:test'
import * as retirement from '../../../dist/Mission/Relay/Retirement/Surface.js'



test('WHAT[relay-retirement-003] live recursive resources are the only business blockers', () => {
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
