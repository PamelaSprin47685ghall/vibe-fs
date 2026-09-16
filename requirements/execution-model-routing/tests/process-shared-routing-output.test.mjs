import assert from 'node:assert/strict'
import test from 'node:test'

import plugin from '../../../dist/OpenCode/Plugin/Plugin.js'
import { createEnvironment, managedConfig } from './support/process-shared-routing.mjs'

test('WHAT[EMR-009] EMR_009_chat_message_routes_when_session_id_is_carried_on_output_message', async () => {
  const environment = createEnvironment(plugin.server)
  const previousHome = process.env.HOME
  process.env.HOME = environment.home
  let hooks

  try {
    hooks = await environment.createPlugin('output-session-workspace')
    await hooks.config(managedConfig())

    const output = {
      message: {
        id: 'msg-output-only',
        role: 'user',
        sessionID: 'ses-output-only',
        agent: 'coder',
        model: { providerID: 'host', modelID: 'placeholder' },
      },
      parts: [],
    }

    await hooks['chat.message']({ messageID: 'msg-output-only' }, output)
    assert.deepEqual(
      [output.message.model.providerID, output.message.model.modelID, output.message.model.variant],
      ['provider', 'model-a', 'none'],
      'chat.message must decode sessionID from output.message and route successfully',
    )
  } finally {
    if (hooks) await hooks.dispose()
    process.env.HOME = previousHome
    environment.dispose()
  }
})
