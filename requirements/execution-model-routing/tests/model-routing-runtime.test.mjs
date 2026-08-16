import assert from 'node:assert/strict'
import test from 'node:test'

const routing = await import('../../../dist/OpenCode/Host/ModelRouting.js')
const {
  ModelRouting_ModelRoutingRuntime: ModelRoutingRuntime,
  ModelRouting_ModelRoutingRuntime__AcquireManaged_Z384F8060: acquireManaged,
  ModelRouting_ModelRoutingRuntime__TryAcquireManaged_Z384F8060: tryAcquireManaged,
  ModelRouting_ModelRoutingRuntime__ReleaseSession_Z721C83C5: releaseSession,
  ModelRouting_ModelRoutingRuntime__CancelPendingSession_Z721C83C5: cancelPendingSession,
  ModelRouting_ModelRoutingRuntime__SnapshotOccupied: snapshotOccupied,
  ModelRouting_ModelRoutingRuntime__get_PendingCount: pendingCount,
} = routing

const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })
const key = (value) => `${value.Model}|${value.Reasoning}`

test('WHAT[EMR-003] EMR_003_each_session_agent_lease_contributes_one_running_occurrence', async () => {
  const runtime = new ModelRoutingRuntime(() => target())

  const first = await acquireManaged(runtime, 'session-a', 'fast-coder')
  const same = await acquireManaged(runtime, 'session-a', 'fast-coder')
  const peer = await acquireManaged(runtime, 'session-a', 'deep-coder')

  assert.equal(key(first), 'provider/shared|none')
  assert.equal(key(same), 'provider/shared|none')
  assert.equal(key(peer), 'provider/shared|none')
  assert.equal(snapshotOccupied(runtime).length, 2, 'A and B are two leases even on the same target')
})

test('WHAT[EMR-006] EMR_006_reusing_one_lease_does_not_rerun_the_scheduler', async () => {
  const seen = []
  const runtime = new ModelRoutingRuntime((_role, running) => {
    seen.push(running.map((item) => `${item.model}|${item.reasoning}`))
    return target()
  })

  await acquireManaged(runtime, 'session-a', 'fast-coder')
  await acquireManaged(runtime, 'session-a', 'fast-coder')
  await acquireManaged(runtime, 'session-a', 'deep-coder')

  assert.deepEqual(seen, [[], ['provider/shared|none']], 'reusing one lease must not re-run the scheduler')
})

test('WHAT[EMR-004] EMR_004_required_null_waits_for_an_occupancy_event_then_retries', async () => {
  const route = (_role, running) => running.filter((item) => item.model === 'provider/only').length < 1
    ? target('provider/only')
    : null
  const runtime = new ModelRoutingRuntime(route)

  await acquireManaged(runtime, 'holder', 'fast-coder')
  let settled = false
  const waiting = acquireManaged(runtime, 'waiter', 'fast-coder').then((value) => {
    settled = true
    return value
  })
  await Promise.resolve()

  assert.equal(settled, false)
  assert.equal(pendingCount(runtime), 1)

  releaseSession(runtime, 'holder')
  assert.equal(key(await waiting), 'provider/only|none')
  assert.equal(pendingCount(runtime), 0)
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-004] EMR_004_an_earlier_null_waiter_does_not_head_of_line_block_another_role', async () => {
  const runtime = new ModelRoutingRuntime((role) => role === 'blocked' ? null : target(`provider/${role}`))

  const blocked = acquireManaged(runtime, 'blocked-session', 'blocked')
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  const free = await acquireManaged(runtime, 'free-session', 'free')
  assert.equal(key(free), 'provider/free|none')
  assert.equal(pendingCount(runtime), 1)

  cancelPendingSession(runtime, 'blocked-session')
  await assert.rejects(blocked)
})

test('WHAT[EMR-004] EMR_004_optional_null_is_k0_not_a_pending_demand', () => {
  const runtime = new ModelRoutingRuntime(() => null)
  assert.equal(tryAcquireManaged(runtime, 'replica', 'fast-coder'), undefined)
  assert.equal(pendingCount(runtime), 0)
  assert.deepEqual(snapshotOccupied(runtime), [])
})

test('WHAT[EMR-006] EMR_006_lease_is_stable_even_when_scheduler_would_choose_differently_later', async () => {
  let calls = 0
  const runtime = new ModelRoutingRuntime(() => target(`provider/model-${++calls}`, 'low'))

  const first = await acquireManaged(runtime, 'session', 'deep-coder')
  const second = await acquireManaged(runtime, 'session', 'deep-coder')

  assert.equal(first.Model, 'provider/model-1')
  assert.equal(second.Model, 'provider/model-1')
  assert.equal(calls, 1)
})

test('WHAT[EMR-007] EMR_007_release_is_idempotent_and_wakes_waiters_once', async () => {
  const runtime = new ModelRoutingRuntime((_role, running) => running.length === 0 ? target('provider/one') : null)
  await acquireManaged(runtime, 'holder', 'fast-coder')
  const waiting = acquireManaged(runtime, 'waiter', 'deep-coder')

  releaseSession(runtime, 'holder')
  const acquired = await waiting
  assert.equal(acquired.Model, 'provider/one')
  assert.equal(snapshotOccupied(runtime).length, 1)

  releaseSession(runtime, 'holder')
  assert.equal(snapshotOccupied(runtime).length, 1, 'second release cannot remove somebody else\'s lease')
})

test('WHAT[EMR-002] EMR_002_scheduler_program_error_poisons_pending_and_future_demands', async () => {
  const runtime = new ModelRoutingRuntime((role) => {
    if (role === 'waiting') return null
    throw new Error('bad scheduler program')
  })

  const waiting = acquireManaged(runtime, 'waiter', 'waiting')
  const waitingRejected = assert.rejects(waiting, /bad scheduler program/)
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  await assert.rejects(acquireManaged(runtime, 'boom', 'boom-role'), /bad scheduler program/)
  await waitingRejected
  await assert.rejects(acquireManaged(runtime, 'later', 'waiting'), /bad scheduler program/)
  assert.equal(pendingCount(runtime), 0)
})
