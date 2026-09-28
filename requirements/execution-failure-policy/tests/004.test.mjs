import assert from 'node:assert/strict'
import test from 'node:test'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as policy from '../../../dist/Execution/Failure/Surface.js'
import { input, capacityFence } from './support/policy-input.mjs'

const target = { model: 'provider/model', reasoning: 'none' }
const identity = (physicalUserMessageId) => ({
  sessionId: 'session-capacity', physicalUserMessageId, role: 'engineer', participant: 'engineer', target,
})
const acquire = (runtime, physical) => routing.acquireExecutionAdmission(runtime, 'session-capacity', physical, 'engineer', 'engineer', null)

test('WHAT[execution-failure-policy-004] actual capacity admission rejects wrong physical identity and stale lease', async () => {
  const runtime = routing.createRuntime(() => target)
  const acquired = await acquire(runtime, 'physical-1')
  assert.equal(acquired.kind, 'Acquired')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, acquired.lease, identity('other')), { kind: 'Conflict' })
  const successor = await acquire(runtime, 'physical-2')
  assert.equal(successor.kind, 'Acquired')
  assert.deepEqual(routing.commitExecutionAdmission(runtime, acquired.lease, identity('physical-1')), { kind: 'StaleFence' })
  assert.deepEqual(routing.commitExecutionAdmission(runtime, successor.lease, identity('physical-2')), { kind: 'Applied' })
})

test('WHAT[execution-failure-policy-004] policy translation preserves supplied fence for release or retention', () => {
  assert.deepEqual(policy.decide(input()).capacitySettlement, { kind: 'ReleaseExactFence', fenceReference: capacityFence.reference })
  assert.deepEqual(policy.decide(input({ failure: { kind: 'PersistenceFailure', commitment: 'NotCommitted' } })).capacitySettlement, {
    kind: 'RetainExactFence', fenceReference: capacityFence.reference,
  })
  assert.deepEqual(policy.decide(input({ capacityFence: null })).capacitySettlement, { kind: 'NoCapacitySettlement' })
  assert.deepEqual(policy.decide(input({ phase: 'NoAcceptedFact' })).capacitySettlement, { kind: 'NoCapacitySettlement' })
})

test.todo('WHAT[execution-failure-policy-004] GAP-120 compiler rejects forged opaque fence and actual release owner preserves capacity on wrong target or execution')
