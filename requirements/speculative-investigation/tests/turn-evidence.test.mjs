import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const text = (value) => ({ kind: 'text', text: value })
const reasoning = (value) => ({ kind: 'reasoning', text: value })
const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, value) => ({ kind: 'tool-result', callId, result: value })
const activity = (kind) => ({ kind, text: '' })

test('WHAT[SPEC-INV-007] STRENGTH_007_provider_output_evidence_rejects_unknown_part_kinds', () => {
  const result = Strength.turnEvidenceClassify([{ kind: 'unknown-part' }])
  assert.equal(result.ok, false)
  assert.match(result.error, /unknown message part kind/)
})

test('WHAT[SPEC-INV-007] STRENGTH_007_provider_output_evidence_is_not_host_bookkeeping', () => {
  assert.equal(Strength.turnEvidenceClassify([]), 'NoOutput')
  assert.equal(Strength.turnEvidenceClassify([activity('step-start')]), 'TransportOnly')
  assert.equal(Strength.turnEvidenceClassify([result('c1', 'result')]), 'TransportOnly')
  assert.equal(Strength.turnEvidenceClassify([text('answer')]), 'RealOutput')
  assert.equal(Strength.turnEvidenceClassify([reasoning('thought')]), 'RealOutput')
  assert.equal(Strength.turnEvidenceClassify([call('c1', 'read', '{}')]), 'RealOutput')
})
