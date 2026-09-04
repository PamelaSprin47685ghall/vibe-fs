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

test('WHAT[RELAY-002] first incumbency opens on the same AuditPending state machine', () => {
  const first = open(relay.empty())
  assert.deepEqual(relay.view(first.state, 'road-1'), {
    activeIncumbency: 'inc-1',
    phase: 'AuditPending',
    source: 'ExistingWorld',
    retired: [],
  })
})

test('WHAT[RELAY-003] successor incumbency starts AuditPending after committed retirement', () => {
  const first = open(relay.empty())
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

test('WHAT[RELAY-004] low-score assessor takes work ownership in place without a new incumbency', () => {
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
    source: 'ExistingWorld',
    retired: [],
  })
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

test('WHAT[RELAY-009] active authority update advances revision and snapshot exactly once', () => {
  const first = open(relay.empty())
  const workOwned = relay.assess(
    first.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    9, 10, 10, 10, 10, 10, 10, 10,
  )
  assert.equal(workOwned.ok, true)

  const revised = relay.advanceAuthority(
    workOwned.state,
    'road-1',
    'inc-1',
    'authority-1',
    'authority-2',
    'physical-authority-2',
    'snapshot-2',
  )
  assert.equal(revised.ok, true)
  assert.deepEqual(relay.authority(revised.state, 'road-1'), {
    roadRevision: 'authority-2',
    revisionHistory: ['authority-1', 'authority-2'],
    activeRevision: 'authority-2',
    activeSnapshot: 'snapshot-2',
    messageIds: ['authority-1', 'physical-authority-2'],
  })

  const replayed = relay.advanceAuthority(
    revised.state,
    'road-1',
    'inc-1',
    'authority-1',
    'authority-2',
    'physical-authority-2',
    'snapshot-2',
  )
  assert.equal(replayed.ok, true)
  assert.deepEqual(relay.authority(replayed.state, 'road-1'), relay.authority(revised.state, 'road-1'))

  const stale = relay.advanceAuthority(
    revised.state,
    'road-1',
    'inc-1',
    'authority-1',
    'authority-3',
    'physical-authority-3',
    'snapshot-3',
  )
  assert.deepEqual(stale, { ok: false, error: 'AuthorityRevisionStale' })
})

test('WHAT[RELAY-008] authority update invalidates a perfect certificate without restoring work ownership', () => {
  const first = open(relay.empty())
  const perfect = relay.assess(
    first.state,
    'road-1',
    'inc-1',
    'assessment-perfect',
    'snapshot-1',
    'authority-1',
    10, 10, 10, 10, 10, 10, 10, 10,
  )
  assert.equal(perfect.ok, true)
  assert.equal(relay.certificate(perfect.state, 'road-1').valid, true)

  const revised = relay.advanceAuthority(
    perfect.state,
    'road-1',
    'inc-1',
    'authority-1',
    'authority-2',
    'physical-authority-2',
    'snapshot-2',
  )
  assert.equal(revised.ok, true)
  assert.equal(relay.certificate(revised.state, 'road-1').valid, false)
  assert.equal(relay.view(revised.state, 'road-1').phase, 'PerfectAwaitingRetirement')
})

test('WHAT[RELAY-005] retired incumbent cannot receive an authority update', () => {
  const first = open(relay.empty())
  const retired = relay.retire(first.state, 'road-1', 'inc-1', 'ret-1', 'snapshot-1', 'baton-1', 'cut-1', false)
  assert.equal(retired.ok, true)

  const revised = relay.advanceAuthority(
    retired.state,
    'road-1',
    'inc-1',
    'authority-1',
    'authority-2',
    'physical-authority-2',
    'snapshot-2',
  )
  assert.deepEqual(revised, { ok: false, error: 'NoActiveIncumbency' })
})
