import assert from 'node:assert/strict'
import test from 'node:test'
import * as tdp from '../../../dist/Enforcer/TipDeliveryProjectionSurface.js'
import * as deliverySurface from '../../../dist/Enforcer/Guidance/DeliverySurface.js'

test('WHAT[GD-001] TDP_006_frontier_and_coverage_are_two_axes_not_one_bool', () => {
  const state = tdp.createState()
  assert.equal(tdp.isDelivered(state, 'tip-1'), false)
  assert.equal(tdp.isCovered(state, 'tip-1'), false)
})
