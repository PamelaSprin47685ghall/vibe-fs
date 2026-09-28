import assert from 'node:assert/strict'
import test from 'node:test'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const sameTarget = { model: 'provider/shared', reasoning: 'none' }

test('WHAT[execution-model-routing-008] SDK prompt never invents a model from agent name', async () => {
  let payload
  const port = routing.createSdkClientPort({ session: { promptAsync: async (value) => {
    payload = value
    return {}
  } } })
  await routing.sendPrompt(port, 'session', 'hello', { agent: 'engineer', bindingIntent: 'Preserve' })
  assert.equal(payload.model, undefined)
  assert.equal(payload.variant, undefined)
  assert.equal(payload.body.model, undefined)
  assert.equal(payload.body.variant, undefined)
})

test('WHAT[execution-model-routing-008] distinct current roles may hold the same physical target', async () => {
  const runtime = routing.createRuntime(() => sameTarget)
  for (const role of ['engineer', 'devops', 'manager', 'orchestrator', 'blogger']) {
    const acquired = await routing.acquireExecutionAdmission(runtime, role, `msg-${role}`, role, role, null)
    assert.equal(acquired.kind, 'Acquired')
    assert.deepEqual(routing.executionAdmissionTarget(runtime, acquired.lease), sameTarget)
  }
  assert.deepEqual(routing.snapshotOccupied(runtime), Array.from({ length: 5 }, () => sameTarget))
  for (const role of ['engineer', 'devops', 'manager', 'orchestrator', 'blogger']) {
    routing.releasePhysicalExecution(runtime, role, `msg-${role}`)
  }
})

test('WHAT[execution-model-routing-008] actual plugin ignores configured Host agent model and incoming message model', async () => {
  await withExecutablePlugin(async (hooks) => {
    const output = { message: {
      id: 'msg-config-precedence', role: 'user', sessionID: 'ses-config-precedence', agent: 'engineer',
      model: { providerID: 'host', modelID: 'override-attempt' },
    }, parts: [] }
    await hooks['chat.message']({ sessionID: 'ses-config-precedence', messageID: 'msg-config-precedence', agent: 'engineer' }, output)
    assert.deepEqual({ ...output.message.model }, { providerID: 'provider', modelID: 'engineer-model', variant: 'none' })
  })
})
