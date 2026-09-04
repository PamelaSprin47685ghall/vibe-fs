import test from 'node:test'
import assert from 'node:assert/strict'

import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

const tinyFactors = Array.from({ length: 40 }, (_, index) => ({
  dependencyKey: `tiny-dep-${String(index + 1).padStart(2, '0')}`,
  likelihoods: { a: 1e-10, b: 2e-10 },
}))

test('WHAT[EPI-010] log-space-bayes-survives-likelihood-product-underflow', async () => {
  const result = await gecSurface.refineCertificate(
    { hypotheses: ['a', 'b'], priors: { a: 0.5, b: 0.5 } },
    { kind: 'bayes-exact', factors: tinyFactors },
  )
  assert.equal(result.ok, true)
  assert.ok(Number.isFinite(result.posterior.a))
  assert.ok(Number.isFinite(result.posterior.b))
  assert.ok(result.posterior.a > 0)
  assert.ok(result.posterior.b > 0)
  assert.ok(Math.abs(result.posterior.a + result.posterior.b - 1) < 1e-12)
  const expectedLogOdds = 40 * Math.log(0.5)
  const actualLogOdds = Math.log(result.posterior.a / result.posterior.b)
  assert.ok(Math.abs(actualLogOdds - expectedLogOdds) < 1e-9)
})

test('WHAT[EPI-010] exact-bayes-matches-brute-force-normalized-product-when-representable', async () => {
  const result = await gecSurface.refineCertificate(
    { hypotheses: ['up', 'down'], priors: { up: 0.3, down: 0.7 } },
    {
      kind: 'bayes-exact',
      factors: [
        { dependencyKey: 'dep-one', likelihoods: { up: 0.8, down: 0.2 } },
        { dependencyKey: 'dep-two', likelihoods: { up: 0.6, down: 0.4 } },
      ],
    },
  )
  assert.equal(result.ok, true)
  assert.ok(Math.abs(result.posterior.up - 0.72) < 1e-12)
  assert.ok(Math.abs(result.posterior.down - 0.28) < 1e-12)
})

test('WHAT[EPI-010] astar-reports-global-frontier-bound-incumbent-and-reopens-better-g', async () => {
  const result = await gecSurface.refineCertificate(
    {},
    {
      kind: 'astar',
      start: 'S',
      goal: 'G',
      edges: [
        { from: 'S', to: 'A', cost: 2 },
        { from: 'S', to: 'B', cost: 2 },
        { from: 'A', to: 'C', cost: 2 },
        { from: 'B', to: 'C', cost: 1 },
        { from: 'C', to: 'G', cost: 2 },
      ],
      heuristic: { S: 4, A: 1, B: 3, C: 0, G: 0 },
    },
  )
  assert.equal(result.ok, true)
  assert.deepEqual(result.path, ['S', 'B', 'C', 'G'])
  assert.ok(Math.abs(result.cost - 5) < 1e-12)
  assert.ok(result.expanded.filter((node) => node === 'C').length >= 2)
  assert.ok(Number.isFinite(result.lowerBound))
  assert.ok(Number.isFinite(result.upperBound))
  assert.ok(result.lowerBound <= result.cost + 1e-12)
  assert.ok(result.cost <= result.upperBound + 1e-12)
  assert.ok(Math.abs(result.lowerBound - 5) < 1e-12)
  assert.ok(Math.abs(result.upperBound - 5) < 1e-12)
})

test('WHAT[EPI-010] astar-rejects-nonzero-goal-heuristic-and-exposes-admissibility-assumption', async () => {
  const rejected = await gecSurface.refineCertificate(
    {},
    {
      kind: 'astar',
      start: 'S',
      goal: 'G',
      edges: [{ from: 'S', to: 'G', cost: 2 }],
      heuristic: { S: 1, G: 5 },
    },
  )
  assert.equal(rejected.ok, false)
  assert.equal(rejected.error.code, 'non-zero-goal-heuristic')

  const admitted = await gecSurface.refineCertificate(
    {},
    {
      kind: 'astar',
      start: 'S',
      goal: 'G',
      edges: [{ from: 'S', to: 'G', cost: 2 }],
      heuristic: { S: 1 },
    },
  )
  assert.equal(admitted.ok, true)
  assert.deepEqual(admitted.path, ['S', 'G'])
  assert.ok(admitted.assumptions.includes('admissible-heuristic-assumed-unverified'))
})

