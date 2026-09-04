import assert from 'node:assert/strict'
import test from 'node:test'
import * as assessment from '../../../dist/Mission/Relay/Assessment/Surface.js'

const perfect = {
  language_algorithms: 10,
  simplicity: 10,
  structure: 10,
  granularity: 10,
  tests_evidence: 10,
  logic_reliability_boundaries: 10,
  caller_ergonomics: 10,
  completeness: 10,
}

test('WHAT[ASSESS-001] review schema is exactly eight required integer scores with no extras', () => {
  const schema = JSON.parse(assessment.schemaJson)
  assert.equal(schema.type, 'object')
  assert.equal(schema.additionalProperties, false)
  assert.deepEqual(Object.keys(schema.properties).sort(), Object.keys(perfect).sort())
  assert.deepEqual(schema.required.slice().sort(), Object.keys(perfect).sort())
  for (const field of Object.keys(perfect)) {
    assert.deepEqual(schema.properties[field], { type: 'integer', minimum: 0, maximum: 10 })
  }
})

test('WHAT[ASSESS-001] malformed scores are rejected without coercion', () => {
  for (const payload of [
    { ...perfect, simplicity: 9.5 },
    { ...perfect, simplicity: '10' },
    { ...perfect, simplicity: null },
    { ...perfect, simplicity: -1 },
    { ...perfect, simplicity: 11 },
    Object.fromEntries(Object.entries(perfect).filter(([key]) => key !== 'simplicity')),
    { ...perfect, verdict: 'PERFECT' },
  ]) {
    assert.equal(assessment.parse(payload).ok, false)
  }
})

test('WHAT[ASSESS-001] valid payload preserves all eight exact integers', () => {
  const parsed = assessment.parse({ ...perfect, structure: 7, completeness: 0 })
  assert.deepEqual(parsed, {
    ok: true,
    scores: { ...perfect, structure: 7, completeness: 0 },
    allPerfect: false,
    lowDimensions: ['structure', 'completeness'],
  })
})

