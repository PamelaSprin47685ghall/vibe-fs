import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const open = (state) => relay.openIncumbency(state, 'road-1', 'inc-1', 'snapshot-1', 'authority-1')

test('WHAT[relay-assessment-006] assessed iteration cannot submit a second review after work begins', () => {
  const opened = open(relay.empty())
  const assessed = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill('PERFECT'),
  )
  assert.equal(assessed.ok, true)
  assert.deepEqual(
    relay.assess(
      assessed.state,
      'road-1',
      'inc-1',
      'assessment-2',
      'snapshot-1',
      'authority-1',
      ...Array(8).fill('PERFECT'),
    ),
    { ok: false, error: 'AssessmentAlreadySubmitted' },
  )

  // Even with different/revise scores, duplicate review in same incumbency is rejected
  assert.deepEqual(
    relay.assess(
      assessed.state,
      'road-1',
      'inc-1',
      'assessment-3',
      'snapshot-1',
      'authority-1',
      ...Array(8).fill('REVISE'),
    ),
    { ok: false, error: 'AssessmentAlreadySubmitted' },
  )
})
