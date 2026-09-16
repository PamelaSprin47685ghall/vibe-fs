import assert from 'node:assert/strict'
import test from 'node:test'
import * as tdp from '../../../dist/Enforcer/TipDeliveryProjectionSurface.js'
import * as tg from '../../../dist/Enforcer/TipGuidanceDeliverySurface.js'

test('WHAT[GD-003] ENFORCER_TIP_DELIVERY_002_second_resolve_same_tip_is_identity_only', () => {
  const state = tg.createDeliveryState()
  tg.resolveTip(state, 'ses-1', 'tip-sample')
  const second = tg.resolveTip(state, 'ses-1', 'tip-sample')
  assert.equal(second.kind, 'IdentityOnly')
  assert.equal(second.text, 'tip = "tip-sample"')
})

test('WHAT[GD-003] TDP_002_second_apply_same_occurrence_is_identity_only', () => {
  const state = tdp.createState()
  tdp.applyFull(state, 'occ-1', 'tip-1')
  const res = tdp.applyIdentity(state, 'occ-1', 'tip-1')
  assert.equal(res.kind, 'IdentityOnly')
})

test('WHAT[GD-003] TDP_003_identity_only_does_not_advance_frontier', () => {
  const state = tdp.createState()
  tdp.applyFull(state, 'occ-1', 'tip-1')
  const f1 = tdp.frontier(state)
  tdp.applyIdentity(state, 'occ-1', 'tip-1')
  const f2 = tdp.frontier(state)
  assert.deepEqual(f1, f2)
})
