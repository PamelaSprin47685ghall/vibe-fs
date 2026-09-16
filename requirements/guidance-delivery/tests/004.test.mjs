import assert from 'node:assert/strict'
import test from 'node:test'
import * as tdp from '../../../dist/Enforcer/TipDeliveryProjectionSurface.js'
import * as tg from '../../../dist/Enforcer/TipGuidanceDeliverySurface.js'
import * as nudge from '../../../dist/Enforcer/LatestTipNudgeSurface.js'

test('WHAT[GD-004] ENFORCER_TIP_NUDGE_001_latest_tip_first_delivery_is_full_main_md', () => {
  const state = nudge.createState()
  const res = nudge.latestNudge(state, 'ses-1', 'tip-sample')
  assert.match(res, /# Enforcer Tip/)
})

test('WHAT[GD-004] TDP_001_delivery_decision_purely_folds_durable_facts', () => {
  const state = tdp.createState()
  assert.equal(tdp.isDelivered(state, 'tip-1'), false)
  tdp.applyFull(state, 'occ-1', 'tip-1')
  assert.equal(tdp.isDelivered(state, 'tip-1'), true)
})

test('WHAT[GD-004] ENFORCER_TIP_DELIVERY_003_delivery_decision_survives_restart', () => {
  const state1 = tg.createDeliveryState()
  const facts = tg.recordDeliveryFacts(state1, 'ses-1', 'tip-sample')
  const state2 = tg.replayDeliveryFacts(facts)
  const res = tg.resolveTip(state2, 'ses-1', 'tip-sample')
  assert.equal(res.kind, 'IdentityOnly')
})
