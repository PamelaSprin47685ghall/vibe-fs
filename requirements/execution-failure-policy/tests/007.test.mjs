import assert from 'node:assert/strict'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as policy from '../../../dist/Execution/Failure/Surface.js'
import { input, executionKey, capacityFence, phases } from './support/policy-input.mjs'
const decide = (change) => policy.decide(input(change))

test('WHAT[execution-failure-policy-007] writer outcome decoding preserves commitment and diagnostic independently', () => {
  for (const [kind, commitment] of [['WriterUnavailable', 'NotCommitted'], ['FactRejected', 'Committed'], ['WriteUnknown', 'Unknown']]) {
    assert.deepEqual(journal.JournalSurface_mapAppendFailure({ kind, diagnostic: 'receipt absent' }), {
      failure: 'PersistenceFailure', commitment, diagnostic: 'receipt absent',
    })
  }
})

test('WHAT[execution-failure-policy-007] production decisions preserve NotCommitted and route Unknown to reconciliation', () => {
  for (const phase of phases) {
    for (const fence of [null, capacityFence]) {
      const decision = decide({ phase, capacityFence: fence, failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' } })
      assert.equal(decision.resolution, 'PreserveCurrentFact')
      assert.equal(decision.authorization, null)
      assert.equal(decision.breaker.kind, 'NoBreakerTransition')
      assert.deepEqual(decision.capacitySettlement, fence === null ? { kind: 'NoCapacitySettlement' } : {
        kind: 'RetainExactFence', fenceReference: fence.reference,
      })
      assert.equal(decision.fatality.kind, 'NoFatality')
    }
  }
  for (const failure of ['AcceptanceUnknown', { kind: 'PersistenceFailure', commitment: 'Unknown' }]) {
    const decision = decide({ failure })
    assert.equal(decision.resolution, 'AwaitAcceptanceReconciliation')
    assert.equal(decision.authorization, null)
    assert.equal(decision.breaker.kind, 'NoBreakerTransition')
    assert.deepEqual(decision.capacitySettlement, { kind: 'RetainExactFence', fenceReference: capacityFence.reference })
    assert.deepEqual(decision.executionKey, executionKey)
  }
  const committed = decide({ failure: { kind: 'PersistenceFailure', commitment: 'Committed' } })
  assert.equal(committed.resolution, 'PreserveCurrentFact')
  assert.equal(committed.capacitySettlement.kind, 'ReleaseExactFence')
  assert.equal(committed.fatality.kind, 'FatalAfterSettlement')
})

test.todo('WHAT[execution-failure-policy-007] GAP-120 unresolved real acceptance or persistence never repeats provider dispatch or capacity effects across restart')
