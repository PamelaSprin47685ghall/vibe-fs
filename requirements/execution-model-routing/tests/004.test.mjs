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
  tryReserveManaged,
  tryLease,
  releasePhysicalExecution,
  cancelPendingExecution,
  snapshotOccupied,
  pendingCount,
  createSdkClientPort,
  sendPrompt,
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

const promptOptions = (overrides = {}) => ({
  model: undefined,
  agent: undefined,
  directory: undefined,
  metadata: undefined,
  tools: undefined,
  bindingIntent: 'Preserve',
  ...overrides,
})

test('WHAT[EMR-004] an ineligible head does not block a later eligible demand', async () => {
  const runtime = createRuntime((role) => (role === 'coder' ? target('coder/1') : null))
  const blocked = await beginExecutionAdmission(runtime, 'ses-blocked', 'msg-1', 'inspector', 'alice', null)
  assert.equal(blocked.kind, 'Queued')

  const eligible = await acquireExecutionAdmission(runtime, 'ses-eligible', 'msg-2', 'coder', 'bob', null)
  assert.equal(eligible.kind, 'Acquired')
  cancelPendingExecution(runtime, 'ses-blocked')
})

test('WHAT[EMR-004] EMR_004_required_null_waits_for_an_occupancy_event_then_retries', async () => {
  const route = (_role, running) => running.filter((item) => item.model === 'provider/only').length < 1
    ? target('provider/only')
    : null
  const runtime = createRuntime(route)

  await acquireTarget(runtime, 'holder', 'msg-holder', 'coder', 'alice')
  let settled = false
  const waiting = acquireTarget(runtime, 'waiter', 'msg-waiter', 'coder', 'bob').then((value) => {
    settled = true
    return value
  })
  await Promise.resolve()

  assert.equal(settled, false)
  assert.equal(pendingCount(runtime), 1)

  releasePhysicalExecution(runtime, 'holder', 'msg-holder')
  assert.equal(key(await waiting), 'provider/only|none')
  assert.equal(pendingCount(runtime), 0)
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-004] EMR_004_newer_physical_message_cancels_superseded_pending_demand', async () => {
  const runtime = createRuntime((role) => role === 'inspector' ? null : target(`provider/${role}`))

  const old = acquireManaged(runtime, 'same-session', 'msg-old', 'inspector', 'alice')
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  const fresh = await acquireTarget(runtime, 'same-session', 'msg-new', 'coder', 'alice')
  assert.equal(fresh.model, 'provider/coder')
  const oldOutcome = await old
  assert.equal(oldOutcome.kind, 'Superseded')
  assert.equal(oldOutcome.target, null)
  assert.equal(pendingCount(runtime), 0)
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-004] EMR_004_an_earlier_null_waiter_does_not_head_of_line_block_another_role', async () => {
  const runtime = createRuntime((role) => role === 'inspector' ? null : target(`provider/${role}`))

  const blocked = acquireManaged(runtime, 'blocked-session', 'msg-blocked', 'inspector', 'alice')
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  const free = await acquireTarget(runtime, 'free-session', 'msg-free', 'coder', 'bob')
  assert.equal(key(free), 'provider/coder|none')
  assert.equal(pendingCount(runtime), 1)

  cancelPendingExecution(runtime, 'blocked-session')
  const blockedOutcome = await blocked
  assert.equal(blockedOutcome.kind, 'Cancelled')
  assert.equal(blockedOutcome.target, null)
})

test('WHAT[EMR-004] EMR_004_optional_null_is_k0_not_a_pending_demand', () => {
  const runtime = createRuntime(() => null)
  assert.equal(tryReserveManaged(runtime, 'replica', 'coder', null), null)
  assert.equal(pendingCount(runtime), 0)
  assert.deepEqual(snapshotOccupied(runtime), [])
})

test('WHAT[EMR-004] EMR_004_strength_reservation_is_adopted_by_chat_message_without_double_counting', async () => {
  let calls = 0
  const runtime = createRuntime(() => {
    calls += 1
    return target('provider/replica')
  })

  const reserved = tryReserveManaged(runtime, 'replica', 'coder', null)
  assert.equal(key(reserved), 'provider/replica|none')
  assert.equal(snapshotOccupied(runtime).length, 1)

  const adopted = await acquireTarget(runtime, 'replica', 'msg-replica', 'coder', 'alice')
  assert.equal(key(adopted), 'provider/replica|none')
  assert.equal(calls, 1, 'physical acceptance adopts the reservation without another scheduler decision')
  assert.equal(snapshotOccupied(runtime).length, 1, 'reservation and physical execution are one capacity occurrence')
  assert.equal(key(tryLease(runtime, 'replica', 'msg-replica', 'coder', 'alice', null)), 'provider/replica|none')
})

test('WHAT[EMR-004] EMR_004_reservation_adoption_binds_role_and_participant', async () => {
  const runtime = createRuntime(() => target('provider/replica'))
  tryReserveManaged(runtime, 'replica', 'coder', null)

  await acquireTarget(runtime, 'replica', 'msg-replica', 'coder', 'alice')
  await assert.rejects(
    acquireTarget(runtime, 'replica', 'msg-replica', 'coder', 'bob'),
    /physical execution .* changed participant/i,
    'the first adopter binds the participant for that exact physical execution',
  )

  const rerouted = createRuntime(() => target('provider/replica'))
  tryReserveManaged(rerouted, 'replica', 'coder', null)
  await assert.rejects(
    acquireTarget(rerouted, 'replica', 'msg-replica', 'inspector', 'alice'),
    /physical execution .* changed role/i,
    'a reservation carries its Role until adoption',
  )
})

test('WHAT[EMR-004] EMR_004_sdk_prompt_async_awaits_host_enqueue_and_surfaces_enqueue_rejection', async () => {
  let releaseHost
  let rejectHost
  let invoked = false
  const hostRun = new Promise((resolve) => {
    releaseHost = resolve
  })
  const failingRun = new Promise((_, reject) => { rejectHost = reject })
  const client = {
    session: {
      promptAsync: () => {
        invoked = true
        return hostRun
      },
    },
  }
  const port = createSdkClientPort(client)

  const sending = sendPrompt(
    port,
    'session-detached',
    'start child work',
    promptOptions({ agent: 'devops' }),
  )
  releaseHost({})
  const result = await sending
  assert.equal(invoked, true, 'the Host enqueue API is invoked')
  assert.match(JSON.stringify(result), /AdmittedWithReceipt/i)

  const failingPort = createSdkClientPort({ session: { promptAsync: () => failingRun } })
  rejectHost(new Error('Host enqueue refused'))
  const failed = await sendPrompt(failingPort, 'session-failing', 'refused work', promptOptions({ agent: 'devops' }))
  assert.match(JSON.stringify(failed), /Fatal|refused/i)
})
