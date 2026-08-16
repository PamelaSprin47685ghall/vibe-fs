import assert from 'node:assert/strict'
import test from 'node:test'

const routing = await import('../../../dist/OpenCode/Host/ModelRouting.js')
const {
  ModelRouting_ModelRoutingRuntime: ModelRoutingRuntime,
  ModelRouting_ModelRoutingRuntime__AcquireManagedExecution_30230F9B: acquireManaged,
  ModelRouting_ModelRoutingRuntime__TryReserveManaged_Z384F8060: tryReserveManaged,
  ModelRouting_ModelRoutingRuntime__TryLease_30230F9B: tryLease,
  ModelRouting_ModelRoutingRuntime__ReleaseExecution_Z721C83C5: releaseExecution,
  ModelRouting_ModelRoutingRuntime__CancelPendingExecution_Z721C83C5: cancelPendingExecution,
  ModelRouting_ModelRoutingRuntime__SnapshotOccupied: snapshotOccupied,
  ModelRouting_ModelRoutingRuntime__get_PendingCount: pendingCount,
} = routing

const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })
const key = (value) => `${value.Model}|${value.Reasoning}`

test('WHAT[EMR-003] EMR_003_each_active_physical_execution_contributes_one_running_occurrence', async () => {
  const runtime = new ModelRoutingRuntime(() => target())

  const first = await acquireManaged(runtime, 'session-a', 'msg-a', 'fast-coder')
  const same = await acquireManaged(runtime, 'session-a', 'msg-a', 'fast-coder')
  const otherExecution = await acquireManaged(runtime, 'session-b', 'msg-b', 'deep-coder')

  assert.equal(key(first), 'provider/shared|none')
  assert.equal(key(same), 'provider/shared|none')
  assert.equal(key(otherExecution), 'provider/shared|none')
  assert.equal(snapshotOccupied(runtime).length, 2, 'two physical executions contribute two occurrences even on one target')
})

test('WHAT[EMR-006] EMR_006_same_physical_message_retry_reuses_target_without_scheduler_rerun', async () => {
  const seen = []
  const runtime = new ModelRoutingRuntime((_role, running) => {
    seen.push(running.map((item) => `${item.model}|${item.reasoning}`))
    return target()
  })

  await acquireManaged(runtime, 'session-a', 'msg-1', 'fast-coder')
  await acquireManaged(runtime, 'session-a', 'msg-1', 'fast-coder')
  await acquireManaged(runtime, 'session-a', 'msg-2', 'deep-coder')

  assert.deepEqual(seen, [[], []], 'same physical material reuses; newer material supersedes and schedules fresh')
})

test('WHAT[EMR-006] EMR_006_new_physical_message_supersedes_old_A_B_occupancy_without_idle', async () => {
  const runtime = new ModelRoutingRuntime((role) => target(`provider/${role}`))

  const a = await acquireManaged(runtime, 'session', 'msg-a', 'fast-coder')
  assert.equal(a.Model, 'provider/fast-coder')
  assert.equal(snapshotOccupied(runtime).length, 1)

  const b = await acquireManaged(runtime, 'session', 'msg-b', 'deep-coder')
  assert.equal(b.Model, 'provider/deep-coder')
  assert.equal(snapshotOccupied(runtime).length, 1, 'one reusable session can own only one current physical execution slot')
  assert.equal(tryLease(runtime, 'session', 'msg-a', 'fast-coder'), undefined, 'superseded physical material no longer owns a lease')
  assert.equal(key(tryLease(runtime, 'session', 'msg-b', 'deep-coder')), 'provider/deep-coder|none')
})

test('WHAT[EMR-006] EMR_006_same_physical_message_cannot_change_effective_agent', async () => {
  const runtime = new ModelRoutingRuntime(() => target())
  await acquireManaged(runtime, 'session', 'msg-1', 'fast-coder')

  await assert.rejects(
    acquireManaged(runtime, 'session', 'msg-1', 'deep-coder'),
    /physical execution .* changed agent/i,
  )
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-004] EMR_004_required_null_waits_for_an_occupancy_event_then_retries', async () => {
  const route = (_role, running) => running.filter((item) => item.model === 'provider/only').length < 1
    ? target('provider/only')
    : null
  const runtime = new ModelRoutingRuntime(route)

  await acquireManaged(runtime, 'holder', 'msg-holder', 'fast-coder')
  let settled = false
  const waiting = acquireManaged(runtime, 'waiter', 'msg-waiter', 'fast-coder').then((value) => {
    settled = true
    return value
  })
  await Promise.resolve()

  assert.equal(settled, false)
  assert.equal(pendingCount(runtime), 1)

  releaseExecution(runtime, 'holder')
  assert.equal(key(await waiting), 'provider/only|none')
  assert.equal(pendingCount(runtime), 0)
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-004] EMR_004_newer_physical_message_cancels_superseded_pending_demand', async () => {
  const runtime = new ModelRoutingRuntime((role) => role === 'blocked' ? null : target(`provider/${role}`))

  const old = acquireManaged(runtime, 'same-session', 'msg-old', 'blocked')
  const oldRejected = assert.rejects(old)
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  const fresh = await acquireManaged(runtime, 'same-session', 'msg-new', 'free')
  assert.equal(fresh.Model, 'provider/free')
  await oldRejected
  assert.equal(pendingCount(runtime), 0)
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-004] EMR_004_an_earlier_null_waiter_does_not_head_of_line_block_another_role', async () => {
  const runtime = new ModelRoutingRuntime((role) => role === 'blocked' ? null : target(`provider/${role}`))

  const blocked = acquireManaged(runtime, 'blocked-session', 'msg-blocked', 'blocked')
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  const free = await acquireManaged(runtime, 'free-session', 'msg-free', 'free')
  assert.equal(key(free), 'provider/free|none')
  assert.equal(pendingCount(runtime), 1)

  cancelPendingExecution(runtime, 'blocked-session')
  await assert.rejects(blocked)
})

