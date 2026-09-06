import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const open = (state, road = 'road-1', incumbent = 'inc-1') =>
  relay.openIncumbency(state, road, incumbent, 'snapshot-1', 'authority-1')

test('WHAT[RELAY-001] one open road admits at most one active iteration', () => {
  const first = open(relay.empty())
  assert.equal(first.ok, true)
  const second = relay.openIncumbency(first.state, 'road-1', 'inc-2', 'snapshot-1', 'authority-1')
  assert.equal(second.ok, false)
})

test('WHAT[RELAY-002] every iteration opens on the same AuditPending algebra', () => {
  const first = open(relay.empty())
  assert.deepEqual(relay.view(first.state, 'road-1'), {
    activeIncumbency: 'inc-1',
    phase: 'AuditPending',
    retired: [],
  })

  const assessed = relay.assess(
    first.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    9, 10, 10, 10, 10, 10, 10, 10,
  )
  assert.equal(assessed.ok, true)
  const retired = relay.retireContinue(
    assessed.state,
    'road-1',
    'inc-1',
    'ret-1',
    'run-1',
    'tool-1',
    'snapshot-1',
  )
  assert.equal(retired.ok, true)

  const next = relay.openIncumbency(retired.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(next.ok, true)
  assert.deepEqual(relay.view(next.state, 'road-1'), {
    activeIncumbency: 'inc-2',
    phase: 'AuditPending',
    retired: ['inc-1'],
  })
})

test('WHAT[RELAY-003] iteration starts empowered with no work before review', () => {
  const first = open(relay.empty())
  assert.equal(first.ok, true)
  const view = relay.view(first.state, 'road-1')
  assert.equal(view.phase, 'AuditPending')
  assert.equal(relay.certificate(first.state, 'road-1'), null)
  assert.equal(relay.retirement(first.state, 'road-1'), null)
})

test('WHAT[RELAY-004] low-score assessor takes work ownership in place without a new iteration', () => {
  const first = open(relay.empty())
  const assessed = relay.assess(
    first.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    9, 10, 10, 10, 10, 10, 10, 10,
  )
  assert.equal(assessed.ok, true)
  assert.deepEqual(relay.view(assessed.state, 'road-1'), {
    activeIncumbency: 'inc-1',
    phase: 'WorkOwned',
    retired: [],
  })
})
