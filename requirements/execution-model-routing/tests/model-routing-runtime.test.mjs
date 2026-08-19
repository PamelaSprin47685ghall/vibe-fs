import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const {
  createRuntime,
  acquireManaged,
  tryReserveManaged,
  tryLease,
  releaseExecution,
  releasePhysicalExecution,
  cancelPendingExecution,
  bindCapacityChild,
  enterProviderStep,
  endProviderStep,
  suppressProviderStep,
  snapshotOccupied,
  pendingCount,
} = routing

const target = (model = 'provider/shared', reasoning = 'none') => ({ model, reasoning })
const key = (value) => `${value.model}|${value.reasoning}`
const acquireTarget = async (...args) => {
  const outcome = await acquireManaged(...args)
  assert.equal(outcome.kind, 'Acquired')
  return outcome.target
}

test('WHAT[EMR-003] EMR_003_each_active_physical_execution_contributes_one_running_occurrence', async () => {
  const runtime = createRuntime(() => target())

  const first = await acquireTarget(runtime, 'session-a', 'msg-a', 'fast-coder')
  const same = await acquireTarget(runtime, 'session-a', 'msg-a', 'fast-coder')
  const otherExecution = await acquireTarget(runtime, 'session-b', 'msg-b', 'deep-coder')

  assert.equal(key(first), 'provider/shared|none')
  assert.equal(key(same), 'provider/shared|none')
  assert.equal(key(otherExecution), 'provider/shared|none')
  assert.equal(snapshotOccupied(runtime).length, 2, 'two physical executions contribute two occurrences even on one target')
})

test('WHAT[EMR-006] EMR_006_same_physical_message_retry_reuses_target_without_scheduler_rerun', async () => {
  const seen = []
  const runtime = createRuntime((_role, running) => {
    seen.push(running.map((item) => `${item.model}|${item.reasoning}`))
    return target()
  })

  await acquireTarget(runtime, 'session-a', 'msg-1', 'fast-coder')
  await acquireTarget(runtime, 'session-a', 'msg-1', 'fast-coder')
  await acquireTarget(runtime, 'session-a', 'msg-2', 'deep-coder')

  assert.deepEqual(seen, [[], []], 'same physical material reuses; newer material supersedes and schedules fresh')
})

test('WHAT[EMR-006] EMR_006_new_physical_message_supersedes_old_A_B_occupancy_without_idle', async () => {
  const runtime = createRuntime((role) => target(`provider/${role}`))

  const a = await acquireTarget(runtime, 'session', 'msg-a', 'fast-coder')
  assert.equal(a.model, 'provider/fast-coder')
  assert.equal(snapshotOccupied(runtime).length, 1)

  const b = await acquireTarget(runtime, 'session', 'msg-b', 'deep-coder')
  assert.equal(b.model, 'provider/deep-coder')
  assert.equal(snapshotOccupied(runtime).length, 1, 'one reusable session can own only one current physical execution slot')
  assert.equal(tryLease(runtime, 'session', 'msg-a', 'fast-coder'), null, 'superseded physical material no longer owns a lease')
  assert.equal(key(tryLease(runtime, 'session', 'msg-b', 'deep-coder')), 'provider/deep-coder|none')
})

