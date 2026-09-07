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

test('WHAT[EMR-003] EMR_003_each_active_physical_execution_contributes_one_running_occurrence', async () => {
  const runtime = createRuntime(() => target())

  const first = await acquireTarget(runtime, 'session-a', 'msg-a', 'coder', 'alice')
  const same = await acquireTarget(runtime, 'session-a', 'msg-a', 'coder', 'alice')
  const otherExecution = await acquireTarget(runtime, 'session-b', 'msg-b', 'coder', 'bob')

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

  await acquireTarget(runtime, 'session-a', 'msg-1', 'coder', 'alice')
  await acquireTarget(runtime, 'session-a', 'msg-1', 'coder', 'alice')
  await acquireTarget(runtime, 'session-a', 'msg-2', 'coder', 'alice')

  assert.deepEqual(seen, [[], ['provider/shared|none']], 'same physical material reuses without a scheduler rerun; the superseding fresh schedule observes the replaced occupancy')
})

test('WHAT[EMR-006] EMR_006_new_physical_message_supersedes_old_A_B_occupancy_without_idle', async () => {
  const runtime = createRuntime((role) => target(`provider/${role}`))

  const a = await acquireTarget(runtime, 'session', 'msg-a', 'coder', 'alice')
  assert.equal(a.model, 'provider/coder')
  assert.equal(snapshotOccupied(runtime).length, 1)

  const b = await acquireTarget(runtime, 'session', 'msg-b', 'inspector', 'alice')
  assert.equal(b.model, 'provider/inspector')
  assert.equal(snapshotOccupied(runtime).length, 1, 'one reusable session can own only one current physical execution slot')
  assert.equal(tryLease(runtime, 'session', 'msg-a', 'coder', 'alice', null), null, 'superseded physical material no longer owns a lease')
  assert.equal(key(tryLease(runtime, 'session', 'msg-b', 'inspector', 'alice', null)), 'provider/inspector|none')
})

test('WHAT[EMR-006] EMR_006_supersede_replaces_the_exact_capacity_owner', async () => {
  const runtime = createRuntime((role) => target(`provider/${role}`))

  await acquireTarget(runtime, 'session', 'msg-a', 'coder', 'alice')
  await acquireTarget(runtime, 'session', 'msg-b', 'inspector', 'alice')

  const snapshot = capacitySnapshot(runtime)
  assert.equal(snapshot.tokens.length, 1)
  assert.deepEqual(snapshot.tokens[0].owner, {
    sessionId: 'session',
    physicalUserMessageId: 'msg-b',
    role: 'inspector',
    participant: 'alice',
  })
  assert.deepEqual(snapshot.executions, [
    {
      sessionId: 'session',
      physicalUserMessageId: 'msg-b',
      role: 'inspector',
      participant: 'alice',
    },
  ])
})

test('WHAT[EMR-006] EMR_006_only_the_current_run_witness_resolves_a_target', async () => {
  let scheduled = 0
  const runtime = createRuntime(() => target(`provider-${++scheduled}/model`))

  await acquireTarget(runtime, 'session', 'msg-a', 'manager', 'alice')
  endProviderStep(runtime, 'session', 'msg-a', 'run-a')

  const current = await acquireTarget(runtime, 'session', 'msg-b', 'manager', 'alice')
  endProviderStep(runtime, 'session', 'msg-b', 'run-b')

  assert.equal(takeProviderRunTarget(runtime, 'run-a'), null, 'superseded run cannot poison the current target')
  assert.equal(takeProviderRunTarget(runtime, 'run-b').model, current.model)
  assert.equal(takeProviderRunTarget(runtime, 'run-b'), null, 'the exact witness is single-consumption')
  assert.equal(takeProviderRunTarget(runtime, 'no-such-run'), null, 'an unknown run resolves nothing')
})

