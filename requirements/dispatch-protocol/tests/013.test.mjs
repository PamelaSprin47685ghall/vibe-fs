import assert from 'node:assert/strict'
import test from 'node:test'
import * as dispatch from '../../../dist/Interaction/Dispatch/OpenCode/Surface.js'

test('WHAT[DISPATCH-PROTOCOL-013] DP_013_construction_waits_for_durability_activation_before_explicit_recovery', () => {
  const dispatcher = dispatch.createDispatcher({ activateDurability: false })
  assert.equal(dispatch.isRecoveryStarted(dispatcher), false)
  dispatch.activateDurability(dispatcher)
  assert.equal(dispatch.isRecoveryStarted(dispatcher), true)
})
