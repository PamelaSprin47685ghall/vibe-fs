import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const seed = 0x36c0ffee
const rounds = 32
const queueWidth = 32
const lineageCycles = 64
const capacity = 4
const admissionRetainedBound = 84
const ELIGIBLE = 'engineer'
const BLOCKED = 'devops'
const lineageRetainedComposition = Object.freeze({
  ledgerEntries: 1,
  token: 1,
  ownerAndBorrowerCustodies: 2,
  executions: 2,
  pendingProviderWaiter: 1,
  owners: 2,
  lineage: 0,
})
const lineageRetainedBound = Object.values(lineageRetainedComposition).reduce((sum, count) => sum + count, 0)
const target = { model: 'provider/shared', reasoning: 'none' }
const exactKey = ({ sessionId, physicalUserMessageId }) => `${sessionId}\u001f${physicalUserMessageId}`
const seeded = (initial) => {
  let state = initial >>> 0
  return () => {
    state ^= state << 13
    state ^= state >>> 17
    state ^= state << 5
    return state >>> 0
  }
}
const shuffled = (count, next) => {
  const values = Array.from({ length: count }, (_, index) => index)
  for (let index = values.length - 1; index > 0; index -= 1) {
    const other = next() % (index + 1)
    ;[values[index], values[other]] = [values[other], values[index]]
  }
  return values
}
const createAuditor = (runtime, retainedBound) => {
  let operations = 0
  let maxRetained = 0
  let previousCounters = { duplicate: 0, stale: 0, conflict: 0 }

  const audit = () => {
    const snapshot = routing.capacitySnapshot(runtime)
    const admissionWaiters = snapshot.waiters.filter((waiter) => waiter.kind === 'Admission')
    const ownerKeys = new Set(snapshot.owners.map(exactKey))
    const ledgerByCredit = new Map(snapshot.ledgerEntries.map((entry) => [entry.credit, entry]))
    const ledgerCredits = new Set(ledgerByCredit.keys())
    const tokenCredits = new Set(snapshot.tokens.map((token) => token.credit))
    const custodyEdges = new Set()

    assert.equal(Object.isFrozen(snapshot), true)
    assert.equal(Object.isFrozen(snapshot.tokens), true)
    assert.ok(snapshot.activeCount >= 0 && snapshot.activeCount <= snapshot.ledgerEntries.length)
    assert.equal(
      snapshot.tokenStateCounts.idle + snapshot.tokenStateCounts.inFlight + snapshot.tokenStateCounts.retiring,
      snapshot.tokens.length,
    )
    assert.equal(snapshot.activeCount, snapshot.tokenStateCounts.inFlight + snapshot.tokenStateCounts.retiring)
    assert.equal(snapshot.tokens.length, snapshot.ledgerEntries.length)
    assert.equal(ledgerCredits.size, snapshot.ledgerEntries.length)
    assert.equal(tokenCredits.size, snapshot.tokens.length)
    assert.equal(admissionWaiters.length, routing.pendingCount(runtime))
    assert.ok(admissionWaiters.length <= routing.pendingBound(runtime))
    assert.ok(Number.isSafeInteger(routing.pendingBound(runtime)) && routing.pendingBound(runtime) > 0)

    for (const token of snapshot.tokens) {
      assert.ok(ledgerCredits.has(token.credit), 'every token traces to one ledger credit')
      assert.deepEqual(token.target, ledgerByCredit.get(token.credit).target, 'token and ledger target are exact')
      assert.ok(ownerKeys.has(exactKey(token.owner)), 'every token traces to one exact owner')
    }
    for (const custody of snapshot.custodies) {
      assert.ok(tokenCredits.has(custody.credit), 'every custody traces to one token')
      assert.ok(custody.owner.sessionId.length > 0 && custody.owner.physicalUserMessageId.length > 0)
      assert.ok(ownerKeys.has(exactKey(custody.owner)), 'every custody traces to one exact owner')
      const edge = `${custody.credit}\u001f${exactKey(custody.owner)}`
      assert.equal(custodyEdges.has(edge), false, 'custody edges are exact and unique')
      custodyEdges.add(edge)
    }
    for (const execution of snapshot.executions)
      assert.ok(ownerKeys.has(exactKey(execution)), 'every execution traces to one exact owner')
    for (const waiter of snapshot.waiters)
      assert.ok(ownerKeys.has(exactKey(waiter)), 'every waiter traces to one exact owner')

    for (const name of ['duplicate', 'stale', 'conflict'])
      assert.ok(snapshot.counters[name] >= previousCounters[name], `${name} counter is monotonic`)
    previousCounters = snapshot.counters

    assert.deepEqual(routing.reconcileCapacityEvidence(snapshot), { kind: 'NoOp' })
    assert.deepEqual(routing.capacitySnapshot(runtime), snapshot, 'reconciliation never mutates owner state')

    const retained =
      snapshot.ledgerEntries.length +
      snapshot.tokens.length +
      snapshot.custodies.length +
      snapshot.executions.length +
      snapshot.waiters.length +
      snapshot.owners.length +
      snapshot.lineage.length
    assert.ok(retained <= 6 * snapshot.owners.length + snapshot.lineage.length)
    if (retainedBound !== undefined) assert.ok(retained <= retainedBound)
    maxRetained = Math.max(maxRetained, retained)
    return snapshot
  }

  return {
    operation() {
      operations += 1
      return audit()
    },
    report() {
      return { operations, maxRetained }
    },
  }
}
const begin = (runtime, sessionId, physicalUserMessageId, role, participant, lenderSessionId = null) =>
  routing.beginExecutionAdmission(runtime, sessionId, physicalUserMessageId, role, participant, lenderSessionId)
