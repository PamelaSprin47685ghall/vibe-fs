import assert from 'node:assert/strict'
import test from 'node:test'
import * as tg from '../../../dist/Enforcer/TipGuidanceDeliverySurface.js'
import * as nudge from '../../../dist/Enforcer/LatestTipNudgeSurface.js'

test('WHAT[GD-006] ENFORCER_TIP_DELIVERY_004_blogger_session_id_resolves_owner_main', () => {
  const state = tg.createDeliveryState()
  tg.bindSessionOwner(state, 'blogger-sub-1', 'main-1')
  const res = tg.resolveTip(state, 'blogger-sub-1', 'tip-sample')
  assert.equal(res.ownerSessionId, 'main-1')
})

test('WHAT[GD-006] ENFORCER_TIP_DELIVERY_005_missing_tip_returns_none', () => {
  const state = tg.createDeliveryState()
  const res = tg.resolveTip(state, 'ses-1', 'nonexistent-tip')
  assert.equal(res, null)
})

test('WHAT[GD-006] NUDGE_blogger_session_resolves_main_owner', () => {
  const state = nudge.createState()
  nudge.bindSessionOwner(state, 'blogger-sub-1', 'main-1')
  const res = nudge.latestNudge(state, 'blogger-sub-1', 'tip-sample')
  assert.match(res, /# Enforcer Tip/)
})

test('WHAT[GD-006] NUDGE_missing_tip_returns_empty', () => {
  const state = nudge.createState()
  assert.equal(nudge.latestNudge(state, 'ses-1', 'missing-tip'), '')
})
