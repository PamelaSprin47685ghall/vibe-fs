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

test('WHAT[execution-model-routing-012] capacity lifecycle admits every legal fenced transition', async () => {
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
test('WHAT[execution-model-routing-012] capacity lifecycle rejects every illegal edge and opposite terminal', async () => {
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
test('WHAT[execution-model-routing-012] same physical retry preserves the capability and a newer generation stales it', async () => {
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
  role: 'engineer',
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

test('WHAT[execution-model-routing-012] admission lease is opaque and projects only its frozen target', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  assert.deepEqual(routing.executionAdmissionTarget(runtime, lease), target())
  assert.throws(
    () => JSON.stringify(lease),
    /ExecutionAdmissionToken is process-local and cannot be serialized/,
    'the live process capability must refuse native JSON serialization',
  )
  assert.deepEqual(routing.commitExecutionAdmission(runtime, {}, identity()), { kind: 'StaleFence' })
})
test('WHAT[execution-model-routing-012] admission lease permits one terminal transition and idempotent duplicate', async () => {
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
test('WHAT[execution-model-routing-012] rejects commit with the wrong role', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  conflict(routing.commitExecutionAdmission(runtime, lease, identity({ role: 'devops' })))
  assert.equal(routing.snapshotOccupied(runtime).length, 1, 'wrong role cannot settle capacity')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'Applied' })
})
test('WHAT[execution-model-routing-012] rejects commit with the wrong participant', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  conflict(routing.commitExecutionAdmission(runtime, lease, identity({ participant: 'bob' })))
  assert.equal(routing.snapshotOccupied(runtime).length, 1, 'wrong participant cannot settle capacity')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'Applied' })
})
test('WHAT[execution-model-routing-012] rejects release from another physical message and every wrong exact identity field', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  for (const change of [
    { sessionId: 'other-session' },
    { physicalUserMessageId: 'other-message' },
    { role: 'devops' },
    { participant: 'bob' },
    { target: { model: 'provider/other', reasoning: 'none' } },
  ]) {
    conflict(routing.releaseExecutionAdmissionBeforeProvider(runtime, lease, identity(change)))
  }

  assert.equal(routing.snapshotOccupied(runtime).length, 1, 'wrong identity cannot release capacity')
})
test('WHAT[execution-model-routing-012] same physical retry reuses capability while newer material stales it', async () => {
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
test('WHAT[execution-model-routing-012] a lease from another runtime is rejected as the wrong capacity fence', async () => {
  const firstRuntime = routing.createRuntime(() => target())
  const secondRuntime = routing.createRuntime(() => target())
  const lease = await acquire(firstRuntime)

  assert.deepEqual(routing.commitExecutionAdmission(secondRuntime, lease, identity()), { kind: 'StaleFence' })
  assert.equal(routing.snapshotOccupied(firstRuntime).length, 1)
  assert.equal(routing.snapshotOccupied(secondRuntime).length, 0)
})
test('WHAT[execution-model-routing-012] EMR_012_try_read_execution_returns_committed_lease_token', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  assert.equal(routing.tryReadExecution(runtime, 'session-a', 'message-a'), null, 'a pending lease is not yet executable')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'Applied' })

  const observed = routing.tryReadExecution(runtime, 'session-a', 'message-a')
  assert.equal(observed, lease)
  assert.equal(routing.executionAdmissionLifecycle(runtime, observed), 'Committed')
  assert.deepEqual(routing.executionAdmissionTarget(runtime, observed), target())
  assert.equal(routing.tryReadExecution(runtime, 'session-a', 'message-a'), observed)
})
test('WHAT[execution-model-routing-012] EMR_012_try_read_execution_misses_unknown_and_blank_ids', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'Applied' })

  for (const [sessionId, physicalUserMessageId] of [
    ['unknown-session', 'message-a'],
    ['session-a', 'unknown-message'],
    ['', 'message-a'],
    ['session-a', ''],
    ['   ', 'message-a'],
    ['session-a', '   '],
  ]) {
    assert.equal(routing.tryReadExecution(runtime, sessionId, physicalUserMessageId), null)
  }
})
test('WHAT[execution-model-routing-012] EMR_012_try_read_execution_pending_returns_null', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)

  assert.equal(routing.tryReadExecution(runtime, 'session-a', 'message-a'), null, 'Pending owns no executable lease')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'Applied' })
  assert.equal(routing.tryReadExecution(runtime, 'session-a', 'message-a'), lease)
})
test('WHAT[execution-model-routing-012] EMR_012_try_read_execution_released_returns_null', async () => {
  const beforeProvider = routing.createRuntime(() => target())
  const released = await acquire(beforeProvider)
  assert.deepEqual(
    routing.releaseExecutionAdmissionBeforeProvider(beforeProvider, released, identity()),
    { kind: 'Applied' },
  )
  assert.equal(routing.tryReadExecution(beforeProvider, 'session-a', 'message-a'), null)

  const provider = routing.createRuntime(() => target())
  const committed = await acquire(provider)
  assert.deepEqual(routing.commitExecutionAdmission(provider, committed, identity()), { kind: 'Applied' })
  assert.equal(routing.tryReadExecution(provider, 'session-a', 'message-a'), committed)
  routing.releasePhysicalExecution(provider, 'session-a', 'message-a')
  assert.equal(routing.tryReadExecution(provider, 'session-a', 'message-a'), null)
})
test('WHAT[execution-model-routing-012] EMR_012_try_read_execution_wrong_physical_and_superseded_generation', async () => {
  const runtime = routing.createRuntime(() => target())
  const first = await acquire(runtime)
  const newerIdentity = identity({ physicalUserMessageId: 'message-b' })
  const second = await acquire(runtime, newerIdentity)

  assert.notEqual(first, second)
  assert.equal(routing.tryReadExecution(runtime, 'session-a', 'message-a'), null, 'a superseded generation is not executable')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, first, identity()), { kind: 'StaleFence' })

  assert.deepEqual(routing.commitExecutionAdmission(runtime, second, newerIdentity), { kind: 'Applied' })
  const observed = routing.tryReadExecution(runtime, 'session-a', 'message-b')
  assert.equal(observed, second)
  assert.deepEqual(routing.executionAdmissionTarget(runtime, observed), target())
})
test('WHAT[execution-model-routing-012] EMR_012_try_read_execution_does_not_upgrade_strength_reservation', async () => {
  const runtime = routing.createRuntime(() => target())
  assert.deepEqual(routing.tryReserveManaged(runtime, 'session-a', 'engineer', null), target())

  const before = routing.capacitySnapshot(runtime)
  assert.equal(routing.tryReadExecution(runtime, 'session-a', 'message-a'), null, 'a reservation is not a physical execution')
  assert.deepEqual(routing.capacitySnapshot(runtime), before, 'the query neither adopts nor consumes the reservation')

  assert.deepEqual(routing.tryLease(runtime, 'session-a', 'message-a', 'engineer', 'alice', null), target())
})
test('WHAT[execution-model-routing-012] EMR_012_try_read_execution_is_read_only', async () => {
  let scheduled = 0
  const runtime = routing.createRuntime(() => {
    scheduled += 1
    return target()
  })
  const lease = await acquire(runtime)
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'Applied' })
  const settled = scheduled

  const before = routing.capacitySnapshot(runtime)
  const pending = routing.pendingCount(runtime)

  for (let index = 0; index < 5; index += 1) {
    assert.equal(routing.tryReadExecution(runtime, 'session-a', 'message-a'), lease)
    assert.equal(routing.tryReadExecution(runtime, 'session-a', 'unknown-message'), null)
    assert.equal(routing.tryReadExecution(runtime, '', 'message-a'), null)
  }

  const after = routing.capacitySnapshot(runtime)
  assert.deepEqual(after.counters, before.counters)
  assert.deepEqual(after.tokenStateCounts, before.tokenStateCounts)
  assert.deepEqual(after, before)
  assert.deepEqual(routing.reconcileCapacityEvidence(after), { kind: 'NoOp' })
  assert.equal(routing.pendingCount(runtime), pending)
  assert.equal(scheduled, settled, 'read-only queries never invoke the scheduler')
})

