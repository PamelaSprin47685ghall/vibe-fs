import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const seed = 0x36c0ffee
const rounds = 32
const queueWidth = 32
const lineageCycles = 64
const capacity = 4
const admissionRetainedBound = 84
const lineageRetainedComposition = Object.freeze({
  ledgerEntries: 1,
  token: 1,
  ownerAndBorrowerCustodies: 2,
  executions: 2,
  pendingProviderWaiter: 1,
  owners: 2,
  lineage: 1,
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

const begin = (runtime, sessionId, physicalUserMessageId, effectiveAgent) =>
  routing.beginExecutionAdmission(runtime, sessionId, physicalUserMessageId, effectiveAgent)

const observedIdentity = (runtime, lease, record) => ({
  sessionId: record.sessionId,
  physicalUserMessageId: record.physicalUserMessageId,
  effectiveAgent: record.effectiveAgent,
  target: routing.executionAdmissionTarget(runtime, lease),
})

test('WHAT[EMR-014] seeded bounded admission soak preserves fairness and exact reconciliation after every operation', async (context) => {
  const next = seeded(seed)
  let blockedEligible = false
  const runtime = routing.createRuntime((role, running) => {
    if (running.length >= capacity) return null
    if (role.startsWith('blocked-') && !blockedEligible) return null
    return target
  })
  const auditor = createAuditor(runtime, admissionRetainedBound)
  const records = new Map()
  const active = []

  const commit = (record, lease) => {
    const observed = observedIdentity(runtime, lease, record)
    assert.deepEqual(routing.commitExecutionAdmission(runtime, lease, observed), { kind: 'Applied' })
    auditor.operation()
    active.push(record)
    return observed
  }

  const release = (record) => {
    assert.deepEqual(
      routing.releasePhysicalExecution(runtime, record.sessionId, record.physicalUserMessageId),
      { kind: 'Applied' },
    )
    auditor.operation()
  }

  for (let round = 0; round < rounds; round += 1) {
    blockedEligible = false
    active.length = 0
    records.clear()

    for (let index = 0; index < capacity; index += 1) {
      const record = {
        sessionId: `holder-${index}`,
        physicalUserMessageId: `holder-${round}-${index}`,
        effectiveAgent: `eligible-holder-${index}`,
      }
      const outcome = await begin(runtime, record.sessionId, record.physicalUserMessageId, record.effectiveAgent)
      assert.equal(outcome.kind, 'Acquired')
      auditor.operation()
      commit(record, outcome.lease)
    }

    const staleRecord = active.shift()
    const staleLeaseOutcome = await begin(
      runtime,
      staleRecord.sessionId,
      staleRecord.physicalUserMessageId,
      staleRecord.effectiveAgent,
    )
    assert.equal(staleLeaseOutcome.kind, 'Acquired')
    auditor.operation()
    const staleIdentity = observedIdentity(runtime, staleLeaseOutcome.lease, staleRecord)
    const replacement = { ...staleRecord, physicalUserMessageId: `${staleRecord.physicalUserMessageId}-new` }
    const replacementOutcome = await begin(
      runtime,
      replacement.sessionId,
      replacement.physicalUserMessageId,
      replacement.effectiveAgent,
    )
    assert.equal(replacementOutcome.kind, 'Acquired')
    auditor.operation()
    assert.deepEqual(
      routing.commitExecutionAdmission(
        runtime,
        staleLeaseOutcome.lease,
        staleIdentity,
      ),
      { kind: 'StaleFence' },
    )
    auditor.operation()
    commit(replacement, replacementOutcome.lease)

    const committed = active[0]
    const committedLease = await begin(
      runtime,
      committed.sessionId,
      committed.physicalUserMessageId,
      committed.effectiveAgent,
    )
    assert.equal(committedLease.kind, 'Acquired')
    auditor.operation()
    assert.deepEqual(
      routing.releaseExecutionAdmissionBeforeProvider(
        runtime,
        committedLease.lease,
        observedIdentity(runtime, committedLease.lease, committed),
      ),
      { kind: 'Conflict' },
    )
    auditor.operation()

    const order = shuffled(queueWidth, next)
    for (let position = 0; position < order.length; position += 1) {
      const index = order[position]
      const record = {
        sessionId: `waiter-${index}`,
        physicalUserMessageId: `waiter-${round}-${index}`,
        effectiveAgent:
          position === 0 || (position !== 1 && (next() & 3) === 0)
            ? `blocked-${index}`
            : `eligible-${index}`,
      }
      const outcome = await begin(runtime, record.sessionId, record.physicalUserMessageId, record.effectiveAgent)
      assert.equal(outcome.kind, 'Queued')
      auditor.operation()
      record.queue = outcome.queue
      records.set(exactKey(record), record)
    }

    for (const index of shuffled(queueWidth, next).slice(0, 3)) {
      const current = [...records.values()].find((record) => record.sessionId === `waiter-${index}`)
      if (!current) continue
      const replacementRecord = {
        ...current,
        physicalUserMessageId: `${current.physicalUserMessageId}-new`,
      }
      const replacementOutcome = await begin(
        runtime,
        replacementRecord.sessionId,
        replacementRecord.physicalUserMessageId,
        replacementRecord.effectiveAgent,
      )
      assert.equal(replacementOutcome.kind, 'Queued')
      auditor.operation()
      assert.equal((await routing.awaitQueuedExecutionAdmission(current.queue)).kind, 'Superseded')
      records.delete(exactKey(current))
      replacementRecord.queue = replacementOutcome.queue
      records.set(exactKey(replacementRecord), replacementRecord)
    }

    const overflow = await begin(runtime, `overflow-${round}`, `overflow-physical-${round}`, 'eligible-overflow')
    assert.deepEqual(
      { kind: overflow.kind, failure: overflow.failure },
      { kind: 'QueueFull', failure: 'CapacityQueueFull' },
    )
    assert.equal(routing.pendingCount(runtime), 32)
    auditor.operation()

    const cancellationRecords = shuffled(queueWidth, next)
      .map((index) => [...records.values()].find((candidate) => candidate.sessionId === `waiter-${index}`))
      .filter((record) => record?.effectiveAgent.startsWith('eligible-'))
      .slice(0, 4)
    assert.equal(cancellationRecords.length, 4)
    for (const record of cancellationRecords) {
      assert.deepEqual(routing.cancelPendingExecution(runtime, record.sessionId), { kind: 'Applied' })
      auditor.operation()
      assert.equal((await routing.awaitQueuedExecutionAdmission(record.queue)).kind, 'Cancelled')
      records.delete(exactKey(record))
      assert.deepEqual(routing.cancelPendingExecution(runtime, record.sessionId), { kind: 'AlreadyApplied' })
      auditor.operation()
    }

    const admitOldestEligible = async (eligible) => {
      const snapshot = routing.capacitySnapshot(runtime)
      const expected = snapshot.waiters
        .filter((waiter) => waiter.kind === 'Admission' && eligible(waiter.effectiveAgent))
        .sort((left, right) => left.sequence - right.sequence)[0]
      if (!expected) return false
      const released = active.shift()
      assert.ok(released, 'finite eligible schedule always has a releasable capacity owner')
      release(released)
      const record = records.get(exactKey(expected))
      const outcome = await routing.awaitQueuedExecutionAdmission(record.queue)
      assert.equal(outcome.kind, 'Acquired')
      records.delete(exactKey(record))
      commit(record, outcome.lease)
      return true
    }

    while (await admitOldestEligible((role) => role.startsWith('eligible-'))) {}
    assert.ok(records.size > 0, 'the seeded schedule retains an ineligible head')

    blockedEligible = true
    const duplicateCandidate = active[0]
    while (records.size > 0) assert.equal(await admitOldestEligible(() => true), true)
    while (active.length > 0) release(active.shift())

    assert.deepEqual(
      routing.releasePhysicalExecution(
        runtime,
        duplicateCandidate.sessionId,
        duplicateCandidate.physicalUserMessageId,
      ),
      { kind: 'AlreadyApplied' },
    )
    const drained = auditor.operation()
    assert.equal(routing.pendingCount(runtime), 0)
    assert.equal(drained.ledgerEntries.length, 0)
    assert.equal(drained.tokens.length, 0)
    assert.equal(drained.custodies.length, 0)
    assert.equal(drained.executions.length, 0)
    assert.equal(drained.waiters.length, 0)
    assert.equal(drained.owners.length, 0)
  }

  const impossibleRuntime = routing.createRuntime(() => target)
  const impossibleAuditor = createAuditor(impossibleRuntime)
  const exact = {
    sessionId: 'impossible-holder',
    physicalUserMessageId: 'impossible-physical',
    effectiveAgent: 'eligible-impossible',
  }
  const acquired = await begin(impossibleRuntime, exact.sessionId, exact.physicalUserMessageId, exact.effectiveAgent)
  assert.equal(acquired.kind, 'Acquired')
  const valid = impossibleAuditor.operation()
  const impossible = { ...valid, ledgerEntries: [] }
  const decision = routing.reconcileCapacityEvidence(impossible)
  assert.equal(decision.kind, 'FailClosed')
  assert.ok(decision.reasons.includes('MapLedgerDivergence'))
  assert.deepEqual(routing.capacitySnapshot(impossibleRuntime), valid, 'impossible evidence cannot repair owner state')

  const report = auditor.report()
  context.diagnostic(
    `task36 admission seed=${seed} rounds=${rounds} queueWidth=${queueWidth} operations=${report.operations} maxRetained=${report.maxRetained} retainedBound=${admissionRetainedBound}`,
  )
})

test('WHAT[EMR-010] seeded lineage soak recalls one physical credit without lost wake or retained settlement nodes', async (context) => {
  const runtime = routing.createRuntime((_role, running) => (running.length === 0 ? target : null))
  const auditor = createAuditor(runtime, lineageRetainedBound)

  for (let cycle = 0; cycle < lineageCycles; cycle += 1) {
    const parent = { sessionId: `parent-${cycle}`, physicalUserMessageId: `parent-physical-${cycle}`, effectiveAgent: 'parent' }
    const child = { sessionId: `child-${cycle}`, physicalUserMessageId: `child-physical-${cycle}`, effectiveAgent: 'child' }

    const parentOutcome = await begin(runtime, parent.sessionId, parent.physicalUserMessageId, parent.effectiveAgent)
    assert.equal(parentOutcome.kind, 'Acquired')
    auditor.operation()
    assert.deepEqual(
      routing.commitExecutionAdmission(runtime, parentOutcome.lease, observedIdentity(runtime, parentOutcome.lease, parent)),
      { kind: 'Applied' },
    )
    auditor.operation()

    routing.bindCapacityChild(runtime, parent.sessionId, child.sessionId)
    auditor.operation()
    const childOutcome = await begin(runtime, child.sessionId, child.physicalUserMessageId, child.effectiveAgent)
    assert.equal(childOutcome.kind, 'Acquired')
    auditor.operation()
    assert.deepEqual(
      routing.commitExecutionAdmission(runtime, childOutcome.lease, observedIdentity(runtime, childOutcome.lease, child)),
      { kind: 'Applied' },
    )
    auditor.operation()
    assert.equal(routing.capacitySnapshot(runtime).ledgerEntries.length, 1, 'lineage borrowing does not duplicate capacity')

    await routing.enterProviderStep(runtime, child.sessionId, child.physicalUserMessageId, [])
    auditor.operation()
    let recalled = false
    const recall = routing.enterProviderStep(runtime, parent.sessionId, parent.physicalUserMessageId, []).then(() => {
      recalled = true
    })
    await Promise.resolve()
    assert.equal(recalled, false)
    auditor.operation()

    routing.endProviderStep(runtime, child.sessionId, child.physicalUserMessageId, `child-run-${cycle}`)
    auditor.operation()
    await recall
    assert.equal(recalled, true, 'one causal provider-step end wakes the waiting owner')
    routing.endProviderStep(runtime, child.sessionId, child.physicalUserMessageId, `child-run-${cycle}`)
    auditor.operation()
    assert.equal(routing.capacitySnapshot(runtime).activeCount, 1, 'duplicate old end cannot release the recalled step')

    routing.endProviderStep(runtime, parent.sessionId, parent.physicalUserMessageId, `parent-run-${cycle}`)
    auditor.operation()
    assert.deepEqual(routing.releasePhysicalExecution(runtime, child.sessionId, child.physicalUserMessageId), {
      kind: 'Applied',
    })
    auditor.operation()
    assert.deepEqual(routing.releasePhysicalExecution(runtime, parent.sessionId, parent.physicalUserMessageId), {
      kind: 'Applied',
    })
    auditor.operation()
    routing.dropCapacityLineage(runtime, child.sessionId)
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
    `task36 lineage seed=${seed} cycles=${lineageCycles} operations=${report.operations} maxRetained=${report.maxRetained} retainedBound=${lineageRetainedBound} retainedComposition=${JSON.stringify(lineageRetainedComposition)}`,
  )
})
