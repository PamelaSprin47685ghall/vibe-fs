import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const target = { model: 'provider/shared', reasoning: 'none' }
const identity = (overrides = {}) => ({
  sessionId: 'session-a',
  physicalUserMessageId: 'message-a',
  effectiveAgent: 'coder',
  target,
  ...overrides,
})

const acquire = async (runtime, exact = identity()) => {
  const outcome = await routing.acquireExecutionAdmission(
    runtime,
    exact.sessionId,
    exact.physicalUserMessageId,
    exact.effectiveAgent,
  )
  assert.equal(outcome.kind, 'Acquired')
  return outcome.lease
}

const conflict = (outcome) => assert.deepEqual(outcome, { kind: 'Conflict' })

test('WHAT[EMR-012] capacity lifecycle admits every legal fenced transition', async () => {
  const beforeProvider = routing.createRuntime(() => target)
  const beforeProviderLease = await acquire(beforeProvider)
  assert.equal(routing.executionAdmissionLifecycle(beforeProvider, beforeProviderLease), 'Pending')
  assert.deepEqual(
    routing.releaseExecutionAdmissionBeforeProvider(beforeProvider, beforeProviderLease, identity()),
    { kind: 'Applied' },
  )
  assert.equal(routing.executionAdmissionLifecycle(beforeProvider, beforeProviderLease), 'Released')

  const provider = routing.createRuntime(() => target)
  const providerLease = await acquire(provider)
  assert.deepEqual(routing.commitExecutionAdmission(provider, providerLease, identity()), { kind: 'Applied' })
  assert.equal(routing.executionAdmissionLifecycle(provider, providerLease), 'Committed')
  routing.releasePhysicalExecution(provider, identity().sessionId, identity().physicalUserMessageId)
  assert.equal(routing.executionAdmissionLifecycle(provider, providerLease), 'Released')
})

test('WHAT[EMR-012] capacity lifecycle rejects every illegal edge and opposite terminal', async () => {
  const committedRuntime = routing.createRuntime(() => target)
  const committed = await acquire(committedRuntime)
  routing.commitExecutionAdmission(committedRuntime, committed, identity())
  conflict(routing.releaseExecutionAdmissionBeforeProvider(committedRuntime, committed, identity()))

  const releasedRuntime = routing.createRuntime(() => target)
  const released = await acquire(releasedRuntime)
  routing.releaseExecutionAdmissionBeforeProvider(releasedRuntime, released, identity())
  conflict(routing.commitExecutionAdmission(releasedRuntime, released, identity()))
  assert.deepEqual(
    routing.releaseExecutionAdmissionBeforeProvider(releasedRuntime, released, identity()),
    { kind: 'AlreadyApplied' },
  )
})

test('WHAT[EMR-012] same physical retry preserves the capability and a newer generation stales it', async () => {
  const runtime = routing.createRuntime(() => target)
  const first = await acquire(runtime)
  assert.equal(await acquire(runtime), first)

  const newerIdentity = identity({ physicalUserMessageId: 'message-b' })
  const newer = await acquire(runtime, newerIdentity)
  assert.notEqual(newer, first)
  assert.deepEqual(routing.commitExecutionAdmission(runtime, first, identity()), { kind: 'StaleFence' })
  assert.deepEqual(routing.commitExecutionAdmission(runtime, newer, newerIdentity), { kind: 'Applied' })
})

test('WHAT[EMR-011] rejects release with the wrong physical fence', async () => {
  const owner = routing.createRuntime(() => target)
  const wrongOwner = routing.createRuntime(() => target)
  const lease = await acquire(owner)

  assert.deepEqual(routing.releaseExecutionAdmissionBeforeProvider(wrongOwner, lease, identity()), {
    kind: 'StaleFence',
  })
  assert.equal(routing.executionAdmissionLifecycle(owner, lease), 'Pending')
  assert.equal(routing.snapshotOccupied(owner).length, 1)
})
