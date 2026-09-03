import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const target = { model: 'provider/shared', reasoning: 'none' }
const identity = (sessionId, physicalUserMessageId, effectiveAgent = 'coder') => ({
  sessionId,
  physicalUserMessageId,
  effectiveAgent,
  target,
})

const acquire = async (runtime, exact) => {
  const outcome = await routing.acquireExecutionAdmission(
    runtime,
    exact.sessionId,
    exact.physicalUserMessageId,
    exact.effectiveAgent,
  )
  assert.equal(outcome.kind, 'Acquired')
  return outcome.lease
}

const exactKey = ({ sessionId, physicalUserMessageId }) => `${sessionId}\u001f${physicalUserMessageId}`

test('WHAT[EMR-014] valid immutable snapshot is a reconciliation no-op with traceable tokens, waiters, and lineage', async () => {
  const runtime = routing.createRuntime((_role, running) => (running.length === 0 ? target : null))
  const holder = identity('holder', 'physical-holder')
  const holderLease = await acquire(runtime, holder)
  assert.deepEqual(routing.commitExecutionAdmission(runtime, holderLease, holder), { kind: 'Applied' })
  routing.bindCapacityChild(runtime, 'holder', 'lineage-child')
  const queued = await routing.beginExecutionAdmission(runtime, 'waiting', 'physical-waiting', 'coder')
  assert.equal(queued.kind, 'Queued')

  const snapshot = routing.capacitySnapshot(runtime)
  assert.equal(Object.isFrozen(snapshot), true)
  assert.equal(Object.isFrozen(snapshot.tokens), true)
  assert.equal(Object.isFrozen(snapshot.tokens[0]), true)
  assert.equal(Object.isFrozen(snapshot.waiters), true)
  assert.equal(snapshot.ledgerEntries.length, 1)
  assert.ok(snapshot.activeCount >= 0 && snapshot.activeCount <= snapshot.ledgerEntries.length)
  assert.deepEqual(snapshot.tokenStateCounts, { idle: 1, inFlight: 0, retiring: 0 })

  const executionKeys = new Set(snapshot.executions.map(exactKey))
  for (const token of snapshot.tokens) assert.ok(executionKeys.has(exactKey(token.owner)), 'token owner is exact and traceable')
  for (const waiter of snapshot.waiters)
    assert.ok(snapshot.owners.some((owner) => exactKey(owner) === exactKey(waiter)), 'waiter owner is traceable')
  assert.deepEqual(snapshot.lineage, [{ childSessionId: 'lineage-child', parentSessionId: 'holder' }])
  assert.deepEqual(routing.reconcileCapacityEvidence(snapshot), { kind: 'NoOp' })
  assert.deepEqual(routing.capacitySnapshot(runtime), snapshot, 'reconciliation is read-only')

  assert.throws(() => snapshot.tokens.push({}), TypeError)
  assert.deepEqual(routing.cancelPendingExecution(runtime, 'waiting'), { kind: 'Applied' })
})

test('WHAT[EMR-014] duplicate release never decrements twice', async () => {
  const runtime = routing.createRuntime(() => target)
  const firstIdentity = identity('session', 'physical-1')
  await acquire(runtime, firstIdentity)

  assert.deepEqual(routing.releasePhysicalExecution(runtime, 'session', 'physical-1'), { kind: 'Applied' })
  const released = routing.capacitySnapshot(runtime)
  assert.equal(released.ledgerEntries.length, 0)
  assert.deepEqual(routing.releasePhysicalExecution(runtime, 'session', 'physical-1'), { kind: 'AlreadyApplied' })
  const duplicate = routing.capacitySnapshot(runtime)
  assert.equal(duplicate.ledgerEntries.length, 0)
  assert.equal(duplicate.counters.duplicate, released.counters.duplicate + 1)
})

test('WHAT[EMR-014] stale fence cannot touch a newer execution', async () => {
  const runtime = routing.createRuntime(() => target)
  const firstIdentity = identity('session', 'physical-1')
  const first = await acquire(runtime, firstIdentity)
  const secondIdentity = identity('session', 'physical-2')
  const second = await acquire(runtime, secondIdentity)
  const before = routing.capacitySnapshot(runtime)
  assert.deepEqual(routing.commitExecutionAdmission(runtime, first, firstIdentity), { kind: 'StaleFence' })
  const stale = routing.capacitySnapshot(runtime)
  assert.equal(stale.counters.stale, before.counters.stale + 1)
  assert.ok(stale.executions.some((owner) => exactKey(owner) === exactKey(secondIdentity)))
  assert.deepEqual(routing.commitExecutionAdmission(runtime, second, secondIdentity), { kind: 'Applied' })
})

test('WHAT[EMR-014] opposite terminal transition is a monotonic conflict', async () => {
  const runtime = routing.createRuntime(() => target)
  const exact = identity('session', 'physical')
  const lease = await acquire(runtime, exact)
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, exact), { kind: 'Applied' })
  const before = routing.capacitySnapshot(runtime)
  assert.deepEqual(routing.releaseExecutionAdmissionBeforeProvider(runtime, lease, exact), { kind: 'Conflict' })
  const conflict = routing.capacitySnapshot(runtime)
  assert.equal(conflict.counters.conflict, before.counters.conflict + 1)
})

test('WHAT[EMR-014] reconciliation fails closed on map ledger divergence without repair', async () => {
  const runtime = routing.createRuntime(() => target)
  await acquire(runtime, identity('holder', 'physical-holder'))
  const valid = routing.capacitySnapshot(runtime)
  const divergent = { ...valid, ledgerEntries: [] }

  const decision = routing.reconcileCapacityEvidence(divergent)
  assert.equal(decision.kind, 'FailClosed')
  assert.ok(decision.reasons.includes('MapLedgerDivergence'))
  assert.deepEqual(routing.capacitySnapshot(runtime), valid, 'diagnostic reconciliation never repairs production state')
})
