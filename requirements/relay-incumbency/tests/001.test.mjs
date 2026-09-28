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
  const replay = open(first.state)
  assert.equal(replay.ok, true)
  assert.deepEqual(relay.view(replay.state, 'road-1'), relay.view(first.state, 'road-1'))
  assert.equal(relay.openIncumbency(first.state, 'road-1', 'inc-1', 'snapshot-2', 'authority-1').ok, false)
  assert.equal(relay.openIncumbency(first.state, 'road-1', 'inc-1', 'snapshot-1', 'authority-2').ok, false)
})

test('WHAT[relay-incumbency-001] concurrent production opening derives one identity and admits one physical loop prompt', {todo: 'GAP-192: sequential Road fold calls do not exercise concurrent opening or prompt dispatch'})