test('WHAT[EMR-006] EMR_006_same_physical_message_cannot_change_role', async () => {
  const runtime = createRuntime(() => target())
  await acquireTarget(runtime, 'session', 'msg-1', 'coder', 'alice')

  await assert.rejects(
    acquireTarget(runtime, 'session', 'msg-1', 'inspector', 'alice'),
    /physical execution .* changed role/i,
  )
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-006] EMR_006_same_physical_message_cannot_change_participant', async () => {
  const runtime = createRuntime(() => target())
  await acquireTarget(runtime, 'session', 'msg-1', 'coder', 'alice')

  await assert.rejects(
    acquireTarget(runtime, 'session', 'msg-1', 'coder', 'bob'),
    /physical execution .* changed participant/i,
  )
  assert.equal(snapshotOccupied(runtime).length, 1)
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

test('WHAT[EMR-006] EMR_006_lease_is_stable_only_for_one_physical_user_material', async () => {
  let calls = 0
  const runtime = createRuntime(() => target(`provider/model-${++calls}`, 'low'))

  const first = await acquireTarget(runtime, 'session', 'msg-1', 'coder', 'alice')
  const retry = await acquireTarget(runtime, 'session', 'msg-1', 'coder', 'alice')

  assert.equal(first.model, 'provider/model-1')
  assert.equal(retry.model, 'provider/model-1')
  assert.equal(calls, 1)

  const nextMaterial = await acquireTarget(runtime, 'session', 'msg-2', 'coder', 'alice')
  assert.equal(nextMaterial.model, 'provider/model-2', 'new physical material gets a fresh lease even without idle')
  assert.equal(calls, 2)
})

test('WHAT[EMR-006] EMR_006_fresh_physical_receives_the_replaced_target_as_previous', async () => {
  const seenPrevious = []
  let next = 0
  const runtime = createRuntime((_role, _running, previous) => {
    seenPrevious.push(previous)
    return previous ?? target(`provider/model-${++next}`, 'low')
  })

  const first = await acquireTarget(runtime, 'continued', 'msg-1', 'coder', 'alice')
  assert.equal(first.model, 'provider/model-1')

  const continued = await acquireTarget(runtime, 'continued', 'msg-2', 'coder', 'alice')
  assert.deepEqual(continued, first, 'the atomically replaced active physical supplies the previous target')
  assert.deepEqual(seenPrevious, [null, first])
})

test('WHAT[EMR-006] EMR_006_released_session_supplies_no_previous_target', async () => {
  const seenPrevious = []
  let next = 0
  const runtime = createRuntime((_role, _running, previous) => {
    seenPrevious.push(previous)
    return previous ?? target(`provider/model-${++next}`, 'low')
  })

  await acquireTarget(runtime, 'continued', 'msg-1', 'coder', 'alice')
  releasePhysicalExecution(runtime, 'continued', 'msg-1')

  const rebuilt = await acquireTarget(runtime, 'continued', 'msg-2', 'coder', 'alice')
  assert.equal(rebuilt.model, 'provider/model-2', 'no session-history cache survives an exact terminal release')
  assert.deepEqual(seenPrevious, [null, null])
})

test('WHAT[EMR-006] EMR_006_unrelated_session_supplies_no_previous_target', async () => {
  const seenPrevious = []
  let next = 0
  const runtime = createRuntime((_role, _running, previous) => {
    seenPrevious.push(previous)
    return previous ?? target(`provider/model-${++next}`, 'low')
  })

  const first = await acquireTarget(runtime, 'session-a', 'msg-1', 'coder', 'alice')
  assert.equal(first.model, 'provider/model-1')

  const fresh = await acquireTarget(runtime, 'session-b', 'msg-1', 'coder', 'bob')
  assert.equal(fresh.model, 'provider/model-2', 'another session never inherits a previous target')
  assert.deepEqual(seenPrevious, [null, null])
})

test('WHAT[EMR-007] EMR_007_execution_release_is_idempotent_and_wakes_waiters_once', async () => {
  const runtime = createRuntime((_role, running) => running.length === 0 ? target('provider/one') : null)
  await acquireTarget(runtime, 'holder', 'msg-holder', 'coder', 'alice')
  const waiting = acquireTarget(runtime, 'waiter', 'msg-waiter', 'inspector', 'bob')

  releasePhysicalExecution(runtime, 'holder', 'msg-holder')
  const acquired = await waiting
  assert.equal(acquired.model, 'provider/one')
  assert.equal(snapshotOccupied(runtime).length, 1)

  releasePhysicalExecution(runtime, 'holder', 'msg-holder')
  assert.equal(snapshotOccupied(runtime).length, 1, 'second release cannot remove somebody else\'s execution')
})

test('WHAT[EMR-007] EMR_007_late_terminal_for_superseded_physical_execution_cannot_release_current_lease', async () => {
  const runtime = createRuntime((role) => target(`provider/${role}`))

  await acquireTarget(runtime, 'reused-session', 'msg-old', 'coder', 'alice')
  await acquireTarget(runtime, 'reused-session', 'msg-current', 'inspector', 'alice')

  releasePhysicalExecution(runtime, 'reused-session', 'msg-old')
  assert.equal(
    key(tryLease(runtime, 'reused-session', 'msg-current', 'inspector', 'alice', null)),
    'provider/inspector|none',
    'late exact terminal evidence for the old physical material must not touch the current lease',
  )
  assert.equal(snapshotOccupied(runtime).length, 1)

  releasePhysicalExecution(runtime, 'reused-session', 'msg-current')
  assert.equal(snapshotOccupied(runtime).length, 0, 'the matching physical terminal releases exactly one occurrence')
})

test('WHAT[EMR-002] EMR_002_scheduler_program_error_poisons_pending_and_future_demands', async () => {
  const runtime = createRuntime((role) => {
    if (role === 'inspector') return null
    throw new Error('bad scheduler program')
  })

  const waiting = acquireTarget(runtime, 'waiter', 'msg-waiting', 'inspector', 'alice')
  const waitingRejected = assert.rejects(waiting, /bad scheduler program/)
  await Promise.resolve()
  assert.equal(pendingCount(runtime), 1)

  await assert.rejects(acquireTarget(runtime, 'boom', 'msg-boom', 'manager', 'bob'), /bad scheduler program/)
  await waitingRejected
  await assert.rejects(acquireTarget(runtime, 'later', 'msg-later', 'inspector', 'carol'), /bad scheduler program/)
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

test('WHAT[EMR-010] EMR_010_explicit_lender_credit_is_free_only_to_borrowers_not_global_waiters', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { coder: [only], manager: [only], inspector: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'coder', 'alice')
  assert.equal(key(await acquireTarget(runtime, 'child', 'msg-child', 'manager', 'bob', 'parent')), key(only))
  assert.equal(snapshotOccupied(runtime).length, 1, 'borrowing never creates a second provider token')

  let settled = false
  const stranger = acquireManaged(runtime, 'stranger', 'msg-stranger', 'inspector', 'carol').then((value) => {
    settled = true
    return value
  })
  await Promise.resolve()
  assert.equal(settled, false, 'a session without an explicit lender still sees the token as occupied')
  cancelPendingExecution(runtime, 'stranger')
  assert.equal((await stranger).kind, 'Cancelled')
})

test('WHAT[EMR-010] EMR_010_absent_lender_queues_without_borrowing', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { coder: [only], manager: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'coder', 'alice')
  const ghost = await beginExecutionAdmission(runtime, 'child', 'msg-child', 'manager', 'bob', 'ghost')
  assert.equal(ghost.kind, 'Queued', 'a lender with no credit authorizes nothing')

  cancelPendingExecution(runtime, 'child')
  assert.equal((await awaitQueuedExecutionAdmission(ghost.queue)).kind, 'Cancelled')
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-010] EMR_010_borrowed_step_handoff_reuses_the_same_credit', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { coder: [only], manager: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'coder', 'alice')
  await acquireTarget(runtime, 'child', 'msg-child', 'manager', 'bob', 'parent')
  await enterProviderStep(runtime, 'child', 'msg-child', [])

  assert.equal(snapshotOccupied(runtime).length, 1, 'handoff reuses the same real provider credit')

  suppressProviderStep(runtime, 'child', 'msg-child')
  assert.deepEqual(capacitySnapshot(runtime).tokenStateCounts, { idle: 1, inFlight: 0, retiring: 0 })
  assert.equal(snapshotOccupied(runtime).length, 1)
})

