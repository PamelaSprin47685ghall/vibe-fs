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

test('WHAT[sphinx-v2-026] runtime certificate propagation never upgrades model posterior or adaptive samples into external correctness or deterministic bounds', async () => {
  const surface = await import('../../../dist/Sphinx/V2/Core/Surface.js')
  const certificate = await import('../../../dist/Sphinx/V2/Core/Certificate.js')

  // A posterior-credible wire payload decodes to exactly the PosteriorCredible
  // guarantee: the propagation never re-labels it as FrequentistCoverage or
  // DeterministicBound (WHAT 026: Bayes 模型后验不等于"概率=实际正确率").
  const posterior = surface.guaranteeCreate('posterior-credible', surface.listOfItems(['0.9', '0.05', 'model-id']))
  assert.equal(posterior.tag, 3, 'posterior-credible decodes to PosteriorCredible (tag 3)')
  assert.ok(Number.isFinite(posterior.fields[1]) && posterior.fields[1] > 0 && posterior.fields[1] < 1)

  // A deterministic bound carries only a name and assumption list: posterior
  // numbers never leak into its payload.
  const bound = surface.guaranteeCreate('deterministic-bound', surface.listOfItems(['bound-id']))
  assert.equal(bound.tag, 5)
  assert.equal(typeof bound.fields[0], 'string')
  assert.equal(bound.fields[1].head, null, 'bound assumptions are a list, not posterior numbers')
  assert.equal(JSON.stringify(bound.fields).includes('0.9'), false)

  // FrequentistCoverage stays its own guarantee class, not a posterior upgrade.
  const coverage = surface.guaranteeCreate('frequentist-coverage', surface.listOfItems(['0.9', '0.05', 'model']))
  assert.equal(coverage.tag, 4)
  assert.notEqual(coverage.tag, posterior.tag)

  // Validation keeps the classes apart: an invalid posterior mass is rejected
  // as a posterior problem, never silently accepted as a bound.
  const invalidMass = new certificate.CertificateGuarantee(3, ['0.0', 0.0, 'model'])
  assert.equal(certificate.Certificate_validateGuarantee(invalidMass).tag, 1)
  const validPosterior = new certificate.CertificateGuarantee(3, ['0.9', 0.05, 'model'])
  assert.equal(certificate.Certificate_validateGuarantee(validPosterior).tag, 0)
})