test('WHAT[execution-model-routing-012] fabricated fences and foreign custody fail closed at every settlement API', async () => {
  const runtime = routing.createRuntime(() => target())
  const lease = await acquire(runtime)
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'Applied' })

  // A fabricated fence never carries the exact capacity identity: every
  // settlement API must fail closed with StaleFence, not mutate the ledger.
  const fabricated = [null, undefined, {}, 'fence-string', 42, { fence: 'x' }, { sessionId: 'session-a' }]
  for (const fake of fabricated) {
    assert.deepEqual(
      routing.commitExecutionAdmission(runtime, fake, identity()),
      { kind: 'StaleFence' },
      `commit must reject fabricated fence ${JSON.stringify(fake) ?? String(fake)}`,
    )
    assert.deepEqual(
      routing.releaseExecutionAdmissionBeforeProvider(runtime, fake, identity()),
      { kind: 'StaleFence' },
      `release must reject fabricated fence ${JSON.stringify(fake) ?? String(fake)}`,
    )
  }

  // Direct foreign custody construction: a shared-custody token minted
  // outside the owner (plain object, null, primitive) is equally fenced out.
  for (const fake of [null, {}, { token: 'x' }, 'str']) {
    assert.deepEqual(
      routing.commitSharedExecutionAdmission(fake, identity()),
      { kind: 'StaleFence' },
      `shared commit must reject foreign custody ${JSON.stringify(fake) ?? String(fake)}`,
    )
    assert.deepEqual(
      routing.releaseSharedExecutionAdmissionBeforeProvider(fake, identity()),
      { kind: 'StaleFence' },
      `shared release must reject foreign custody ${JSON.stringify(fake) ?? String(fake)}`,
    )
  }

  // The real lease stays settled exactly once; no fabricated call disturbed it.
  assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, identity()), { kind: 'AlreadyApplied' })
})

test.todo('WHAT[execution-model-routing-012] compiler rejects fabricated exact opaque capacity fences and direct foreign custody construction (GAP-128: F# type-level opacity proof pending — the runtime fail-closed test above is retained but does not prove compile-time rejection)')
}
