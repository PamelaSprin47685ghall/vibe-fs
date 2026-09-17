import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const target = { model: 'provider/shared', reasoning: 'none' }
const identity = (overrides = {}) => ({
  sessionId: 'session-a',
  physicalUserMessageId: 'message-a',
  role: 'engineer',
  participant: 'alice',
  target,
  ...overrides,
})
const acquire = async (runtime, exact = identity()) => {
  const outcome = await routing.acquireExecutionAdmission(
    runtime,
    exact.sessionId,
    exact.physicalUserMessageId,
    exact.role,
    exact.participant,
    exact.lenderSessionId ?? null,
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })
const identity = (overrides = {}) => ({
  sessionId: 'session-a',
  physicalUserMessageId: 'message-a',
  role: 'coder',
  participant: 'alice',
  target: target(),
  ...overrides,
})
const acquire = async (runtime, exact = identity()) => {
  const outcome = await routing.acquireExecutionAdmission(
    runtime,
    exact.sessionId,
    exact.physicalUserMessageId,
    exact.role,
    exact.participant,
    exact.lenderSessionId ?? null,
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
test('WHAT[EMR-012] rejects commit with the wrong role', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  conflict(routing.commitExecutionAdmission(runtime, lease, identity({ role: 'inspector' })))
  assert.equal(routing.snapshotOccupied(runtime).length, 1, 'wrong role cannot settle capacity')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'Applied' })
})
test('WHAT[EMR-012] rejects commit with the wrong participant', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  conflict(routing.commitExecutionAdmission(runtime, lease, identity({ participant: 'bob' })))
  assert.equal(routing.snapshotOccupied(runtime).length, 1, 'wrong participant cannot settle capacity')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'Applied' })
})
test('WHAT[EMR-012] rejects release from another physical message and every wrong exact identity field', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  for (const change of [
    { sessionId: 'other-session' },
    { physicalUserMessageId: 'other-message' },
    { role: 'inspector' },
    { participant: 'bob' },
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
}
