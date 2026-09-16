import assert from 'node:assert/strict'
import test from 'node:test'
import * as kernel from '../../../dist/Sphinx/KernelSurface.js'

test('WHAT[EPI-007] contract_keeps_distribution_after_semantic_assessment', () => {
  let state = kernel.start('Ambiguous question')
  state = kernel.resume(state, { kind: 'SemanticAssessment', forms: { Fact: 0.6, Decision: 0.4 } })
  assert.deepEqual(state.rootContract.distribution, { Fact: 0.6, Decision: 0.4 })
})
