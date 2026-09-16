import assert from 'node:assert/strict'
import test from 'node:test'
import * as kernel from '../../../dist/Sphinx/KernelSurface.js'

test('WHAT[EPI-004] resume_rejects_observation_that_does_not_match_pending_kernel_request', () => {
  const state = kernel.start('Test question')
  assert.throws(
    () => kernel.resume(state, { kind: 'CandidatesObservation', candidates: [] }),
    /observation type mismatch/,
  )
})

test('WHAT[EPI-004] decoder_parity_checks_exact_observation_shape', () => {
  assert.equal(kernel.isValidObservationShape({ kind: 'SemanticAssessment' }), true)
  assert.equal(kernel.isValidObservationShape({ kind: 'UnknownShape' }), false)
})
