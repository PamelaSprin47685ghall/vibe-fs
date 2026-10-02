import assert from 'node:assert/strict'
import test from 'node:test'
import * as Bayes from '../../../dist/Sphinx/V2/Plugins/Bayes/Surface.js'

const hypotheses = () => Bayes.listOfItems([{Key: 'h1', Prior: 0.5}, {Key: 'h2', Prior: 0.5}])
const factor = (values = [['h1', 0.8], ['h2', 0.4]]) => ({
  ObservationId: 'o1', DependencyKey: 'dep-1', Likelihoods: Bayes.stringFloatMapOf(values), Qualified: true,
})

test('WHAT[sphinx-v2-026] the declared finite Bayes model normalizes its likelihood product', () => {
  const posterior = Bayes.okPosterior(Bayes.infer(hypotheses(), Bayes.listOfItems([factor()])))
  assert.ok(Math.abs(Bayes.probabilityOf(posterior, 'h1') - 2 / 3) < 1e-12)
  assert.ok(Math.abs(Bayes.probabilityOf(posterior, 'h2') - 1 / 3) < 1e-12)
})

test('WHAT[sphinx-v2-026] repeating one exact observation does not multiply its model contribution', () => {
  const posterior = Bayes.okPosterior(Bayes.infer(hypotheses(), Bayes.listOfItems([factor(), factor()])))
  assert.ok(Math.abs(Bayes.probabilityOf(posterior, 'h1') - 2 / 3) < 1e-12)
})

test('WHAT[sphinx-v2-026] malformed likelihoods cannot produce an exact posterior', () => {
  for (const values of [[['h1', 0.8]], [['h1', 2], ['h2', 0.4]], [['h1', 0], ['h2', 0]]]) {
    assert.equal(Bayes.isError(Bayes.infer(hypotheses(), Bayes.listOfItems([factor(values)]))), true)
  }
})

test('WHAT[sphinx-v2-026] certificate wire decoding keeps posterior guarantees distinct from external correctness classes', async () => {
  const surface = await import('../../../dist/Sphinx/V2/Core/Surface.js')

  // A posterior-credible wire payload decodes to exactly the PosteriorCredible
  // guarantee class: the propagation never re-labels it as FrequentistCoverage
  // or DeterministicBound (WHAT 026: Bayes 模型后验不等于"概率=实际正确率").
  const posterior = surface.guaranteeCreate('posterior-credible', surface.listOfItems(['0.9', '0.05', 'model-id']))
  assert.equal(surface.guaranteeKind(posterior), 'posterior-credible', 'posterior-credible decodes to its own class')

  // A deterministic bound is its own class and validates on its own terms:
  // posterior numbers never leak into it.
  const bound = surface.guaranteeCreate('deterministic-bound', surface.listOfItems(['bound-id']))
  assert.equal(surface.guaranteeKind(bound), 'deterministic-bound')
  assert.ok(surface.isOk(surface.certificateValidateGuarantee(bound)), 'the bound validates without any posterior material')

  // FrequentistCoverage stays its own guarantee class, not a posterior upgrade.
  const coverage = surface.guaranteeCreate('frequentist-coverage', surface.listOfItems(['0.9', '0.05', 'model']))
  assert.equal(surface.guaranteeKind(coverage), 'frequentist-coverage')
  assert.notEqual(surface.guaranteeKind(coverage), surface.guaranteeKind(posterior))

  // Validation keeps the classes apart: an invalid posterior mass is rejected
  // as a posterior problem, never silently accepted as a bound.
  const invalidMass = surface.guaranteeCreate('posterior-credible', surface.listOfItems(['model', '0.0', 'approx']))
  assert.ok(surface.isError(surface.certificateValidateGuarantee(invalidMass)), 'an invalid posterior mass is rejected')
  const validPosterior = surface.guaranteeCreate('posterior-credible', surface.listOfItems(['model', '0.9', 'approx']))
  assert.ok(surface.isOk(surface.certificateValidateGuarantee(validPosterior)), 'a valid posterior mass is accepted')
})


test.todo('WHAT[sphinx-v2-026] runtime certificate propagation never upgrades model posterior or adaptive samples into external correctness or deterministic bounds (GAP-219: runtime wiring pending — the wire-decoding distinctness test above does not prove cross-operator propagation)')
