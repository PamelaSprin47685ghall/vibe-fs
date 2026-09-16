import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const target = (model = 'provider/model') => ({ model, reasoning: 'none' })
const begin = (runtime, session, physical, role, participant, lenderSessionId = null) =>
  routing.beginExecutionAdmission(runtime, session, physical, role, participant, lenderSessionId)
const awaitQueued = (outcome) => routing.awaitQueuedExecutionAdmission(outcome.queue)

const fill = async (runtime, count, prefix = 'waiting') => {
  const queued = []
  for (let index = 0; index < count; index += 1) {
    const outcome = await begin(runtime, `${prefix}-${index}`, `physical-${index}`, 'coder', `${prefix}-owner-${index}`)
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

  const full = await begin(runtime, 'overflow', 'physical-overflow', 'coder', 'overflow-owner')
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

  const holder = await begin(runtime, 'holder', 'physical-holder', 'coder', 'holder-owner')
  assert.equal(holder.kind, 'Acquired')
  const first = await begin(runtime, 'first', 'physical-first', 'manager', 'first-owner')
  const second = await begin(runtime, 'second', 'physical-second', 'inspector', 'second-owner')
  assert.equal(first.kind, 'Queued')
  assert.equal(second.kind, 'Queued')

  routing.releasePhysicalExecution(runtime, 'holder', 'physical-holder')
  assert.equal((await awaitQueued(first)).kind, 'Acquired')
  assert.deepEqual(admitted, ['coder', 'manager'])
  assert.equal(routing.pendingCount(runtime), 1)

  routing.releasePhysicalExecution(runtime, 'first', 'physical-first')
  assert.equal((await awaitQueued(second)).kind, 'Acquired')
  assert.deepEqual(admitted, ['coder', 'manager', 'inspector'])
})

test('WHAT[EMR-004] an ineligible head does not block a later eligible demand', async () => {
  let freeEnabled = false
  const runtime = routing.createRuntime((role) => {
    if (role === 'devops') return target('provider/trigger')
    if (role === 'browser' && freeEnabled) return target('provider/free')
    return null
  })

  const blocked = await begin(runtime, 'blocked-session', 'physical-blocked', 'inquiry', 'blocked-owner')
  const free = await begin(runtime, 'free-session', 'physical-free', 'browser', 'free-owner')
  assert.equal(blocked.kind, 'Queued')
  assert.equal(free.kind, 'Queued')

  freeEnabled = true
  assert.equal((await begin(runtime, 'trigger', 'physical-trigger', 'devops', 'trigger-owner')).kind, 'Acquired')
  assert.equal((await awaitQueued(free)).kind, 'Acquired')
  assert.equal(routing.pendingCount(runtime), 1)

  routing.cancelPendingExecution(runtime, 'blocked-session')
  assert.equal((await awaitQueued(blocked)).kind, 'Cancelled')
})

test('WHAT[EMR-013] exact cancel and supersede are typed and idempotent', async () => {
  const runtime = routing.createRuntime(() => null)
  const cancelled = await begin(runtime, 'cancelled', 'physical-cancelled', 'coder', 'cancelled-owner')
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
