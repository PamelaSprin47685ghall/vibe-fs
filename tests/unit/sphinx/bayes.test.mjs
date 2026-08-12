import assert from 'node:assert/strict'
import test from 'node:test'
import {
  frozenBayesianInference,
  posteriorEntropy,
  syncBayesianBelief,
  closure,
  createEpistemicState,
} from '../../../src/sphinx/kernel/index.js'

test('frozen_bayesian_inference_updates_posterior', () => {
  const posterior = frozenBayesianInference(
    [
      { semanticKey: 'h1', label: 'rain' },
      { semanticKey: 'h2', label: 'dry' },
    ],
    [{ supports: ['h1'] }, { supports: ['h1'] }],
  )
  const rain = posterior.find((row) => row.semanticKey === 'h1')
  const dry = posterior.find((row) => row.semanticKey === 'h2')
  assert.ok(rain.posterior > dry.posterior)
  assert.ok(Math.abs(rain.posterior + dry.posterior - 1) < 1e-9)
})

test('sync_bayesian_belief_runs_in_closure', () => {
  let state = createEpistemicState('will it rain?')
  state = closure(
    state,
    {
      type: 'SemanticAssessment',
      forms: { Polar: 1 },
      facets: { predictive: 1 },
    },
    { exogenous: true },
  )
  state = closure(
    state,
    {
      type: 'Candidates',
      items: [
        { method: 'Abduction', text: 'rain', semanticKey: 'h1', prior: 0.6, likelihood: 0.8 },
        { method: 'Abduction', text: 'dry', semanticKey: 'h2', prior: 0.4, likelihood: 0.2 },
      ],
    },
    { exogenous: true },
  )
  state = closure(
    state,
    { type: 'Evidence', supports: ['h1'], refutes: ['h2'] },
    { exogenous: true },
  )
  assert.ok(state.B.belief)
  assert.ok(state.B.belief.entropy >= 0)
  assert.ok(state.B.hypotheses.some((row) => row.semanticKey === 'h1' && row.posterior > 0.5))
})

test('posterior_entropy_drops_after_decisive_evidence', () => {
  const before = posteriorEntropy(
    frozenBayesianInference([{ semanticKey: 'a' }, { semanticKey: 'b' }], []),
  )
  const after = posteriorEntropy(
    frozenBayesianInference([{ semanticKey: 'a' }, { semanticKey: 'b' }], [{ supports: ['a'] }]),
  )
  assert.ok(after < before)
})
