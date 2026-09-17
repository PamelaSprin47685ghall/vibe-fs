import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const open = (state, road = 'road-1', incumbent = 'inc-1', snapshot = 'snapshot-1') =>
  relay.openIncumbency(state, road, incumbent, snapshot, 'authority-1')

test('WHAT[RELAY-005] retired iteration never reactivates and stale runs stay absorbed', () => {
  const first = open(relay.empty())
  const assessed = relay.assess(
    first.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    'REVISE', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT',
  )
  assert.equal(assessed.ok, true)
  const retired = relay.retireContinue(assessed.state, 'road-1', 'inc-1', 'ret-1', 'run-1', 'tool-1', 'snapshot-1')
  assert.equal(retired.ok, true)

  const resurrected = relay.openIncumbency(retired.state, 'road-1', 'inc-1', 'snapshot-2', 'authority-1')
  assert.equal(resurrected.ok, false)

  const revised = relay.advanceAuthority(
    retired.state,
    'road-1',
    'inc-1',
    'authority-1',
    'authority-2',
    'physical-authority-2',
    'snapshot-2',
  )
  assert.equal(revised.ok, false)
})
