import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const target = { model: 'provider/model', reasoning: 'none' }
const identity = (physicalUserMessageId) => ({
  sessionId: 'session-capacity', physicalUserMessageId, effectiveAgent: 'coder',
  target,
})

test('WHAT[EXECFAIL-004] wrong exact capacity fence identity returns closed conflict', async () => {
  const runtime = routing.createRuntime(() => target)
  const acquired = await routing.acquireExecutionAdmission(runtime, 'session-capacity', 'physical-1', 'coder')
  assert.equal(acquired.kind, 'Acquired')
  const wrong = routing.commitExecutionAdmission(runtime, acquired.lease, identity('physical-other'))
  assert.deepEqual(wrong, { kind: 'Conflict' })
})

test('WHAT[EXECFAIL-004] stale exact capacity fence is closed without exposing handle', async () => {
  const runtime = routing.createRuntime(() => target)
  const acquired = await routing.acquireExecutionAdmission(runtime, 'session-capacity', 'physical-2', 'coder')
  const successor = await routing.acquireExecutionAdmission(runtime, 'session-capacity', 'physical-3', 'coder')
  assert.equal(successor.kind, 'Acquired')
  const stale = routing.commitExecutionAdmission(runtime, acquired.lease, identity('physical-2'))
  assert.deepEqual(stale, { kind: 'StaleFence' })
  assert.ok(!Object.keys(stale).some((key) => /fence|lease/i.test(key)))
})
