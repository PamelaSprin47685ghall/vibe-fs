import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })
const identity = (overrides = {}) => ({
  sessionId: 'session-a',
  physicalUserMessageId: 'message-a',
  effectiveAgent: 'fast-coder',
  target: target(),
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

test('WHAT[EMR-012] admission lease is opaque and projects only its frozen target', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  assert.deepEqual(routing.executionAdmissionTarget(runtime, lease), target())
  assert.equal(JSON.stringify(lease), '{}', 'the process capability has no serializable identity or capacity fields')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, {}, identity()), { kind: 'StaleFence' })
})

test('WHAT[EMR-012] admission lease permits one terminal transition and idempotent duplicate', async () => {
  const runtime = routing.createRuntime(() => target())
  const committed = await acquire(runtime)

  assert.deepEqual(routing.commitExecutionAdmission(runtime, committed, identity()), { kind: 'Applied' })
  assert.deepEqual(routing.commitExecutionAdmission(runtime, committed, identity()), { kind: 'AlreadyApplied' })
  conflict(routing.releaseExecutionAdmissionBeforeProvider(runtime, committed, identity()))

  const releasedIdentity = identity({ sessionId: 'session-b', physicalUserMessageId: 'message-b' })
  const released = await acquire(runtime, releasedIdentity)
  assert.deepEqual(
    routing.releaseExecutionAdmissionBeforeProvider(runtime, released, releasedIdentity),
    { kind: 'Applied' },
  )
  assert.deepEqual(
    routing.releaseExecutionAdmissionBeforeProvider(runtime, released, releasedIdentity),
    { kind: 'AlreadyApplied' },
  )
  conflict(routing.commitExecutionAdmission(runtime, released, releasedIdentity))
})

test('WHAT[EMR-012] rejects release from another physical message and every wrong exact identity field', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  for (const change of [
    { sessionId: 'other-session' },
    { physicalUserMessageId: 'other-message' },
    { effectiveAgent: 'deep-coder' },
    { target: { model: 'provider/other', reasoning: 'none' } },
  ]) {
    conflict(routing.releaseExecutionAdmissionBeforeProvider(runtime, lease, identity(change)))
  }

  assert.equal(routing.snapshotOccupied(runtime).length, 1, 'wrong identity cannot release capacity')
})

test('WHAT[EMR-012] same physical retry reuses capability while newer material stales it', async () => {
  const runtime = routing.createRuntime(() => target())
  const first = await acquire(runtime)
  const retry = await acquire(runtime)
  assert.equal(first, retry, 'the exact provider retry observes one capability identity')

  const newerIdentity = identity({ physicalUserMessageId: 'message-b' })
  const newer = await acquire(runtime, newerIdentity)
  assert.notEqual(first, newer)
  assert.deepEqual(routing.commitExecutionAdmission(runtime, first, identity()), { kind: 'StaleFence' })
  assert.deepEqual(routing.releaseExecutionAdmissionBeforeProvider(runtime, first, identity()), {
    kind: 'StaleFence',
  })
  assert.deepEqual(routing.commitExecutionAdmission(runtime, newer, newerIdentity), { kind: 'Applied' })
})

test('WHAT[EMR-012] a lease from another runtime is rejected as the wrong capacity fence', async () => {
  const firstRuntime = routing.createRuntime(() => target())
  const secondRuntime = routing.createRuntime(() => target())
  const lease = await acquire(firstRuntime)

  assert.deepEqual(routing.commitExecutionAdmission(secondRuntime, lease, identity()), { kind: 'StaleFence' })
  assert.equal(routing.snapshotOccupied(firstRuntime).length, 1)
  assert.equal(routing.snapshotOccupied(secondRuntime).length, 0)
})
