import assert from 'node:assert/strict'
import test from 'node:test'
import * as bayes from '../../../dist/Sphinx/BayesSurface.js'

test('WHAT[EPI-009] bayesian_posterior_requires_explicit_numeric_qualification', () => {
  assert.throws(() => bayes.calculatePosterior({ invalidLikelihood: -0.5 }), /likelihood out of range/)
})

test('WHAT[EPI-009] qualified_independent_evidence_updates_posterior', () => {
  const prior = { H1: 0.5, H2: 0.5 }
  const likelihood = { H1: 0.8, H2: 0.2 }
  const post = bayes.updatePosterior(prior, likelihood)
  assert.ok(post.H1 > 0.7)
})

test('WHAT[EPI-009] unqualified_item_cannot_mask_qualified_evidence_from_same_dependency_group', () => {
  const factors = [{ dep: 'd1', valid: false }, { dep: 'd1', valid: true, likelihood: { H1: 0.9, H2: 0.1 } }]
  const post = bayes.updateWithFactors({ H1: 0.5, H2: 0.5 }, factors)
  assert.ok(post.H1 > 0.8)
})
