import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'

test('WHAT[DISPATCH-PROTOCOL-011] PROMPT_006_send_payload_carries_prompt_key_metadata', () => {
  const payload = dispatch.formatUserMessagePayload({
    promptKey: 'pk-exact-123',
    origin: 'synthesized-repair',
    content: 'please check invariant',
  })
  assert.equal(payload.promptKey, 'pk-exact-123')
  assert.equal(payload.origin, 'synthesized-repair')
  assert.equal(payload.content, 'please check invariant')
})
