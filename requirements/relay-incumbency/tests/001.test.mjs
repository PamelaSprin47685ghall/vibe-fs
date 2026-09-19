import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const open = (state, road = 'road-1', incumbent = 'inc-1') =>
  relay.openIncumbency(state, road, incumbent, 'snapshot-1', 'authority-1')

test('WHAT[relay-incumbency-001] one open road admits at most one active iteration', () => {
  const first = open(relay.empty())
  assert.equal(first.ok, true)
  const second = relay.openIncumbency(first.state, 'road-1', 'inc-2', 'snapshot-1', 'authority-1')
  assert.equal(second.ok, false)
})
