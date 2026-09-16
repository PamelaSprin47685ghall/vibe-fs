import assert from 'node:assert/strict'
import test from 'node:test'
import * as semantics from '../../../dist/Sphinx/SemanticsSurface.js'

test('WHAT[EPI-008] gateway_gain_can_make_low_immediate_gain_question_worth_asking', () => {
  const val = semantics.evaluateAction({ immediateGain: 0.1, gatewayGain: 0.9 })
  assert.ok(val.totalScore > 0.5)
})
