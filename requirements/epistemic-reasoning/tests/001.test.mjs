import assert from 'node:assert/strict'
import test from 'node:test'
import * as kernel from '../../../dist/Sphinx/KernelSurface.js'

test('WHAT[EPI-001] start_yields_semantic_assessment_request', () => {
  const state = kernel.start('What is the capital of France?')
  assert.equal(state.status, 'Active')
  assert.equal(state.pendingRequest.kind, 'SemanticAssessmentRequest')
})

test('WHAT[EPI-001] wire_characterization_start_initializes_state', () => {
  const state = kernel.start('Test question')
  assert.ok(state.inquiryId)
  assert.equal(state.revision, 0)
})
