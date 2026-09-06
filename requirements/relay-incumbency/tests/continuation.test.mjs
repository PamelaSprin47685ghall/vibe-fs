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
    9, 10, 10, 10, 10, 10, 10, 10,
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

test('WHAT[RELAY-006] Continue keeps the road open for a next iteration', () => {
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
  const retired = relay.retireContinue(assessed.state, 'road-1', 'inc-1', 'ret-1', 'run-1', 'tool-1', 'snapshot-2')
  assert.equal(retired.ok, true)
  assert.deepEqual(relay.retirement(retired.state, 'road-1'), {
    retirementId: 'ret-1',
    incumbentId: 'inc-1',
    outcome: 'Continue',
    certificateId: null,
    providerRunId: 'run-1',
    toolCallId: 'tool-1',
    snapshotId: 'snapshot-2',
    authorityRevision: 'authority-1',
  })
  assert.equal(relay.view(retired.state, 'road-1').activeIncumbency, null)

  const next = relay.openIncumbency(retired.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(next.ok, true)
  assert.equal(relay.view(next.state, 'road-1').phase, 'AuditPending')
  assert.equal(relay.authority(next.state, 'road-1').activeSnapshot, 'snapshot-2')
})

test('WHAT[RELAY-006] Accepted blocks reopening while valid, invalidation reopens it', () => {
  const first = open(relay.empty())
  const assessed = relay.assess(
    first.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill(10),
  )
  assert.equal(assessed.ok, true)
  const retired = relay.retireAccepted(
    assessed.state,
    'road-1',
    'inc-1',
    'ret-1',
    'run-1',
    'tool-1',
    'certificate:assessment-1',
    'snapshot-1',
  )
  assert.equal(retired.ok, true)
  assert.deepEqual(relay.retirement(retired.state, 'road-1'), {
    retirementId: 'ret-1',
    incumbentId: 'inc-1',
    outcome: 'Accepted',
    certificateId: 'certificate:assessment-1',
    providerRunId: 'run-1',
    toolCallId: 'tool-1',
    snapshotId: 'snapshot-1',
    authorityRevision: 'authority-1',
  })

  const blocked = relay.openIncumbency(retired.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.deepEqual(blocked, { ok: false, error: 'RoadAlreadyAccepted' })

  const invalidated = relay.invalidateCertificate(retired.state, 'road-1', 'WorkspaceChanged')
  assert.equal(invalidated.ok, true)
  assert.equal(relay.certificate(invalidated.state, 'road-1').valid, false)

  const next = relay.openIncumbency(invalidated.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(next.ok, true)
  assert.deepEqual(relay.view(next.state, 'road-1'), {
    activeIncumbency: 'inc-2',
    phase: 'AuditPending',
    retired: ['inc-1'],
  })
  assert.deepEqual(relay.retirement(next.state, 'road-1'), {
    retirementId: 'ret-1',
    incumbentId: 'inc-1',
    outcome: 'Accepted',
    certificateId: 'certificate:assessment-1',
    providerRunId: 'run-1',
    toolCallId: 'tool-1',
    snapshotId: 'snapshot-1',
    authorityRevision: 'authority-1',
  })
})
