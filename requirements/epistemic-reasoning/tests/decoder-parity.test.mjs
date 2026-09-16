import { test } from 'node:test'
import assert from 'node:assert/strict'
import {
  decode,
  decodeSemanticAssessmentObservation,
  decodeCandidatesObservation,
  decodeInvestigationObservation,
  decodeSynthesisObservation,
} from '../../../dist/Sphinx/Surface.js'

test('WHAT[EPI-004] decode and decodeSemanticAssessmentObservation produce same result for SemanticAssessment raw', () => {
  const raw = { type: 'SemanticAssessment', forms: { Polar: 0.9, Other: 0.1 } }
  const generic = decode(raw)
  const specific = decodeSemanticAssessmentObservation(raw)
  assert.equal(generic.ok, true)
  assert.equal(specific.ok, true)
  assert.equal(generic.observationType, 'SemanticAssessment')
  assert.equal(specific.observationType, 'SemanticAssessment')
})

test('WHAT[EPI-004] decode and decodeCandidatesObservation produce same result for Candidates raw', () => {
  const raw = {
    type: 'Candidates',
    items: [{ method: 'why', question: 'why X?', semanticKey: 'k1' }],
  }
  const generic = decode(raw)
  const specific = decodeCandidatesObservation(raw)
  assert.equal(generic.ok, true)
  assert.equal(specific.ok, true)
  assert.equal(generic.observationType, 'Candidates')
  assert.equal(specific.observationType, 'Candidates')
})

test('WHAT[EPI-004] decode and decodeInvestigationObservation produce same result for Investigation raw', () => {
  const raw = { type: 'Investigation', actionKey: 'action-1' }
  const generic = decode(raw)
  const specific = decodeInvestigationObservation(raw)
  assert.equal(generic.ok, true)
  assert.equal(specific.ok, true)
  assert.equal(generic.observationType, 'Investigation')
  assert.equal(specific.observationType, 'Investigation')
})

test('WHAT[EPI-004] decode and decodeSynthesisObservation produce same result for Synthesis raw', () => {
  const raw = { type: 'Synthesis', text: 'summary' }
  const generic = decode(raw)
  const specific = decodeSynthesisObservation(raw)
  assert.equal(generic.ok, true)
  assert.equal(specific.ok, true)
  assert.equal(generic.observationType, 'Synthesis')
  assert.equal(specific.observationType, 'Synthesis')
})

test('WHAT[EPI-004] decode rejects unknown observation type', () => {
  const raw = { type: 'Unknown' }
  const result = decode(raw)
  assert.equal(result.ok, false)
  assert.ok(result.error)
})
