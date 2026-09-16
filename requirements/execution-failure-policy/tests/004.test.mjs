import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as policy from '../../../dist/Execution/Failure/Surface.js'

const target = { model: 'provider/model', reasoning: 'none' }
const identity = (physicalUserMessageId) => ({
  sessionId: 'session-capacity', physicalUserMessageId, role: 'coder', participant: 'coder',
  target,
})

const acquire = (runtime, physicalUserMessageId) =>
  routing.acquireExecutionAdmission(
    runtime,
    'session-capacity',
    physicalUserMessageId,
    'coder',
    'coder',
    null,
  )

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

test('WHAT[EXECFAIL-004] wrong exact capacity fence identity returns closed conflict', async () => {
  const runtime = routing.createRuntime(() => target)
  const acquired = await acquire(runtime, 'physical-1')
  assert.equal(acquired.kind, 'Acquired')
  const wrong = routing.commitExecutionAdmission(runtime, acquired.lease, identity('physical-other'))
  assert.deepEqual(wrong, { kind: 'Conflict' })
})

test('WHAT[EXECFAIL-004] stale exact capacity fence is closed without exposing handle', async () => {
  const runtime = routing.createRuntime(() => target)
  const acquired = await acquire(runtime, 'physical-2')
  const successor = await acquire(runtime, 'physical-3')
  assert.equal(successor.kind, 'Acquired')
  const stale = routing.commitExecutionAdmission(runtime, acquired.lease, identity('physical-2'))
  assert.deepEqual(stale, { kind: 'StaleFence' })
  assert.ok(!Object.keys(stale).some((key) => /fence|lease/i.test(key)))
})

test('WHAT[EXECFAIL-004] capacity settlement preserves the exact opaque fence reference', () => {
  const release = decide({ failure: 'ProtocolRejection' }).capacitySettlement
  assert.deepEqual(release, { kind: 'ReleaseExactFence', fenceReference: capacityFence.reference })

  const retain = decide({
    failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' },
  }).capacitySettlement
  assert.deepEqual(retain, { kind: 'RetainExactFence', fenceReference: capacityFence.reference })

  assert.deepEqual(
    decide({ failure: 'ProtocolRejection', capacityFence: null }).capacitySettlement,
    { kind: 'NoCapacitySettlement' },
  )
  assert.deepEqual(decide({ failure: 'ProtocolRejection', phase: 'NoAcceptedFact' }).capacitySettlement, {
    kind: 'NoCapacitySettlement',
  })
})
