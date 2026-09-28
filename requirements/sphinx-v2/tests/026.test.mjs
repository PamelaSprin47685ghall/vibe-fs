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

test.todo('WHAT[sphinx-v2-026] runtime certificate propagation never upgrades model posterior or adaptive samples into external correctness or deterministic bounds')
