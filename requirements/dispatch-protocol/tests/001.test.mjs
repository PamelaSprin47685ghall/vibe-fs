import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'

test('WHAT[DISPATCH-PROTOCOL-001] DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime', () => {
  const members = ['sendDirect', 'sendDetached', 'sendWithReceipt', 'abandonClaim', 'registerClaim', 'resolvePhysicalAccepted']
  for (const member of members) {
    assert.equal(typeof dispatch[member], 'function', `${member} must exist on PromptDispatcher`)
  }
})