test('WHAT[EMR-010] EMR_010_older_borrowed_step_precedes_later_owned_step', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { coder: [only], manager: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'coder', 'alice')
  await acquireTarget(runtime, 'child', 'msg-child', 'manager', 'bob', 'parent')
  await enterProviderStep(runtime, 'parent', 'msg-parent', [])

  const childStep = enterProviderStep(runtime, 'child', 'msg-child', [])
  const parentNextStep = enterProviderStep(runtime, 'parent', 'msg-parent', [])
  assert.deepEqual(
    capacitySnapshot(runtime).waiters.map((waiter) => waiter.sessionId),
    ['child', 'parent'],
  )

  endProviderStep(runtime, 'parent', 'msg-parent', 'run-parent')
  assert.deepEqual(
    capacitySnapshot(runtime).waiters.map((waiter) => waiter.sessionId),
    ['parent'],
    'owned identity selects its exact token but grants no priority over an older eligible borrower',
  )

  await childStep
  suppressProviderStep(runtime, 'child', 'msg-child')
  await parentNextStep
  suppressProviderStep(runtime, 'parent', 'msg-parent')
  assert.deepEqual(capacitySnapshot(runtime).tokenStateCounts, { idle: 1, inFlight: 0, retiring: 0 })
})

test('WHAT[EMR-010] EMR_010_credit_never_crosses_provider_boundary', async () => {
  const a = target('provider-a/model')
  const b = target('provider-b/model')
  const runtime = createRuntime(providerLimited(
    { 'provider-a': 1, 'provider-b': 1 },
    { coder: [a], manager: [b] },
  ))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'coder', 'alice')
  const child = await beginExecutionAdmission(runtime, 'child', 'msg-child', 'manager', 'bob', 'parent')
  assert.equal(child.kind, 'Acquired', 'a borrower needing another provider takes ordinary capacity')
  assert.equal(key(executionAdmissionTarget(runtime, child.lease)), 'provider-b/model|none')
  assert.equal(snapshotOccupied(runtime).length, 2, 'no provider token is shared across providers')

  cancelPendingExecution(runtime, 'child')
})

test('WHAT[EMR-010] EMR_010_reservation_borrowing_shares_one_token', async () => {
  const runtime = createRuntime(() => target('provider/shared'))

  const first = tryReserveManaged(runtime, 'parent', 'coder', null)
  const second = tryReserveManaged(runtime, 'child', 'coder', 'parent')
  assert.equal(key(first), 'provider/shared|none')
  assert.equal(key(second), 'provider/shared|none')
  assert.equal(capacitySnapshot(runtime).ledgerEntries.length, 1, 'an explicit lender reservation duplicates no capacity')
  assert.equal(snapshotOccupied(runtime).length, 1)
})
