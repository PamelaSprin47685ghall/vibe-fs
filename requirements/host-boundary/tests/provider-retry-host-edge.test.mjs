import assert from 'node:assert/strict'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as signals from '../../../dist/OpenCode/Host/HostSignalSurface.js'

const target = { model: 'provider/retry-stable', reasoning: 'none' }

test('WHAT[HOST-BOUNDARY-001] HOST_001_failed_provider_step_keeps_same_physical_execution_binding_for_host_retry', async () => {
  const runtime = routing.createRuntime(() => target)
  const sessionId = 'session-retry'
  const physicalUserMessageId = 'msg-retry'
  const agent = 'fast-coder'

  const acquisition = await routing.acquireManaged(runtime, sessionId, physicalUserMessageId, agent)
  assert.equal(acquisition.kind, 'Acquired')
  await routing.enterProviderStep(runtime, sessionId, physicalUserMessageId, [])

  const failedAssistant = {
    type: 'message.updated',
    properties: {
      info: {
        sessionID: sessionId,
        id: 'run-failed',
        role: 'assistant',
        parentID: physicalUserMessageId,
        time: { created: 1, completed: 2 },
        error: { name: 'ProviderError' },
      },
    },
  }

  const stepEnd = signals.tryDecodeProviderStepEnd(failedAssistant)
  assert.deepEqual(stepEnd, {
    sessionId,
    physicalUserMessageId,
    providerRun: 'run-failed',
  })
  routing.endProviderStep(runtime, stepEnd.sessionId, stepEnd.physicalUserMessageId, stepEnd.providerRun)

  const physicalEnd = signals.tryDecodePhysicalExecutionEnd(failedAssistant)
  assert.equal(physicalEnd, null)
  if (physicalEnd !== null) {
    routing.releasePhysicalExecution(runtime, physicalEnd.sessionId, physicalEnd.physicalUserMessageId)
  }

  await routing.enterProviderStep(runtime, sessionId, physicalUserMessageId, ['run-failed'])
  assert.deepEqual(routing.tryLease(runtime, sessionId, physicalUserMessageId, agent), target)
})

test('WHAT[HOST-BOUNDARY-001] HOST_001_ambiguous_finish_keeps_same_physical_execution_binding_for_host_retry', async () => {
  for (const finish of ['unknown', 'error']) {
    const runtime = routing.createRuntime(() => target)
    const sessionId = `session-retry-${finish}`
    const physicalUserMessageId = `msg-retry-${finish}`
    const providerRun = `run-${finish}`
    const agent = 'fast-coder'

    const acquisition = await routing.acquireManaged(runtime, sessionId, physicalUserMessageId, agent)
    assert.equal(acquisition.kind, 'Acquired')
    await routing.enterProviderStep(runtime, sessionId, physicalUserMessageId, [])

    // OpenCode can normalize an upstream streaming failure into a completed
    // assistant with an ambiguous finish and no error field, then retry the
    // same PhysicalUserMessageId. That ends the provider step, not the physical
    // execution.
    const ambiguousAssistant = {
      type: 'message.updated',
      properties: {
        info: {
          sessionID: sessionId,
          id: providerRun,
          role: 'assistant',
          parentID: physicalUserMessageId,
          time: { created: 1, completed: 2 },
          finish,
        },
      },
    }

    const stepEnd = signals.tryDecodeProviderStepEnd(ambiguousAssistant)
    assert.deepEqual(stepEnd, { sessionId, physicalUserMessageId, providerRun })
    routing.endProviderStep(runtime, stepEnd.sessionId, stepEnd.physicalUserMessageId, stepEnd.providerRun)

    const physicalEnd = signals.tryDecodePhysicalExecutionEnd(ambiguousAssistant)
    assert.equal(physicalEnd, null)
    if (physicalEnd !== null) {
      routing.releasePhysicalExecution(runtime, physicalEnd.sessionId, physicalEnd.physicalUserMessageId)
    }

    await routing.enterProviderStep(runtime, sessionId, physicalUserMessageId, [providerRun])
    assert.deepEqual(routing.tryLease(runtime, sessionId, physicalUserMessageId, agent), target)
  }
})
