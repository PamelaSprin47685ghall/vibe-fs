import assert from 'node:assert/strict'
import test from 'node:test'
import * as kernel from '../../../dist/Sphinx/KernelSurface.js'

test('WHAT[EPI-005] semantic_assessment_and_candidates_are_control_observations_not_world_evidence', () => {
  let state = kernel.start('Q')
  state = kernel.resume(state, { kind: 'SemanticAssessment', assessment: {} })
  assert.equal(state.evidence.length, 0)
})

test('WHAT[EPI-005] candidate_question_must_be_investigated_before_it_can_affect_answer', () => {
  let state = kernel.start('Q')
  state = kernel.resume(state, { kind: 'SemanticAssessment', assessment: {} })
  state = kernel.resume(state, { kind: 'Candidates', list: ['sub-q1'] })
  assert.equal(state.findings.length, 0)
})
