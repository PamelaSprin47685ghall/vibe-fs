import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

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
