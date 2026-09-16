import assert from 'node:assert/strict'
import test from 'node:test'
import * as tdp from '../../../dist/Enforcer/TipDeliveryProjectionSurface.js'
import * as tg from '../../../dist/Enforcer/TipGuidanceDeliverySurface.js'

test('WHAT[GD-005] TDP_004_reanchor_voids_full_history_so_next_resolve_refulls', () => {
  const state = tdp.createState()
  tdp.applyFull(state, 'occ-1', 'tip-1')
  assert.equal(tdp.isCovered(state, 'tip-1'), true)
  tdp.applyReanchor(state)
  assert.equal(tdp.isCovered(state, 'tip-1'), false)
  assert.equal(tdp.isDelivered(state, 'tip-1'), true)
})

test('WHAT[GD-005] TDP_005_reanchor_does_not_advance_occurrence_frontier', () => {
  const state = tdp.createState()
  tdp.applyFull(state, 'occ-1', 'tip-1')
  const f1 = tdp.frontier(state)
  tdp.applyReanchor(state)
  const f2 = tdp.frontier(state)
  assert.deepEqual(f1, f2)
})

test('WHAT[GD-005] ENFORCER_TIP_DELIVERY_006_reanchor_triggers_semantic_restoration', () => {
  const state = tg.createDeliveryState()
  tg.resolveTip(state, 'ses-1', 'tip-sample')
  tg.reanchor(state, 'ses-1')
  const restored = tg.resolveTip(state, 'ses-1', 'tip-sample')
  assert.equal(restored.kind, 'Full')
})
