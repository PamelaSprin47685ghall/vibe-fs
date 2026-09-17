import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const open = (state, road = 'road-1', incumbent = 'inc-1') =>
  relay.openIncumbency(state, road, incumbent, 'snapshot-1', 'authority-1')

test('WHAT[RELAY-003] iteration starts empowered with no work before review', () => {
  const first = open(relay.empty())
  assert.equal(first.ok, true)
  const view = relay.view(first.state, 'road-1')
  assert.equal(view.phase, 'AuditPending')
  assert.equal(relay.certificate(first.state, 'road-1'), null)
  assert.equal(relay.retirement(first.state, 'road-1'), null)
})
