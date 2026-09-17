import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const target = (model = 'provider/model') => ({ model, reasoning: 'none' })
const begin = (runtime, session, physical, role, participant, lenderSessionId = null) =>
  routing.beginExecutionAdmission(runtime, session, physical, role, participant, lenderSessionId)
const awaitQueued = (outcome) => routing.awaitQueuedExecutionAdmission(outcome.queue)
const fill = async (runtime, count, prefix = 'waiting') => {
  const queued = []
  for (let index = 0; index < count; index += 1) {
    const outcome = await begin(runtime, `${prefix}-${index}`, `physical-${index}`, 'engineer', `${prefix}-owner-${index}`)
    assert.equal(outcome.kind, 'Queued')
    queued.push(outcome)
  }
  return queued
}

test('WHAT[EMR-013] queue bound is enforced without drops', async () => {
  const runtime = routing.createRuntime(() => null)
  const bound = routing.pendingBound(runtime)

  assert.equal(routing.pendingContractVersion(runtime), 1)
  assert.equal(bound, 32)
  const queued = await fill(runtime, bound)
  assert.equal(routing.pendingCount(runtime), bound)

  const full = await begin(runtime, 'overflow', 'physical-overflow', 'engineer', 'overflow-owner')
  assert.deepEqual(
    { kind: full.kind, failure: full.failure, lease: full.lease },
    { kind: 'QueueFull', failure: 'CapacityQueueFull', lease: null },
  )
  assert.equal(routing.pendingCount(runtime), bound)
  assert.deepEqual(routing.snapshotOccupied(runtime), [])

  for (let index = 0; index < queued.length; index += 1) {
    routing.cancelPendingExecution(runtime, `waiting-${index}`)
    assert.equal((await awaitQueued(queued[index])).kind, 'Cancelled')
  }
  assert.equal(routing.pendingCount(runtime), 0)
})
test('WHAT[EMR-013] FIFO admits the oldest scheduler-eligible demand on capacity release', async () => {
  const admitted = []
  const runtime = routing.createRuntime((role, running) => {
    if (running.some((item) => item.model === 'provider/one')) return null
    admitted.push(role)
    return target('provider/one')
  })

  const holder = await begin(runtime, 'holder', 'physical-holder', 'engineer', 'holder-owner')
  assert.equal(holder.kind, 'Acquired')
  const first = await begin(runtime, 'first', 'physical-first', 'manager', 'first-owner')
  const second = await begin(runtime, 'second', 'physical-second', 'devops', 'second-owner')
  assert.equal(first.kind, 'Queued')
  assert.equal(second.kind, 'Queued')

  routing.releasePhysicalExecution(runtime, 'holder', 'physical-holder')
  assert.equal((await awaitQueued(first)).kind, 'Acquired')
  assert.deepEqual(admitted, ['engineer', 'manager'])
  assert.equal(routing.pendingCount(runtime), 1)

  routing.releasePhysicalExecution(runtime, 'first', 'physical-first')
  assert.equal((await awaitQueued(second)).kind, 'Acquired')
  assert.deepEqual(admitted, ['engineer', 'manager', 'devops'])
})
test('WHAT[EMR-013] exact cancel and supersede are typed and idempotent', async () => {
  const runtime = routing.createRuntime(() => null)
  const cancelled = await begin(runtime, 'cancelled', 'physical-cancelled', 'engineer', 'cancelled-owner')
  routing.cancelPendingExecution(runtime, 'cancelled')
  routing.cancelPendingExecution(runtime, 'cancelled')
  const cancelledOutcome = await awaitQueued(cancelled)
  assert.equal(cancelledOutcome.kind, 'Cancelled')
  assert.equal(cancelledOutcome.failure, 'UserCancelled')

  const old = await begin(runtime, 'generation', 'physical-old', 'manager', 'generation-owner')
  const fresh = await begin(runtime, 'generation', 'physical-new', 'manager', 'generation-owner')
  const duplicateFresh = await begin(runtime, 'generation', 'physical-new', 'manager', 'generation-owner')
  assert.equal((await awaitQueued(old)).kind, 'Superseded')
  assert.equal(fresh.kind, 'Queued')
  assert.equal(duplicateFresh.kind, 'Queued')
  assert.equal(routing.pendingCount(runtime), 1)

  routing.cancelPendingExecution(runtime, 'generation')
  assert.equal((await awaitQueued(fresh)).kind, 'Cancelled')
  assert.equal((await awaitQueued(duplicateFresh)).kind, 'Cancelled')
  assert.equal(routing.pendingCount(runtime), 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const {
  createRuntime,
  acquireExecutionAdmission,
  beginExecutionAdmission,
  awaitQueuedExecutionAdmission,
  executionAdmissionTarget,
  commitExecutionAdmission,
  tryReserveManaged,
  tryLease,
  releasePhysicalExecution,
  cancelPendingExecution,
  enterProviderStep,
  endProviderStep,
  takeProviderRunTarget,
  suppressProviderStep,
  snapshotOccupied,
  capacitySnapshot,
  pendingCount,
} = routing
const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })
const key = (value) => `${value.model}|${value.reasoning}`
const acquireManaged = async (runtime, sessionId, physicalUserMessageId, role, participant, lenderSessionId = null) => {
  const acquisition = await acquireExecutionAdmission(
    runtime,
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    lenderSessionId,
  )
  if (acquisition.kind !== 'Acquired') return { kind: acquisition.kind, target: null }

  const projected = executionAdmissionTarget(runtime, acquisition.lease)
  const observed = {
    sessionId,
    physicalUserMessageId,
    role,
    participant,
    target: projected,
  }
  const settlement = commitExecutionAdmission(runtime, acquisition.lease, observed)
  assert.ok(['Applied', 'AlreadyApplied'].includes(settlement.kind))
  return { kind: 'Acquired', target: projected }
}
const acquireTarget = async (...args) => {
  const outcome = await acquireManaged(...args)
  assert.equal(outcome.kind, 'Acquired')
  return outcome.target
}
const provider = (model) => model.slice(0, model.indexOf('/'))
const providerLimited = (limits, routes) => (role, running, previous) => {
  const candidates = routes[role] ?? []
  const count = (name) => running.filter((item) => provider(item.model) === name).length
  const available = (candidate) => count(provider(candidate.model)) < (limits[provider(candidate.model)] ?? 0)
  if (previous && candidates.some((candidate) => key(candidate) === key(previous)) && available(previous)) return previous
  return candidates.find(available) ?? null
}

test('WHAT[EMR-013] superseded slot freed inside the same turn recomputes the waiting queue', { timeout: 5000 }, async () => {
  const only = target('provider/only')
  const runningCounts = []
  const route = (_role, running) => {
    runningCounts.push(running.length)
    return running.length < 1 ? only : null
  }
  const runtime = createRuntime(route)

  await acquireTarget(runtime, 'session', 'msg-1', 'engineer', 'alice')
  endProviderStep(runtime, 'session', 'msg-1', 'run-1')

  const retry = await acquireManaged(runtime, 'session', 'msg-2', 'engineer', 'alice')
  assert.equal(retry.kind, 'Acquired', 'superseded occupancy reaches the first schedule, its retire frees the slot, and the queued demand drains in the same turn')
  assert.equal(key(retry.target), 'provider/only|none')
  assert.equal(pendingCount(runtime), 0)
  assert.equal(snapshotOccupied(runtime).length, 1)
  assert.deepEqual(runningCounts, [0, 1, 0])
})
}