test('WHAT[EMR-006] EMR_006_same_physical_message_cannot_change_effective_agent', async () => {
  const runtime = createRuntime(() => target())
  await acquireTarget(runtime, 'session', 'msg-1', 'fast-coder')

  await assert.rejects(
    acquireTarget(runtime, 'session', 'msg-1', 'deep-coder'),
    /physical execution .* changed agent/i,
  )
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-004] EMR_004_required_null_waits_for_an_occupancy_event_then_retries', async () => {
  const route = (_role, running) => running.filter((item) => item.model === 'provider/only').length < 1
    ? target('provider/only')
    : null
  const runtime = createRuntime(route)

  await acquireTarget(runtime, 'holder', 'msg-holder', 'fast-coder')
  let settled = false
  const waiting = acquireTarget(runtime, 'waiter', 'msg-waiter', 'fast-coder').then((value) => {
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
  const runtime = createRuntime((role) => role === 'blocked' ? null : target(`provider/${role}`))

  const old = acquireManaged(runtime, 'same-session', 'msg-old', 'blocked')
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  const fresh = await acquireTarget(runtime, 'same-session', 'msg-new', 'free')
  assert.equal(fresh.model, 'provider/free')
  const oldOutcome = await old
  assert.equal(oldOutcome.kind, 'Superseded')
  assert.equal(oldOutcome.target, null)
  assert.equal(pendingCount(runtime), 0)
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-004] EMR_004_an_earlier_null_waiter_does_not_head_of_line_block_another_role', async () => {
  const runtime = createRuntime((role) => role === 'blocked' ? null : target(`provider/${role}`))

  const blocked = acquireManaged(runtime, 'blocked-session', 'msg-blocked', 'blocked')
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  const free = await acquireTarget(runtime, 'free-session', 'msg-free', 'free')
  assert.equal(key(free), 'provider/free|none')
  assert.equal(pendingCount(runtime), 1)

  cancelPendingExecution(runtime, 'blocked-session')
  const blockedOutcome = await blocked
  assert.equal(blockedOutcome.kind, 'Superseded')
  assert.equal(blockedOutcome.target, null)
})

test('WHAT[EMR-004] EMR_004_optional_null_is_k0_not_a_pending_demand', () => {
  const runtime = createRuntime(() => null)
  assert.equal(tryReserveManaged(runtime, 'replica', 'fast-coder'), null)
  assert.equal(pendingCount(runtime), 0)
  assert.deepEqual(snapshotOccupied(runtime), [])
})

test('WHAT[EMR-004] EMR_004_strength_reservation_is_adopted_by_chat_message_without_double_counting', async () => {
  let calls = 0
  const runtime = createRuntime(() => {
    calls += 1
    return target('provider/replica')
  })

  const reserved = tryReserveManaged(runtime, 'replica', 'fast-coder')
  assert.equal(key(reserved), 'provider/replica|none')
  assert.equal(snapshotOccupied(runtime).length, 1)

  const adopted = await acquireTarget(runtime, 'replica', 'msg-replica', 'fast-coder')
  assert.equal(key(adopted), 'provider/replica|none')
  assert.equal(calls, 1, 'physical acceptance adopts the reservation without another scheduler decision')
  assert.equal(snapshotOccupied(runtime).length, 1, 'reservation and physical execution are one capacity occurrence')
  assert.equal(key(tryLease(runtime, 'replica', 'msg-replica', 'fast-coder')), 'provider/replica|none')
})

test('WHAT[EMR-006] EMR_006_lease_is_stable_only_for_one_physical_user_material', async () => {
  let calls = 0
  const runtime = createRuntime(() => target(`provider/model-${++calls}`, 'low'))

  const first = await acquireTarget(runtime, 'session', 'msg-1', 'deep-coder')
  const retry = await acquireTarget(runtime, 'session', 'msg-1', 'deep-coder')

  assert.equal(first.model, 'provider/model-1')
  assert.equal(retry.model, 'provider/model-1')
  assert.equal(calls, 1)

  const nextMaterial = await acquireTarget(runtime, 'session', 'msg-2', 'deep-coder')
  assert.equal(nextMaterial.model, 'provider/model-2', 'new physical material gets a fresh lease even without idle')
  assert.equal(calls, 2)
})

