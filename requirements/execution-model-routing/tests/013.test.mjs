import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const {
  createRuntime,
  acquireExecutionAdmission,
  beginExecutionAdmission,
  awaitQueuedExecutionAdmission,
  executionAdmissionTarget,
  commitExecutionAdmission,
  releasePhysicalExecution,
  cancelPendingExecution,
  endProviderStep,
  pendingCount,
  snapshotOccupied,
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

test('WHAT[EMR-013] queue bound is enforced without drops', async () => {
  const runtime = createRuntime(() => null, { maxQueueLength: 2 })

  const first = await beginExecutionAdmission(runtime, 'ses-1', 'msg-1', 'coder', 'alice', null)
  const second = await beginExecutionAdmission(runtime, 'ses-2', 'msg-2', 'coder', 'bob', null)
  const third = await beginExecutionAdmission(runtime, 'ses-3', 'msg-3', 'coder', 'carol', null)

  assert.equal(first.kind, 'Queued')
  assert.equal(second.kind, 'Queued')
  assert.equal(third.kind, 'Rejected')
  assert.equal(third.rejection, 'QueueFull')

  cancelPendingExecution(runtime, 'ses-1')
  cancelPendingExecution(runtime, 'ses-2')
})

test('WHAT[EMR-013] FIFO admits the oldest scheduler-eligible demand on capacity release', async () => {
  let releaseSlot
  const free = target('coder/1')
  const runtime = createRuntime((_role, running) => (running.length === 0 ? free : null))

  const holder = await acquireExecutionAdmission(runtime, 'ses-holder', 'msg-0', 'coder', 'h', null)
  assert.equal(holder.kind, 'Acquired')

  const waiter1Promise = beginExecutionAdmission(runtime, 'ses-1', 'msg-1', 'coder', 'first', null)
  const waiter2Promise = beginExecutionAdmission(runtime, 'ses-2', 'msg-2', 'coder', 'second', null)
  const waiter1 = await waiter1Promise
  const waiter2 = await waiter2Promise

  assert.equal(waiter1.kind, 'Queued')
  assert.equal(waiter2.kind, 'Queued')

  const drained1 = awaitQueuedExecutionAdmission(waiter1.queue)
  const drained2 = awaitQueuedExecutionAdmission(waiter2.queue)

  releasePhysicalExecution(runtime, 'ses-holder', 'msg-0')

  const outcome1 = await drained1
  assert.equal(outcome1.kind, 'Acquired')

  cancelPendingExecution(runtime, 'ses-2')
  const outcome2 = await drained2
  assert.equal(outcome2.kind, 'Cancelled')
})

test('WHAT[EMR-013] exact cancel and supersede are typed and idempotent', async () => {
  const runtime = createRuntime(() => null)
  const waiter = await beginExecutionAdmission(runtime, 'ses-1', 'msg-1', 'coder', 'alice', null)
  assert.equal(waiter.kind, 'Queued')

  cancelPendingExecution(runtime, 'ses-1')
  cancelPendingExecution(runtime, 'ses-1')

  const outcome = await awaitQueuedExecutionAdmission(waiter.queue)
  assert.equal(outcome.kind, 'Cancelled')
})

test('WHAT[EMR-013] superseded slot freed inside the same turn recomputes the waiting queue', { timeout: 5000 }, async () => {
  const only = target('provider/only')
  const runningCounts = []
  const route = (_role, running) => {
    runningCounts.push(running.length)
    return running.length < 1 ? only : null
  }
  const runtime = createRuntime(route)

  await acquireManaged(runtime, 'session', 'msg-1', 'coder', 'alice')
  endProviderStep(runtime, 'session', 'msg-1', 'run-1')

  const retry = await acquireManaged(runtime, 'session', 'msg-2', 'coder', 'alice')
  assert.equal(retry.kind, 'Acquired', 'superseded occupancy reaches the first schedule, its retire frees the slot, and the queued demand drains in the same turn')
  assert.equal(key(retry.target), 'provider/only|none')
  assert.equal(pendingCount(runtime), 0)
  assert.equal(snapshotOccupied(runtime).length, 1)
  assert.deepEqual(runningCounts, [0, 1, 0])
})
