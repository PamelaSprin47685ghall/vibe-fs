import assert from 'node:assert/strict'
import test from 'node:test'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

// This exercises low-level SDK encoding; internal synthetic dispatch must still omit the model.
test('WHAT[execution-model-routing-009] SDK target encoding separates model identity from reasoning variant', async () => {
  let payload
  const port = routing.createSdkClientPort({ session: { promptAsync: async (value) => {
    payload = value
    return {}
  } } })
  await routing.sendPrompt(port, 'session', 'hello', {
    agent: 'engineer', bindingIntent: 'Preserve',
    model: { providerID: 'provider', modelID: 'model', variant: 'high' },
  })
  assert.deepEqual(payload.body.model, { providerID: 'provider', modelID: 'model' })
  assert.equal(payload.body.variant, 'high')
  assert.equal(payload.variant, 'high')
  assert.deepEqual(payload.model, { providerID: 'provider', modelID: 'model' })
})

test('WHAT[execution-model-routing-009] actual chat.message reads session identity from output and projects the configured scheduler target', async () => {
  await withExecutablePlugin(async (hooks) => {
    const output = { message: {
      id: 'msg-output-session', role: 'user', sessionID: 'ses-output-session', agent: 'engineer',
      model: { providerID: 'host', modelID: 'placeholder' },
    }, parts: [] }
    await hooks['chat.message']({ messageID: 'msg-output-session' }, output)
    assert.deepEqual({ ...output.message.model }, { providerID: 'provider', modelID: 'engineer-model', variant: 'none' })
  })
})

test.todo('WHAT[execution-model-routing-009] all actual internal synthetic send paths remain model-free and chat.params cannot create missing admission (GAP-128)')