test('WHAT[EMR-006] EMR_006_continuation_passes_previous_target_but_new_session_passes_null', async () => {
  let next = 0
  const seenPrevious = []
  const runtime = createRuntime((_role, _running, previous) => {
    seenPrevious.push(previous)
    return previous ?? target(`provider/model-${++next}`, 'low')
  })

  const first = await acquireTarget(runtime, 'continued', 'msg-1', 'deep-coder')
  assert.equal(first.model, 'provider/model-1')
  releasePhysicalExecution(runtime, 'continued', 'msg-1')

  const continued = await acquireTarget(runtime, 'continued', 'msg-2', 'deep-coder')
  assert.deepEqual(continued, first, 'exact terminal releases capacity but preserves continuation preference')

  const fresh = await acquireTarget(runtime, 'new-session', 'msg-1', 'deep-coder')
  assert.equal(fresh.model, 'provider/model-2')
  assert.deepEqual(seenPrevious, [null, first, null])
})

test('WHAT[EMR-007] EMR_007_execution_release_is_idempotent_and_wakes_waiters_once', async () => {
  const runtime = createRuntime((_role, running) => running.length === 0 ? target('provider/one') : null)
  await acquireTarget(runtime, 'holder', 'msg-holder', 'fast-coder')
  const waiting = acquireTarget(runtime, 'waiter', 'msg-waiter', 'deep-coder')

  releaseExecution(runtime, 'holder')
  const acquired = await waiting
  assert.equal(acquired.model, 'provider/one')
  assert.equal(snapshotOccupied(runtime).length, 1)

  releaseExecution(runtime, 'holder')
  assert.equal(snapshotOccupied(runtime).length, 1, 'second release cannot remove somebody else\'s execution')
})

test('WHAT[EMR-007] EMR_007_late_terminal_for_superseded_physical_execution_cannot_release_current_lease', async () => {
  const runtime = createRuntime((role) => target(`provider/${role}`))

  await acquireTarget(runtime, 'reused-session', 'msg-old', 'fast-coder')
  await acquireTarget(runtime, 'reused-session', 'msg-current', 'deep-coder')

  releasePhysicalExecution(runtime, 'reused-session', 'msg-old')
  assert.equal(
    key(tryLease(runtime, 'reused-session', 'msg-current', 'deep-coder')),
    'provider/deep-coder|none',
    'late exact terminal evidence for the old physical material must not touch the current lease',
  )
  assert.equal(snapshotOccupied(runtime).length, 1)

  releasePhysicalExecution(runtime, 'reused-session', 'msg-current')
  assert.equal(snapshotOccupied(runtime).length, 0, 'the matching physical terminal releases exactly one occurrence')
})

test('WHAT[EMR-002] EMR_002_scheduler_program_error_poisons_pending_and_future_demands', async () => {
  const runtime = createRuntime((role) => {
    if (role === 'waiting') return null
    throw new Error('bad scheduler program')
  })

  const waiting = acquireTarget(runtime, 'waiter', 'msg-waiting', 'waiting')
  const waitingRejected = assert.rejects(waiting, /bad scheduler program/)
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  await assert.rejects(acquireTarget(runtime, 'boom', 'msg-boom', 'boom-role'), /bad scheduler program/)
  await waitingRejected
  await assert.rejects(acquireTarget(runtime, 'later', 'msg-later', 'waiting'), /bad scheduler program/)
  assert.equal(pendingCount(runtime), 0)
})

const provider = (model) => model.slice(0, model.indexOf('/'))
const providerLimited = (limits, routes) => (role, running, previous) => {
  const candidates = routes[role] ?? []
  const count = (name) => running.filter((item) => provider(item.model) === name).length
  const available = (candidate) => count(provider(candidate.model)) < (limits[provider(candidate.model)] ?? 0)
  if (previous && candidates.some((candidate) => key(candidate) === key(previous)) && available(previous)) return previous
  return candidates.find(available) ?? null
}

