import assert from 'node:assert/strict'
import test from 'node:test'
import * as assessment from '../../../dist/Mission/Relay/Assessment/Surface.js'

const perfect = {
  language_algorithms: 'PERFECT',
  simplicity: 'PERFECT',
  structure: 'PERFECT',
  granularity: 'PERFECT',
  tests_evidence: 'PERFECT',
  logic_reliability_boundaries: 'PERFECT',
  caller_ergonomics: 'PERFECT',
  completeness: 'PERFECT',
}

test('WHAT[ASSESS-001] review schema is eight required PERFECT/REVISE/N/A scores with optional note', () => {
  const schema = JSON.parse(assessment.schemaJson)
  assert.equal(schema.type, 'object')
  assert.equal(schema.additionalProperties, false)
  const expectedDimensions = Object.keys(perfect).sort()
  assert.deepEqual(schema.required.slice().sort(), expectedDimensions)
  assert.deepEqual(Object.keys(schema.properties).sort(), [...expectedDimensions, 'note'].sort())
  for (const field of expectedDimensions) {
    assert.deepEqual(schema.properties[field], { type: 'string', enum: ['PERFECT', 'REVISE', 'N/A'] })
  }
  assert.deepEqual(schema.properties.note, { type: 'string' })
})

test('WHAT[ASSESS-001] malformed scores are rejected without coercion', () => {
  for (const payload of [
    { ...perfect, simplicity: 10 },
    { ...perfect, simplicity: 9.5 },
    { ...perfect, simplicity: 'perfect' },
    { ...perfect, simplicity: 'GOOD' },
    { ...perfect, simplicity: null },
    { ...perfect, note: 123 },
    { ...perfect, note: null },
    Object.fromEntries(Object.entries(perfect).filter(([key]) => key !== 'simplicity')),
    { ...perfect, verdict: 'PERFECT' },
  ]) {
    assert.equal(assessment.parse(payload).ok, false)
  }
})

test('WHAT[ASSESS-001] valid payload preserves exact ratings and rejects only on REVISE', () => {
  // Any dimension with REVISE causes overall rejection (allPerfect: false) and populates lowDimensions
  const withRevise = assessment.parse({ ...perfect, structure: 'REVISE', completeness: 'REVISE' })
  assert.deepEqual(withRevise, {
    ok: true,
    scores: { ...perfect, structure: 'REVISE', completeness: 'REVISE' },
    allPerfect: false,
    lowDimensions: ['structure', 'completeness'],
  })

  // Dimensions with N/A and PERFECT pass (allPerfect: true, lowDimensions: [])
  const withNa = assessment.parse({ ...perfect, structure: 'N/A', completeness: 'N/A' })
  assert.deepEqual(withNa, {
    ok: true,
    scores: { ...perfect, structure: 'N/A', completeness: 'N/A' },
    allPerfect: true,
    lowDimensions: [],
  })

  // All N/A also passes because there is no REVISE
  const allNa = Object.fromEntries(Object.keys(perfect).map((k) => [k, 'N/A']))
  const parsedAllNa = assessment.parse(allNa)
  assert.deepEqual(parsedAllNa, {
    ok: true,
    scores: allNa,
    allPerfect: true,
    lowDimensions: [],
  })

  // Note parameter is accepted for writing, but never returned in parsed result
  const withNote = assessment.parse({ ...perfect, note: 'thorough inspection completed; no defects observed.' })
  assert.deepEqual(withNote, {
    ok: true,
    scores: perfect,
    allPerfect: true,
    lowDimensions: [],
  })
  assert.equal('note' in withNote, false)
  assert.equal('note' in withNote.scores, false)
})
