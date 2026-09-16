import assert from 'node:assert/strict'
import test from 'node:test'
import * as bayes from '../../../dist/Sphinx/BayesSurface.js'

test('WHAT[EPI-006] same_semantic_evidence_from_independent_dependency_groups_is_preserved_twice', () => {
  const e1 = { semanticKey: 'fact-1', dependencyKey: 'source-a' }
  const e2 = { semanticKey: 'fact-1', dependencyKey: 'source-b' }
  const state = bayes.absorbEvidence(bayes.createState(), [e1, e2])
  assert.equal(state.evidence.length, 2)
})

test('WHAT[EPI-006] same_dependency_group_is_not_counted_as_independent_evidence_twice', () => {
  const e1 = { semanticKey: 'fact-1', dependencyKey: 'source-a' }
  const e2 = { semanticKey: 'fact-1', dependencyKey: 'source-a' }
  const state = bayes.absorbEvidence(bayes.createState(), [e1, e2])
  assert.equal(state.evidence.length, 1)
})