test('WHAT[EMR-010] EMR_010_lineage_credit_is_free_only_to_descendants_not_global_waiters', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { parent: [only], child: [only], stranger: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'parent')
  bindCapacityChild(runtime, 'parent', 'child')
  assert.equal(key(await acquireTarget(runtime, 'child', 'msg-child', 'child')), key(only))
  assert.equal(snapshotOccupied(runtime).length, 1, 'borrowing never creates a second provider token')

  let settled = false
  const stranger = acquireManaged(runtime, 'stranger', 'msg-stranger', 'stranger').then((value) => {
    settled = true
    return value
  })
  await Promise.resolve()
  assert.equal(settled, false, 'unrelated sessions still see the ancestor token as occupied')
  cancelPendingExecution(runtime, 'stranger')
  assert.equal((await stranger).kind, 'Superseded')
})

test('WHAT[EMR-010] EMR_010_ancestor_recall_waits_for_descendant_step_end_without_overbooking', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { parent: [only], child: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'parent')
  bindCapacityChild(runtime, 'parent', 'child')
  await acquireTarget(runtime, 'child', 'msg-child', 'child')

  await enterProviderStep(runtime, 'child', 'msg-child', [])
  let parentEntered = false
  const parentStep = enterProviderStep(runtime, 'parent', 'msg-parent', []).then(() => { parentEntered = true })
  await Promise.resolve()
  assert.equal(parentEntered, false)
  assert.equal(snapshotOccupied(runtime).length, 1, 'recall cannot exceed the hard provider limit')

  endProviderStep(runtime, 'child', 'msg-child', 'run-child-1')
  await parentStep
  let childEntered = false
  const childStep = enterProviderStep(runtime, 'child', 'msg-child', ['run-child-1']).then(() => { childEntered = true })
  await Promise.resolve()
  assert.equal(childEntered, false, 'recalled child blocks at its next transform')

  endProviderStep(runtime, 'parent', 'msg-parent', 'run-parent-1')
  await childStep
})

test('WHAT[EMR-010] EMR_010_late_old_terminal_cannot_release_a_new_provider_step', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { parent: [only], child: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'parent')
  bindCapacityChild(runtime, 'parent', 'child')
  await acquireTarget(runtime, 'child', 'msg-child', 'child')
  await enterProviderStep(runtime, 'child', 'msg-child', [])
  endProviderStep(runtime, 'child', 'msg-child', 'run-child-1')
  await enterProviderStep(runtime, 'child', 'msg-child', ['run-child-1'])

  let recalled = false
  const parentStep = enterProviderStep(runtime, 'parent', 'msg-parent', []).then(() => { recalled = true })
  endProviderStep(runtime, 'child', 'msg-child', 'run-child-1')
  await Promise.resolve()
  assert.equal(recalled, false, 'the previous assistant id is fenced out of the new step')

  endProviderStep(runtime, 'child', 'msg-child', 'run-child-2')
  await parentStep
})

test('WHAT[EMR-010] EMR_010_confirmed_pre_dispatch_suppression_returns_the_step_token', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { parent: [only], child: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'parent')
  bindCapacityChild(runtime, 'parent', 'child')
  await acquireTarget(runtime, 'child', 'msg-child', 'child')
  await enterProviderStep(runtime, 'child', 'msg-child', [])

  let recalled = false
  const parentStep = enterProviderStep(runtime, 'parent', 'msg-parent', []).then(() => { recalled = true })
  await Promise.resolve()
  assert.equal(recalled, false)

  suppressProviderStep(runtime, 'child', 'msg-child')
  await parentStep
  assert.equal(recalled, true)
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-010] EMR_010_recalled_child_may_take_new_ordinary_capacity_for_exact_target', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 2 }, { parent: [only], child: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'parent')
  bindCapacityChild(runtime, 'parent', 'child')
  await acquireTarget(runtime, 'child', 'msg-child', 'child')
  await enterProviderStep(runtime, 'child', 'msg-child', [])
  const parentStep = enterProviderStep(runtime, 'parent', 'msg-parent', [])
  endProviderStep(runtime, 'child', 'msg-child', 'run-child-1')
  await parentStep

  await enterProviderStep(runtime, 'child', 'msg-child', ['run-child-1'])
  assert.equal(snapshotOccupied(runtime).length, 2)
})

