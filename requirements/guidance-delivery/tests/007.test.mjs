import assert from 'node:assert/strict'
import test from 'node:test'
import * as tg from '../../../dist/Enforcer/TipGuidanceDeliverySurface.js'
import * as nudge from '../../../dist/Enforcer/LatestTipNudgeSurface.js'

test('WHAT[GD-007] ENFORCER_TIP_NUDGE_001b_latestTipNudge_is_same_bytes_as_latestTipGuidance', () => {
  const state = tg.createDeliveryState()
  const g = tg.latestTipGuidance(state, 'ses-1', 'tip-sample')
  const n = nudge.latestTipNudge(state, 'ses-1', 'tip-sample')
  assert.equal(g, n)
})

test('WHAT[GD-007] latestTipGuidance_and_latestTipNudge_byte_parity_on_identity', () => {
  const state = tg.createDeliveryState()
  tg.resolveTip(state, 'ses-1', 'tip-sample')
  const g = tg.latestTipGuidance(state, 'ses-1', 'tip-sample')
  const n = nudge.latestTipNudge(state, 'ses-1', 'tip-sample')
  assert.equal(g, n)
  assert.equal(g, 'tip = "tip-sample"')
})