const observedIdentity = (runtime, lease, record) => ({
  sessionId: record.sessionId,
  physicalUserMessageId: record.physicalUserMessageId,
  role: record.role,
  participant: record.participant,
  target: routing.executionAdmissionTarget(runtime, lease),
})

test('WHAT[execution-model-routing-010] seeded lender soak shares one physical credit without retained settlement nodes', async (context) => {
  const runtime = routing.createRuntime((_role, running) => (running.length === 0 ? target : null))
  const auditor = createAuditor(runtime, lineageRetainedBound)

  for (let cycle = 0; cycle < lineageCycles; cycle += 1) {
    const parent = {
      sessionId: `parent-${cycle}`,
      physicalUserMessageId: `parent-physical-${cycle}`,
      role: 'engineer',
      participant: `parent-owner-${cycle}`,
    }
    const child = {
      sessionId: `child-${cycle}`,
      physicalUserMessageId: `child-physical-${cycle}`,
      role: 'manager',
      participant: `child-owner-${cycle}`,
    }

    const parentOutcome = await begin(runtime, parent.sessionId, parent.physicalUserMessageId, parent.role, parent.participant)
    assert.equal(parentOutcome.kind, 'Acquired')
    auditor.operation()
    assert.deepEqual(
      routing.commitExecutionAdmission(runtime, parentOutcome.lease, observedIdentity(runtime, parentOutcome.lease, parent)),
      { kind: 'Applied' },
    )
    auditor.operation()

    const childOutcome = await begin(
      runtime,
      child.sessionId,
      child.physicalUserMessageId,
      child.role,
      child.participant,
      parent.sessionId,
    )
    assert.equal(childOutcome.kind, 'Acquired')
    auditor.operation()
    assert.deepEqual(
      routing.commitExecutionAdmission(runtime, childOutcome.lease, observedIdentity(runtime, childOutcome.lease, child)),
      { kind: 'Applied' },
    )
    auditor.operation()
    assert.equal(routing.capacitySnapshot(runtime).ledgerEntries.length, 1, 'explicit lender borrowing does not duplicate capacity')
    assert.deepEqual(routing.capacitySnapshot(runtime).lineage, [], 'borrowing leaves no ambient lineage edge')

    await routing.enterProviderStep(runtime, child.sessionId, child.physicalUserMessageId, [])
    auditor.operation()
    assert.equal(routing.capacitySnapshot(runtime).activeCount, 1, 'the borrowed step holds the one real credit')
    routing.endProviderStep(runtime, child.sessionId, child.physicalUserMessageId, `child-run-${cycle}`)
    auditor.operation()
    assert.equal(routing.capacitySnapshot(runtime).activeCount, 0, 'the causal step end releases the borrowed credit')
    routing.endProviderStep(runtime, child.sessionId, child.physicalUserMessageId, `child-run-${cycle}`)
    auditor.operation()
    assert.equal(routing.capacitySnapshot(runtime).activeCount, 0, 'a duplicate old end cannot release anything twice')

    assert.deepEqual(routing.releasePhysicalExecution(runtime, child.sessionId, child.physicalUserMessageId), {
      kind: 'Applied',
    })
    auditor.operation()
    assert.deepEqual(routing.releasePhysicalExecution(runtime, parent.sessionId, parent.physicalUserMessageId), {
      kind: 'Applied',
    })
    auditor.operation()
    const drained = auditor.operation()
    assert.equal(drained.ledgerEntries.length, 0)
    assert.equal(drained.tokens.length, 0)
    assert.equal(drained.custodies.length, 0)
    assert.equal(drained.executions.length, 0)
    assert.equal(drained.waiters.length, 0)
    assert.equal(drained.owners.length, 0)
    assert.equal(drained.lineage.length, 0)
  }

  const report = auditor.report()
  context.diagnostic(
    `task36 lender seed=${seed} cycles=${lineageCycles} operations=${report.operations} maxRetained=${report.maxRetained} retainedBound=${lineageRetainedBound} retainedComposition=${JSON.stringify(lineageRetainedComposition)}`,
  )
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

test('WHAT[execution-model-routing-010] EMR_010_explicit_lender_credit_is_free_only_to_borrowers_not_global_waiters', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { engineer: [only], manager: [only], devops: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'engineer', 'alice')
  assert.equal(key(await acquireTarget(runtime, 'child', 'msg-child', 'manager', 'bob', 'parent')), key(only))
  assert.equal(snapshotOccupied(runtime).length, 1, 'borrowing never creates a second provider token')

  let settled = false
  const stranger = acquireManaged(runtime, 'stranger', 'msg-stranger', 'devops', 'carol').then((value) => {
    settled = true
    return value
  })
  await Promise.resolve()
  assert.equal(settled, false, 'a session without an explicit lender still sees the token as occupied')
  cancelPendingExecution(runtime, 'stranger')
  assert.equal((await stranger).kind, 'Cancelled')
})
test('WHAT[execution-model-routing-010] EMR_010_absent_lender_queues_without_borrowing', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { engineer: [only], manager: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'engineer', 'alice')
  const ghost = await beginExecutionAdmission(runtime, 'child', 'msg-child', 'manager', 'bob', 'ghost')
  assert.equal(ghost.kind, 'Queued', 'a lender with no credit authorizes nothing')

  cancelPendingExecution(runtime, 'child')
  assert.equal((await awaitQueuedExecutionAdmission(ghost.queue)).kind, 'Cancelled')
  assert.equal(snapshotOccupied(runtime).length, 1)
})
test('WHAT[execution-model-routing-010] EMR_010_borrowed_step_handoff_reuses_the_same_credit', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { engineer: [only], manager: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'engineer', 'alice')
  await acquireTarget(runtime, 'child', 'msg-child', 'manager', 'bob', 'parent')
  await enterProviderStep(runtime, 'child', 'msg-child', [])

  assert.equal(snapshotOccupied(runtime).length, 1, 'handoff reuses the same real provider credit')

  suppressProviderStep(runtime, 'child', 'msg-child')
  assert.deepEqual(capacitySnapshot(runtime).tokenStateCounts, { idle: 1, inFlight: 0, retiring: 0 })
  assert.equal(snapshotOccupied(runtime).length, 1)
})
test('WHAT[execution-model-routing-010] EMR_010_owner_transform_entry_reclaims_foreign_inflight_borrow', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { engineer: [only], manager: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'engineer', 'alice')
  await acquireTarget(runtime, 'child', 'msg-child', 'manager', 'bob', 'parent')

  await enterProviderStep(runtime, 'parent', 'msg-parent', [])
  endProviderStep(runtime, 'parent', 'msg-parent', 'run-parent-0')
  assert.deepEqual(capacitySnapshot(runtime).tokenStateCounts, { idle: 1, inFlight: 0, retiring: 0 })

  await enterProviderStep(runtime, 'child', 'msg-child', [])
  assert.deepEqual(
    capacitySnapshot(runtime).tokenStateCounts,
    { idle: 0, inFlight: 1, retiring: 0 },
    'descendant borrow holds the owner credit in flight',
  )

  // Leave the borrower's step open (the Long Stroke failure mode: EndStep blocked / never arrives).
  // Owner re-entering messages.transform must reclaim — not hang behind the foreign InFlight step.
  const parentNext = enterProviderStep(runtime, 'parent', 'msg-parent', ['run-parent-0'])
  assert.deepEqual(
    capacitySnapshot(runtime).waiters.map((waiter) => waiter.sessionId),
    [],
    'owner transform-entry reclaim grants immediately; must not leave the owner waiting behind a foreign InFlight borrow',
  )
  assert.deepEqual(capacitySnapshot(runtime).tokenStateCounts, { idle: 0, inFlight: 1, retiring: 0 })
  await parentNext
  suppressProviderStep(runtime, 'parent', 'msg-parent')
  assert.deepEqual(capacitySnapshot(runtime).tokenStateCounts, { idle: 1, inFlight: 0, retiring: 0 })
})
test('WHAT[execution-model-routing-010] EMR_010_older_borrowed_step_precedes_later_owned_step', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { engineer: [only], manager: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'engineer', 'alice')
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
    'borrowed waiter precedes later owned waiter for the same credit',
  )
  await childStep
  suppressProviderStep(runtime, 'child', 'msg-child')

  assert.deepEqual(
    capacitySnapshot(runtime).waiters.map((waiter) => waiter.sessionId),
    [],
  )
  await parentNextStep
  suppressProviderStep(runtime, 'parent', 'msg-parent')
  assert.deepEqual(capacitySnapshot(runtime).tokenStateCounts, { idle: 1, inFlight: 0, retiring: 0 })
  assert.equal(snapshotOccupied(runtime).length, 1)
})
test('WHAT[execution-model-routing-010] EMR_010_credit_never_crosses_provider_boundary', async () => {
  const a = target('provider-a/model')
  const b = target('provider-b/model')
  const runtime = createRuntime(providerLimited(
    { 'provider-a': 1, 'provider-b': 1 },
    { engineer: [a], manager: [b] },
  ))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'engineer', 'alice')
  const child = await beginExecutionAdmission(runtime, 'child', 'msg-child', 'manager', 'bob', 'parent')
  assert.equal(child.kind, 'Acquired', 'a borrower needing another provider takes ordinary capacity')
  assert.equal(key(executionAdmissionTarget(runtime, child.lease)), 'provider-b/model|none')
  assert.equal(snapshotOccupied(runtime).length, 2, 'no provider token is shared across providers')

  cancelPendingExecution(runtime, 'child')
})
test('WHAT[execution-model-routing-010] EMR_010_reservation_borrowing_shares_one_token', async () => {
  const runtime = createRuntime(() => target('provider/shared'))

  const first = tryReserveManaged(runtime, 'parent', 'engineer', null)
  const second = tryReserveManaged(runtime, 'child', 'engineer', 'parent')
  assert.equal(key(first), 'provider/shared|none')
  assert.equal(key(second), 'provider/shared|none')
  assert.equal(capacitySnapshot(runtime).ledgerEntries.length, 1, 'an explicit lender reservation duplicates no capacity')
  assert.equal(snapshotOccupied(runtime).length, 1)
})
}

test.todo('WHAT[execution-model-routing-010] actual tool entry ends the exact provider step before capability denial or tool body, including a synchronous descendant wait (GAP-128)')