test('WHAT[EMR-010] EMR_010_owner_priority_beats_multiple_children_and_nested_borrowers', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited(
    { provider: 1 },
    { root: [only], childA: [only], childB: [only], grandchild: [only] },
  ))

  await acquireTarget(runtime, 'root', 'msg-root', 'root')
  bindCapacityChild(runtime, 'root', 'child-a')
  bindCapacityChild(runtime, 'root', 'child-b')
  bindCapacityChild(runtime, 'child-a', 'grandchild')
  await acquireTarget(runtime, 'child-a', 'msg-a', 'childA')
  await acquireTarget(runtime, 'child-b', 'msg-b', 'childB')
  await acquireTarget(runtime, 'grandchild', 'msg-g', 'grandchild')

  await enterProviderStep(runtime, 'grandchild', 'msg-g', [])
  const order = []
  const childA = enterProviderStep(runtime, 'child-a', 'msg-a', []).then(() => order.push('child-a'))
  const childB = enterProviderStep(runtime, 'child-b', 'msg-b', []).then(() => order.push('child-b'))
  const root = enterProviderStep(runtime, 'root', 'msg-root', []).then(() => order.push('root'))
  await Promise.resolve()

  endProviderStep(runtime, 'grandchild', 'msg-g', 'run-g-1')
  await root
  assert.deepEqual(order, ['root'])
  endProviderStep(runtime, 'root', 'msg-root', 'run-root-1')
  await childA
  assert.deepEqual(order.slice(0, 2), ['root', 'child-a'])
  endProviderStep(runtime, 'child-a', 'msg-a', 'run-a-1')
  await childB
  assert.deepEqual(order, ['root', 'child-a', 'child-b'])
})

test('WHAT[EMR-010] EMR_010_credit_never_crosses_provider_boundary', async () => {
  const a = target('provider-a/model')
  const b = target('provider-b/model')
  const runtime = createRuntime(providerLimited(
    { 'provider-a': 1, 'provider-b': 1 },
    { parent: [a], blocker: [b], child: [b] },
  ))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'parent')
  await acquireTarget(runtime, 'blocker', 'msg-blocker', 'blocker')
  bindCapacityChild(runtime, 'parent', 'child')

  let settled = false
  const child = acquireManaged(runtime, 'child', 'msg-child', 'child').then((value) => {
    settled = true
    return value
  })
  await Promise.resolve()
  assert.equal(settled, false)
  cancelPendingExecution(runtime, 'child')
  assert.equal((await child).kind, 'Superseded')
})

test('WHAT[EMR-010] EMR_010_multi_provider_credit_requires_one_token_attribution', async () => {
  const a = target('provider-a/model')
  const b = target('provider-b/model')
  const runtime = createRuntime((role, running) => {
    if (role === 'root') return a
    if (role === 'middle') return b
    if (role !== 'leaf') return null
    const occupied = new Set(running.map((item) => provider(item.model)))
    return !occupied.has('provider-a') && !occupied.has('provider-b') ? a : null
  })

  await acquireTarget(runtime, 'root', 'msg-root', 'root')
  bindCapacityChild(runtime, 'root', 'middle')
  await acquireTarget(runtime, 'middle', 'msg-middle', 'middle')
  bindCapacityChild(runtime, 'middle', 'leaf')

  let settled = false
  const leaf = acquireManaged(runtime, 'leaf', 'msg-leaf', 'leaf').then((value) => {
    settled = true
    return value
  })
  await Promise.resolve()
  assert.equal(settled, false, 'a schedule requiring two hidden providers cannot consume one borrowed token')
  assert.equal(snapshotOccupied(runtime).length, 2)
  cancelPendingExecution(runtime, 'leaf')
  assert.equal((await leaf).kind, 'Superseded')
})
