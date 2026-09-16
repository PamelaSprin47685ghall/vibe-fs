import assert from 'node:assert/strict'
import test from 'node:test'
import * as semantics from '../../../dist/Sphinx/SemanticsSurface.js'

test('WHAT[EPI-003] ungrounded_model_finding_is_retained_as_claim_but_never_promoted_to_evidence', () => {
  const state = semantics.createState()
  const next = semantics.absorbFinding(state, { text: 'Hypothetical finding', source: null })
  assert.equal(next.evidence.length, 0)
  assert.equal(next.findings[0].uncertainty, true)
})
