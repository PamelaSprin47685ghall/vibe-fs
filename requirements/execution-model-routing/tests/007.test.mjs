import assert from 'node:assert/strict'
import test from 'node:test'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as signals from '../../../dist/OpenCode/Host/HostSignalSurface.js'

const acquire = async (runtime, sessionId, physicalUserMessageId, role = 'engineer') => {
  const result = await routing.acquireExecutionAdmission(runtime, sessionId, physicalUserMessageId, role, 'owner', null)
  assert.equal(result.kind, 'Acquired')
  const target = routing.executionAdmissionTarget(runtime, result.lease)
  assert.deepEqual(routing.commitExecutionAdmission(runtime, result.lease, {
    sessionId, physicalUserMessageId, role, participant: 'owner', target,
  }), { kind: 'Applied' })
  return target
}

test('WHAT[execution-model-routing-007] exact repeated release wakes a waiter once without removing its ownership', async () => {
  const runtime = routing.createRuntime((_role, running) => running.length === 0 ? { model: 'provider/one', reasoning: 'none' } : null)
  await acquire(runtime, 'holder', 'old')
  const waiting = acquire(runtime, 'waiter', 'fresh')
  routing.releasePhysicalExecution(runtime, 'holder', 'old')
  assert.deepEqual(await waiting, { model: 'provider/one', reasoning: 'none' })
  const before = routing.snapshotOccupied(runtime)
  routing.releasePhysicalExecution(runtime, 'holder', 'old')
  assert.deepEqual(routing.snapshotOccupied(runtime), before)
  assert.equal(before.length, 1)
  routing.releasePhysicalExecution(runtime, 'waiter', 'fresh')
})

test('WHAT[execution-model-routing-007] late release of superseded physical key preserves the exact current lease', async () => {
  const runtime = routing.createRuntime((role) => ({ model: `provider/${role}`, reasoning: 'none' }))
  await acquire(runtime, 'reused', 'old')
  const current = await acquire(runtime, 'reused', 'current', 'devops')
  routing.releasePhysicalExecution(runtime, 'reused', 'old')
  assert.deepEqual(routing.tryLease(runtime, 'reused', 'current', 'devops', 'owner', null), current)
  assert.equal(routing.snapshotOccupied(runtime).length, 1)
  routing.releasePhysicalExecution(runtime, 'reused', 'current')
  assert.deepEqual(routing.snapshotOccupied(runtime), [])
})

const assistant = (finish, extra = {}) => ({
  type: 'message.updated',
  properties: { info: {
    role: 'assistant', sessionID: 'session', id: 'provider-run', parentID: 'physical',
    time: { created: 1, completed: 2 }, finish, ...extra,
  } },
})

test('WHAT[execution-model-routing-007] actual decoder distinguishes successful physical end from tool and failed step end', () => {
  for (const finish of ['stop', 'length', 'content-filter']) {
    assert.deepEqual(signals.tryDecodePhysicalExecutionEnd(assistant(finish)), {
      sessionId: 'session', physicalUserMessageId: 'physical',
    })
  }
  for (const finish of ['tool-calls', 'unknown', 'error']) {
    assert.equal(signals.tryDecodePhysicalExecutionEnd(assistant(finish)), null)
    assert.ok(signals.tryDecodeProviderStepEnd(assistant(finish)))
  }
  for (const event of [
    assistant('stop', { error: { name: 'TimeoutError' } }),
    assistant('stop', { parentID: '' }),
    assistant('stop', { time: { created: 1 } }),
    { type: 'session.idle', properties: { sessionID: 'session' } },
  ]) {
    assert.equal(signals.tryDecodePhysicalExecutionEnd(event), null)
  }
})

test.todo('WHAT[execution-model-routing-007] actual Host event closes the physical execution only after durable terminal and idle/business events leave binding intact (GAP-128)')
