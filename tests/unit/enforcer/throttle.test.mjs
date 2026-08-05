// ENFORCER-080..090 score-vector throttle exited product contract (tip v2).
// Domain/EnforcerThrottle.fs and Domain/EnforcerNudge.fs are compile tombstones only.

import assert from 'node:assert/strict'
import test from 'node:test'

test('ENFORCER_TIP_throttle_and_nudge_modules_are_tombstones', async () => {
  const throttle = await import('../../../dist/Domain/EnforcerThrottle.js')
  const nudge = await import('../../../dist/Domain/EnforcerNudge.js')
  assert.equal(throttle.Removed ?? throttle.EnforcerThrottle_Removed, true)
  assert.equal(nudge.Removed ?? nudge.EnforcerNudge_Removed, true)
})
