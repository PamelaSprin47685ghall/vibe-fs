import assert from 'node:assert/strict'
import test from 'node:test'

import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as policy from '../../../dist/Execution/Failure/Surface.js'

const executionKey = {
  sessionId: 'ses-failure-policy',
  physicalUserMessageId: 'msg-failure-policy',
}

const capacityFence = { reference: 'fence-failure-policy' }

const provider = {
  logicalRun: 'logical-failure-policy',
  providerRun: 'provider-failure-policy',
  requestKind: 'WorkMain',
  retryBudget: 'Available',
  breaker: 'Closed',
}

const baseInput = {
  failure: 'ProtocolRejection',
  phase: 'ProviderStarted',
  executionKey,
  capacityFence,
  provider,
}

const decide = (change = {}) => policy.decide({ ...baseInput, ...change })
const phases = ['NoAcceptedFact', 'AcceptedBeforeProvider', 'ProviderStarted', 'Terminal']
const capacityCases = [null, capacityFence]

test('WHAT[EXECFAIL-007] journal writer outcomes preserve exact persistence commitment', () => {
  assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind: 'WriterUnavailable', diagnostic: 'writer closing' }), {
    failure: 'PersistenceFailure', commitment: 'NotCommitted', diagnostic: 'writer closing',
  })
  assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind: 'FactRejected', diagnostic: 'durable semantic cut' }), {
    failure: 'PersistenceFailure', commitment: 'Committed', diagnostic: 'durable semantic cut',
  })
  assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind: 'WriteUnknown', diagnostic: 'flush receipt absent' }), {
    failure: 'PersistenceFailure', commitment: 'Unknown', diagnostic: 'flush receipt absent',
  })
})

test('WHAT[EXECFAIL-007] persistence commitment remains explicit and uncertainty reconciles without repeated effect', () => {
  const notCommitted = decide({
    failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  })
  assert.equal(notCommitted.resolution, 'PreserveCurrentFact')
  assert.equal(notCommitted.breaker.kind, 'NoBreakerTransition')
  assert.deepEqual(notCommitted.capacitySettlement, {
    kind: 'RetainExactFence',
    fenceReference: capacityFence.reference,
  })
  assert.equal(notCommitted.fatality.kind, 'NoFatality')

  for (const phase of phases) {
    for (const fence of capacityCases) {
      const decision = decide({
        phase,
        capacityFence: fence,
        failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
      })
      assert.equal(decision.resolution, 'PreserveCurrentFact')
      assert.equal(decision.breaker.kind, 'NoBreakerTransition')
      assert.equal(
        decision.capacitySettlement.kind,
        fence === null ? 'NoCapacitySettlement' : 'RetainExactFence',
      )
      assert.equal(decision.fatality.kind, 'NoFatality')
    }
  }

  for (const failure of [
    'AcceptanceUnknown',
    { kind: 'PersistenceFailure', commitment: 'Unknown' },
  ]) {
    const decision = decide({ failure })
    assert.equal(decision.resolution, 'AwaitAcceptanceReconciliation')
    assert.equal(decision.breaker.kind, 'NoBreakerTransition')
    assert.equal(decision.capacitySettlement.kind, 'RetainExactFence')
    assert.deepEqual(decision.executionKey, executionKey)
  }

  const committed = decide({
    failure: { kind: 'PersistenceFailure', commitment: 'Committed' },
  })
  assert.equal(committed.resolution, 'PreserveCurrentFact')
  assert.equal(committed.capacitySettlement.kind, 'ReleaseExactFence')
  assert.equal(committed.fatality.kind, 'FatalAfterSettlement')
})