test('WHAT[EPI-010] exact-bayes-reports-canonical-factors-and-ignores-invalid-shadows', async () => {
  const result = await gecSurface.refineCertificate(
    { hypotheses: ['up', 'down'], priors: { up: 0.3, down: 0.7 } },
    {
      kind: 'bayes-exact',
      factors: [
        { dependencyKey: 'dep-one', likelihoods: { up: 0.8, down: 0.2 } },
        { dependencyKey: 'dep-two', likelihoods: { up: 0.6, down: 0.4 } },
      ],
    },
  )
  assert.equal(result.ok, true)
  assert.deepEqual(result.usedFactors, ['dep-one', 'dep-two'])
  assert.ok(Number.isFinite(result.logPartition))
  assert.ok(Math.abs(result.posterior.up - 0.72) < 1e-12)

  const shadowed = await gecSurface.refineCertificate(
    { hypotheses: ['up', 'down'], priors: { up: 0.3, down: 0.7 } },
    {
      kind: 'bayes-exact',
      factors: [
        { dependencyKey: 'dep-one', likelihoods: { up: 0.8, down: 0.2 } },
        { dependencyKey: 'dep-one', likelihoods: { up: 0.9 } },
      ],
    },
  )
  assert.equal(shadowed.ok, true)
  assert.deepEqual(shadowed.usedFactors, ['dep-one'])
  assert.ok(Math.abs(shadowed.posterior.up - (0.8 * 0.3) / (0.8 * 0.3 + 0.2 * 0.7)) < 1e-12)
})

test('WHAT[EPI-010] seeded-mcts-returns-descriptive-sample-summary-not-deterministic-truth', async () => {
  const patch = {
    kind: 'mcts-sample',
    root: 'root',
    children: {
      root: ['weak', 'strong'],
      weak: ['weak-terminal'],
      strong: ['strong-terminal'],
    },
    terminalReward: { 'weak-terminal': 0.1, 'strong-terminal': 0.95 },
    prior: { weak: 0.5, strong: 0.5 },
    iterations: 40,
    seed: 7,
    delta: 0.05,
  }
  const first = await gecSurface.refineCertificate({}, patch)
  const second = await gecSurface.refineCertificate({}, patch)
  assert.equal(first.ok, true)
  assert.equal(second.ok, true)
  assert.deepEqual(second, first)
  assert.ok(Math.abs(first.coverage.delta - 0.05) < 1e-12)
  assert.equal(first.coverage.scope, 'reference-only-no-finite-sample-coverage')
  assert.ok(!('level' in first.coverage))
  for (const key of Object.keys(first.estimates)) {
    assert.ok(Number.isFinite(first.estimates[key]))
    assert.ok(first.estimates[key] >= 0 && first.estimates[key] <= 1)
  }
  assert.match(first.guarantee, /descriptive sample summary/i)
  const guaranteeText = `${first.guarantee} ${Object.keys(first)}`
  assert.ok(!/deterministic truth/i.test(guaranteeText))
  assert.ok(!/singleton/i.test(guaranteeText))
  assert.ok(!/probabilistic-coverage/i.test(guaranteeText))
})

test('WHAT[EPI-010] mcts-sample-accepts-negative-rewards-and-ignores-legacy-prior', async () => {
  const patch = {
    kind: 'mcts-sample',
    root: 'root',
    children: {
      root: ['loss', 'gain'],
      loss: ['loss-terminal'],
      gain: ['gain-terminal'],
    },
    terminalReward: { 'loss-terminal': -2.0, 'gain-terminal': 4.0 },
    iterations: 40,
    seed: 11,
    delta: 0.05,
  }
  const first = await gecSurface.refineCertificate({}, patch)
  assert.equal(first.ok, true)
  assert.equal(first.coverage.rewardLo, -2.0)
  assert.equal(first.coverage.rewardHi, 4.0)
  for (const key of Object.keys(first.estimates)) {
    assert.ok(Number.isFinite(first.estimates[key]))
  }
  assert.match(first.guarantee, /prior.*ignored|ignored.*prior/i)

  const withPrior = await gecSurface.refineCertificate({}, { ...patch, prior: { loss: 0.9, gain: 0.1 } })
  assert.equal(withPrior.ok, true)
  assert.deepEqual(withPrior.estimates, first.estimates)
})
