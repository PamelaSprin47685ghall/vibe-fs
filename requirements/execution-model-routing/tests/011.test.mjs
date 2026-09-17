import assert from 'node:assert/strict'
import test from 'node:test'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const target = { model: 'provider/shared', reasoning: 'none' }

const identity = (overrides = {}) => ({
  sessionId: 'session-a',
  physicalUserMessageId: 'message-a',
  role: 'engineer',
  participant: 'alice',
  target,
  ...overrides,
})

const acquire = async (runtime, exact = identity()) => {
  const outcome = await routing.acquireExecutionAdmission(
    runtime,
    exact.sessionId,
    exact.physicalUserMessageId,
    exact.role,
    exact.participant,
    exact.lenderSessionId ?? null,
  )
  assert.equal(outcome.kind, 'Acquired')
  return outcome.lease
}

const conflict = (outcome) => assert.deepEqual(outcome, { kind: 'Conflict' })

test('WHAT[EMR-011] rejects release with the wrong physical fence', async () => {
  const owner = routing.createRuntime(() => target)
  const wrongOwner = routing.createRuntime(() => target)
  const lease = await acquire(owner)

  assert.deepEqual(routing.releaseExecutionAdmissionBeforeProvider(wrongOwner, lease, identity()), {
    kind: 'StaleFence',
  })
  assert.equal(routing.executionAdmissionLifecycle(owner, lease), 'Pending')
  assert.equal(routing.snapshotOccupied(owner).length, 1)
})