test('WHAT[EMR-004] EMR_004_optional_null_is_k0_not_a_pending_demand', () => {
  const runtime = new ModelRoutingRuntime(() => null)
  assert.equal(tryReserveManaged(runtime, 'replica', 'fast-coder'), undefined)
  assert.equal(pendingCount(runtime), 0)
  assert.deepEqual(snapshotOccupied(runtime), [])
})

test('WHAT[EMR-004] EMR_004_strength_reservation_is_adopted_by_chat_message_without_double_counting', async () => {
  let calls = 0
  const runtime = new ModelRoutingRuntime(() => {
    calls += 1
    return target('provider/replica')
  })

  const reserved = tryReserveManaged(runtime, 'replica', 'fast-coder')
  assert.equal(key(reserved), 'provider/replica|none')
  assert.equal(snapshotOccupied(runtime).length, 1)

  const adopted = await acquireManaged(runtime, 'replica', 'msg-replica', 'fast-coder')
  assert.equal(key(adopted), 'provider/replica|none')
  assert.equal(calls, 1, 'physical acceptance adopts the reservation without another scheduler decision')
  assert.equal(snapshotOccupied(runtime).length, 1, 'reservation and physical execution are one capacity occurrence')
  assert.equal(key(tryLease(runtime, 'replica', 'msg-replica', 'fast-coder')), 'provider/replica|none')
})

test('WHAT[EMR-006] EMR_006_lease_is_stable_only_for_one_physical_user_material', async () => {
  let calls = 0
  const runtime = new ModelRoutingRuntime(() => target(`provider/model-${++calls}`, 'low'))

  const first = await acquireManaged(runtime, 'session', 'msg-1', 'deep-coder')
  const retry = await acquireManaged(runtime, 'session', 'msg-1', 'deep-coder')

  assert.equal(first.Model, 'provider/model-1')
  assert.equal(retry.Model, 'provider/model-1')
  assert.equal(calls, 1)

  const nextMaterial = await acquireManaged(runtime, 'session', 'msg-2', 'deep-coder')
  assert.equal(nextMaterial.Model, 'provider/model-2', 'new physical material gets a fresh lease even without idle')
  assert.equal(calls, 2)
})

test('WHAT[EMR-007] EMR_007_execution_release_is_idempotent_and_wakes_waiters_once', async () => {
  const runtime = new ModelRoutingRuntime((_role, running) => running.length === 0 ? target('provider/one') : null)
  await acquireManaged(runtime, 'holder', 'msg-holder', 'fast-coder')
  const waiting = acquireManaged(runtime, 'waiter', 'msg-waiter', 'deep-coder')

  releaseExecution(runtime, 'holder')
  const acquired = await waiting
  assert.equal(acquired.Model, 'provider/one')
  assert.equal(snapshotOccupied(runtime).length, 1)

  releaseExecution(runtime, 'holder')
  assert.equal(snapshotOccupied(runtime).length, 1, 'second release cannot remove somebody else\'s execution')
})

test('WHAT[EMR-002] EMR_002_scheduler_program_error_poisons_pending_and_future_demands', async () => {
  const runtime = new ModelRoutingRuntime((role) => {
    if (role === 'waiting') return null
    throw new Error('bad scheduler program')
  })

  const waiting = acquireManaged(runtime, 'waiter', 'msg-waiting', 'waiting')
  const waitingRejected = assert.rejects(waiting, /bad scheduler program/)
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  await assert.rejects(acquireManaged(runtime, 'boom', 'msg-boom', 'boom-role'), /bad scheduler program/)
  await waitingRejected
  await assert.rejects(acquireManaged(runtime, 'later', 'msg-later', 'waiting'), /bad scheduler program/)
  assert.equal(pendingCount(runtime), 0)
})
