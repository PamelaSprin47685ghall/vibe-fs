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
    assert.equal(routing.pendingBound(runtime), 32)

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

test('WHAT[EMR-010] seeded lender soak shares one physical credit without retained settlement nodes', async (context) => {
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
const { readdir, readFile } = await import("node:fs/promises");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");

const repoRoot = fileURLToPath(new URL('../../../', import.meta.url))
const srcRoot = join(repoRoot, 'src/Wanxiangshu')
const toRelative = (absPath) => {
  const rel = absPath.startsWith(repoRoot) ? absPath.slice(repoRoot.length) : absPath
  return rel.replace(/\\/g, '/')
}
async function getAllProductionFsFiles(dir) {
  const entries = await readdir(dir, { withFileTypes: true })
  const nested = await Promise.all(
    entries.map(async (entry) => {
      const fullPath = join(dir, entry.name)
      if (entry.isDirectory()) {
        return getAllProductionFsFiles(fullPath)
      } else if (entry.isFile() && entry.name.endsWith('.fs')) {
        return [fullPath]
      }
      return []
    })
  )
  return nested.flat()
}
const EXCLUSIVE_PRIVATE_KNOWLEDGE = Object.freeze([
  // Private types & DU states
  'CapacityStep',
  'CapacityCreditState',
  'CapacityCredit',
  'CapacityStepDemand',
  'CapacityCreditSource',

  // Private mutable state / dictionary resources
  'ownedTokenByExecution',
  'creditSourceByExecution',
  'nextCapacityDemandSequence',

  // Private borrowing, recall & reservation algorithm helpers
  'currentCreditSource',
  'clearCreditSource',
  'clearCreditSourcesForToken',
  'clearCreditSourcesForSession',
  'rememberCreditSource',
  'moveCreditSource',
  'isRetiring',
  'releaseToken',
  'retireToken',
  'retireTokenId',
  'retireExecution',
  'creditTokens',
  'withoutTokens',
  'schedulingView',
  'capacityOrdinaryDecision',
  'attributedDecision',
  'matchingCreditDecision',
  'routeDecision',
  'acquireOwnedToken',
  'moveOwnedToken',
  'finishStep',
  'reconcileFence',
  'tryGrantOwned',
  'tryGrantBorrowed',
  'demandOwnsToken',
  'tryGrantOrdinary',
  'tryGrantDemand',
  'acquireForRoute',
  'recordRoutedCredit',
  'applyRoutedToken',
  'commitRoutedTarget',
  'ensureReservationToken',
  'recordReservationCredit',
  'adoptOwnedToken',
])
const CONTROLLED_PUBLIC_TYPES = Object.freeze([
  'CapacityLedger',
  'BorrowingCapacity',
])
const OWNER_FILES = Object.freeze([
  'src/Wanxiangshu/OpenCode/Host/ModelCapacity/Model.fs',
  'src/Wanxiangshu/OpenCode/Host/ModelCapacity/Ledger.fs',
  'src/Wanxiangshu/OpenCode/Host/ModelCapacity/Queue.fs',
  'src/Wanxiangshu/OpenCode/Host/ModelCapacity/Borrowing.fs',
  'src/Wanxiangshu/OpenCode/Host/ModelCapacity/Surface.fs',
])
const PERMITTED_CONSUMER_FILES = Object.freeze([
  ...OWNER_FILES,
  'src/Wanxiangshu/OpenCode/Host/ModelRouting.fs',
])

test('WHAT[EMR-010] EMR_010_model_capacity_owner_defines_all_private_borrowing_knowledge', async () => {
  const ownerContent = (
    await Promise.all(OWNER_FILES.map((file) => readFile(join(repoRoot, file), 'utf8')))
  ).join('\n')

  for (const identifier of EXCLUSIVE_PRIVATE_KNOWLEDGE) {
    const pattern = new RegExp(`\\b${identifier}\\b`)
    assert.match(
      ownerContent,
      pattern,
      `ModelCapacity owner file must define knowledge identifier: ${identifier}`
    )
  }

  for (const publicType of CONTROLLED_PUBLIC_TYPES) {
    const pattern = new RegExp(`\\b${publicType}\\b`)
    assert.match(
      ownerContent,
      pattern,
      `ModelCapacity owner file must define controlled public type: ${publicType}`
    )
  }
})
test('WHAT[EMR-010] EMR_010_model_capacity_private_knowledge_is_exclusive_to_owner', async () => {
  const allFsFiles = await getAllProductionFsFiles(srcRoot)
  assert.ok(allFsFiles.length >= 600, `Expected at least 600 production files, got ${allFsFiles.length}`)

  const violations = []

  for (const fileAbs of allFsFiles) {
    const relPath = toRelative(fileAbs)
    if (OWNER_FILES.includes(relPath)) {
      continue
    }

    const content = await readFile(fileAbs, 'utf8')
    for (const identifier of EXCLUSIVE_PRIVATE_KNOWLEDGE) {
      const pattern = new RegExp(`\\b${identifier}\\b`)
      if (pattern.test(content)) {
        violations.push({
          file: relPath,
          leakedIdentifier: identifier,
        })
      }
    }
  }

  assert.deepEqual(
    violations,
    [],
    `Private ModelCapacity borrowing/lineage knowledge leaked outside owner modules (${OWNER_FILES.join(', ')}). Violations: ${JSON.stringify(violations, null, 2)}`
  )
})
test('WHAT[EMR-010] EMR_010_capacity_ledger_and_borrowing_capacity_types_are_restricted_to_permitted_zones', async () => {
  const allFsFiles = await getAllProductionFsFiles(srcRoot)
  const violations = []

  for (const fileAbs of allFsFiles) {
    const relPath = toRelative(fileAbs)
    if (PERMITTED_CONSUMER_FILES.includes(relPath)) {
      continue
    }

    const content = await readFile(fileAbs, 'utf8')
    for (const publicType of CONTROLLED_PUBLIC_TYPES) {
      const pattern = new RegExp(`\\b${publicType}\\b`)
      if (pattern.test(content)) {
        violations.push({
          file: relPath,
          leakedType: publicType,
        })
      }
    }
  }

  assert.deepEqual(
    violations,
    [],
    `Controlled ModelCapacity types appeared outside permitted zones (${PERMITTED_CONSUMER_FILES.join(', ')}). Violations: ${JSON.stringify(violations, null, 2)}`
  )
})
test('WHAT[EMR-010] EMR_010_exclusivity_test_is_refutable_and_fails_closed_on_violation', () => {
  // Test refutability: simulate a leaked knowledge identifier in non-owner content
  const simulatedLeakedContent = `
    namespace Wanxiangshu.SomeModule
    let decide = routeDecision None route
  `

  const checkContent = (content, identifiers) => {
    const matches = []
    for (const id of identifiers) {
      if (new RegExp(`\\b${id}\\b`).test(content)) {
        matches.push(id)
      }
    }
    return matches
  }

  const detected = checkContent(simulatedLeakedContent, EXCLUSIVE_PRIVATE_KNOWLEDGE)
  assert.deepEqual(
    detected,
    ['routeDecision'],
    'Exclusivity detector must reliably catch leaked identifiers'
  )

  const cleanContent = `
    namespace Wanxiangshu.SomeModule
    let doSomething () = ()
  `
  assert.deepEqual(
    checkContent(cleanContent, EXCLUSIVE_PRIVATE_KNOWLEDGE),
    [],
    'Clean content must produce zero violations'
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

test('WHAT[EMR-010] EMR_010_explicit_lender_credit_is_free_only_to_borrowers_not_global_waiters', async () => {
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
test('WHAT[EMR-010] EMR_010_absent_lender_queues_without_borrowing', async () => {
  const only = target('provider/only')
  const runtime = createRuntime(providerLimited({ provider: 1 }, { engineer: [only], manager: [only] }))

  await acquireTarget(runtime, 'parent', 'msg-parent', 'engineer', 'alice')
  const ghost = await beginExecutionAdmission(runtime, 'child', 'msg-child', 'manager', 'bob', 'ghost')
  assert.equal(ghost.kind, 'Queued', 'a lender with no credit authorizes nothing')

  cancelPendingExecution(runtime, 'child')
  assert.equal((await awaitQueuedExecutionAdmission(ghost.queue)).kind, 'Cancelled')
  assert.equal(snapshotOccupied(runtime).length, 1)
})
test('WHAT[EMR-010] EMR_010_borrowed_step_handoff_reuses_the_same_credit', async () => {
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
test('WHAT[EMR-010] EMR_010_owner_transform_entry_reclaims_foreign_inflight_borrow', async () => {
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
test('WHAT[EMR-010] EMR_010_older_borrowed_step_precedes_later_owned_step', async () => {
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
test('WHAT[EMR-010] EMR_010_credit_never_crosses_provider_boundary', async () => {
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
test('WHAT[EMR-010] EMR_010_reservation_borrowing_shares_one_token', async () => {
  const runtime = createRuntime(() => target('provider/shared'))

  const first = tryReserveManaged(runtime, 'parent', 'engineer', null)
  const second = tryReserveManaged(runtime, 'child', 'engineer', 'parent')
  assert.equal(key(first), 'provider/shared|none')
  assert.equal(key(second), 'provider/shared|none')
  assert.equal(capacitySnapshot(runtime).ledgerEntries.length, 1, 'an explicit lender reservation duplicates no capacity')
  assert.equal(snapshotOccupied(runtime).length, 1)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");

const source = async (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[EMR-010] EMR_010_borrowing_complexity_is_owned_only_by_the_capacity_decorator', async () => {
  const capacity = (
    await Promise.all([
      source('src/Wanxiangshu/OpenCode/Host/ModelCapacity/Model.fs'),
      source('src/Wanxiangshu/OpenCode/Host/ModelCapacity/Ledger.fs'),
      source('src/Wanxiangshu/OpenCode/Host/ModelCapacity/Queue.fs'),
      source('src/Wanxiangshu/OpenCode/Host/ModelCapacity/Borrowing.fs'),
      source('src/Wanxiangshu/OpenCode/Host/ModelCapacity/Surface.fs'),
    ])
  ).join('\n')
  const routing = await source('src/Wanxiangshu/OpenCode/Host/ModelRouting.fs')
  const sessions = await source('src/Wanxiangshu/OpenCode/Host/Sessions.fs')
  const binding = await source('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs')
  const transform = await source('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')
  const host = await source('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const scheduler = await source('resources/wanxiangshu.mjs')

  assert.match(capacity, /type internal CapacityLedger<'target>/)
  assert.match(capacity, /type internal BorrowingCapacity<'target>/)
  assert.match(capacity, /routeDecision/)
  assert.match(capacity, /CapacityCreditState/)
  assert.match(routing, /BorrowingCapacity<ModelRoutingTarget>/)
  assert.doesNotMatch(routing, /routeDecision|recordRoutedCredit|ownedTokenByExecution|creditSourceByExecution/)

  for (const main of [sessions, binding, transform, host]) {
    assert.doesNotMatch(main, /routeDecision|recordRoutedCredit|ownedTokenByExecution|creditSourceByExecution|CapacityCreditState|CapacityStepDemand/)
  }
  assert.doesNotMatch(scheduler, /borrow|recall|lineage|parentSession|childSession/i)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");

const root = new URL('../../../', import.meta.url)
const source = (path) => readFile(new URL(path, root), 'utf8')

test('WHAT[EMR-010] EMR_010_managed_tool_execution_ends_the_current_provider_step_before_tool_body', async () => {
  const [binding, registry] = await Promise.all([
    source('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs'),
    source('src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs'),
  ])

  assert.match(
    binding,
    /let endProviderStepAtToolBoundary[\s\S]*ProviderRunIdentity option[\s\S]*ModelRouting\.endProviderStep/,
    'the exact provider-attempt binding owns conversion from tool context identity to capacity step end',
  )

  const boundaryCall = 'SessionExecutionBinding.endProviderStepAtToolBoundary'
  const boundaryIndex = registry.indexOf(boundaryCall)

  assert.ok(boundaryIndex >= 0, 'ToolRegistry must cross the provider→tool capacity boundary')
  assert.match(
    registry,
    /let providerToolBoundary[\s\S]*SessionExecutionBinding\.endProviderStepAtToolBoundary/,
    'provider→tool handoff is a named outer execution stage',
  )
  assert.match(
    registry,
    /match providerToolBoundary ctx with[\s\S]*\| Ok\(\) -> return! execute(?:Tracked|AfterBoundary) args ctx/,
    'all later gates execute only after the provider boundary succeeds',
  )
  assert.match(
    registry,
    /let executeAfterBoundary[\s\S]*if isStrengthReplica ctx then[\s\S]*else[\s\S]*return! executeEstablished args ctx/,
    'strength and role/admission gates remain downstream of the provider boundary',
  )
  assert.match(
    registry,
    /match spec\.Admission with[\s\S]*OfficeRole[\s\S]*executeOffice[\s\S]*PrivateAttachment[\s\S]*executePrivateAttachment/,
    'the declared tool authority, not a guessed one, selects the admission path downstream of the boundary',
  )
  assert.match(
    registry,
    /let executeKnownRole[\s\S]*officeAdmission ctx role[\s\S]*return! original args ctx/,
    'after the outer handoff and gates, ToolRegistry still delegates to the original tool body',
  )
  assert.match(
    registry,
    /let executePrivateAttachment[\s\S]*if attachmentAdmission ctx then[\s\S]*return! original args ctx/,
    'an internal leaf tool also reaches the original body only after the provider boundary',
  )

  const boundarySlice = registry.slice(Math.max(0, boundaryIndex - 500), boundaryIndex + 1000)
  assert.doesNotMatch(
    boundarySlice,
    /DateTime|setTimeout|timer|sleep|TimeoutMs|milliseconds?/i,
    'provider→tool handoff is causal and must not depend on elapsed time',
  )
})
}
