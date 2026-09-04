import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const open = (state, road = 'road-1', incumbent = 'inc-1') =>
  relay.openIncumbency(state, road, incumbent, 'snapshot-1', 'authority-1', 'ExistingWorld')

test('WHAT[RELAY-001] one open road admits at most one active incumbent', () => {
  const first = open(relay.empty())
  assert.equal(first.ok, true)
  const second = relay.openIncumbency(first.state, 'road-1', 'inc-2', 'snapshot-1', 'authority-1', 'Retirement')
  assert.deepEqual(second, { ok: false, error: 'ActiveIncumbencyAlreadyExists' })
})

test('WHAT[RELAY-002] WHAT[RELAY-003] first and successor incumbencies both start AuditPending', () => {
  const first = open(relay.empty())
  assert.deepEqual(relay.view(first.state, 'road-1'), {
    activeIncumbency: 'inc-1',
    phase: 'AuditPending',
    source: 'ExistingWorld',
    retired: [],
  })

  const retired = relay.retire(first.state, 'road-1', 'inc-1', 'ret-1', 'snapshot-1', 'baton-1', 'cut-1', false)
  assert.equal(retired.ok, true)
  const successor = relay.activateSuccessor(
    retired.state,
    'road-1',
    'ret-1',
    'inc-2',
    'snapshot-1',
    'authority-1',
  )
  assert.equal(successor.ok, true)
  assert.equal(relay.view(successor.state, 'road-1').phase, 'AuditPending')
  assert.equal(relay.view(successor.state, 'road-1').source, 'Retirement')
})

test('WHAT[RELAY-005] retired incumbent cannot be activated again', () => {
  const first = open(relay.empty())
  const retired = relay.retire(first.state, 'road-1', 'inc-1', 'ret-1', 'snapshot-1', 'baton-1', 'cut-1', false)
  assert.equal(retired.ok, true)
  const resurrected = relay.activateSuccessor(
    retired.state,
    'road-1',
    'ret-1',
    'inc-1',
    'snapshot-1',
    'authority-1',
  )
  assert.deepEqual(resurrected, { ok: false, error: 'RetiredIncumbencyCannotReactivate' })
})

test('WHAT[RELAY-006] successor requires committed retirement baton and cut', () => {
  const first = open(relay.empty())
  const premature = relay.activateSuccessor(
    first.state,
    'road-1',
    'ret-1',
    'inc-2',
    'snapshot-1',
    'authority-1',
  )
  assert.deepEqual(premature, { ok: false, error: 'PredecessorRetirementNotCommitted' })
})

