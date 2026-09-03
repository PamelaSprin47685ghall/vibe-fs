import test from 'node:test'
import assert from 'node:assert/strict'

import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

const strict2 = (order) => order.map((label) => [label])

test('WHAT[EPI-024] candidate-label-equivariance-permuted-labels-permute-scores-identically', async () => {
  const ballots = [strict2(['a', 'b', 'c']), strict2(['a', 'b', 'c'])]
  const original = await gecSurface.borda({ candidates: ['a', 'b', 'c'], ballots })
  assert.equal(original.ok, true)
  assert.ok(Math.abs(original.scores.a - 4) < 1e-12)
  assert.ok(Math.abs(original.scores.b - 2) < 1e-12)
  assert.ok(Math.abs(original.scores.c - 0) < 1e-12)
  const permuted = await gecSurface.borda({
    candidates: ['a', 'b', 'c'],
    ballots: [strict2(['c', 'b', 'a']), strict2(['c', 'b', 'a'])],
  })
  assert.equal(permuted.ok, true)
  assert.ok(Math.abs(permuted.scores.c - original.scores.a) < 1e-12)
  assert.ok(Math.abs(permuted.scores.b - original.scores.b) < 1e-12)
  assert.ok(Math.abs(permuted.scores.a - original.scores.c) < 1e-12)
})

test('WHAT[EPI-024] fractional-tie-extension-shares-average-borda-points', async () => {
  const result = await gecSurface.borda({
    candidates: ['a', 'b', 'c'],
    ballots: [[['a', 'b'], ['c']]],
  })
  assert.equal(result.ok, true)
  assert.ok(Math.abs(result.scores.a - 1.5) < 1e-12)
  assert.ok(Math.abs(result.scores.b - 1.5) < 1e-12)
  assert.ok(Math.abs(result.scores.c - 0) < 1e-12)
  assert.ok(String(result.extension).includes('fractional'))
})

test('WHAT[EPI-024] appearance-normalized-extension-divides-by-ballot-appearance-not-raw-sum', async () => {
  const result = await gecSurface.borda({
    candidates: ['a', 'b', 'c'],
    ballots: [
      [[ 'b' ], ['a']],
      [[ 'b' ], ['a']],
      [[ 'b' ], ['a']],
      [[ 'c' ], ['b']],
    ],
  })
  assert.equal(result.ok, true)
  assert.ok(String(result.extension).includes('appearance-normalized'))
  assert.ok(Math.abs(result.meanScores.c - 1.0) < 1e-12)
  assert.ok(Math.abs(result.meanScores.b - 0.75) < 1e-12)
  assert.ok(Math.abs(result.meanScores.a - 0.0) < 1e-12)
  assert.ok(result.meanScores.c > result.meanScores.b)
  assert.ok(result.meanScores.b > result.meanScores.a)
})

test('WHAT[EPI-024] borda-guarantees-claim-only-ballot-order-invariance-and-label-equivariance', async () => {
  const input = {
    candidates: ['a', 'b', 'c'],
    ballots: [strict2(['a', 'b', 'c']), strict2(['c', 'a', 'b'])],
  }
  const first = await gecSurface.borda(input)
  assert.equal(first.ok, true)
  assert.ok(first.guarantees.includes('ballot-order-invariance'))
  assert.ok(first.guarantees.includes('candidate-label-equivariance'))
  assert.ok(!first.guarantees.includes('clone-independence'))
  assert.ok(!first.guarantees.includes('iia'))
  const reversed = await gecSurface.borda({ ...input, ballots: [...input.ballots].reverse() })
  assert.equal(reversed.ok, true)
  assert.ok(Math.abs(reversed.scores.a - first.scores.a) < 1e-12)
  assert.ok(Math.abs(reversed.scores.b - first.scores.b) < 1e-12)
  assert.ok(Math.abs(reversed.scores.c - first.scores.c) < 1e-12)
})

test('WHAT[EPI-024] zero-sum-gauge-fixes-location-with-strengths-summing-to-zero', async () => {
  const result = await gecSurface.bradleyTerry({
    candidates: ['a', 'b', 'c'],
    comparisons: [
      { a: 'a', b: 'b', winsA: 3, winsB: 1 },
      { a: 'b', b: 'c', winsA: 3, winsB: 1 },
      { a: 'a', b: 'c', winsA: 3, winsB: 1 },
    ],
    regularization: 0.1,
  })
  assert.equal(result.ok, true)
  const total = result.strengths.a + result.strengths.b + result.strengths.c
  assert.ok(Math.abs(total) < 1e-12)
  assert.ok(Number.isFinite(result.strengths.a))
  assert.ok(Number.isFinite(result.strengths.b))
  assert.ok(Number.isFinite(result.strengths.c))
  assert.ok(result.strengths.a > result.strengths.b)
  assert.ok(result.strengths.b > result.strengths.c)
})

test('WHAT[EPI-024] disconnected-comparison-graph-returns-typed-unidentifiable-error', async () => {
  const result = await gecSurface.bradleyTerry({
    candidates: ['a', 'b', 'c', 'd'],
    comparisons: [
      { a: 'a', b: 'b', winsA: 2, winsB: 1 },
      { a: 'c', b: 'd', winsA: 2, winsB: 1 },
    ],
    regularization: 0.1,
  })
  assert.equal(result.ok, false)
  assert.ok(typeof result.error === 'string')
  assert.match(result.error, /connect|identif|rank/i)
  assert.ok(!('strengths' in result) || result.strengths == null)
})

test('WHAT[EPI-024] separation-with-regularization-stays-finite-and-reports-diagnostics', async () => {
  const result = await gecSurface.bradleyTerry({
    candidates: ['a', 'b'],
    comparisons: [{ a: 'a', b: 'b', winsA: 10, winsB: 0 }],
    regularization: 0.5,
  })
  assert.equal(result.ok, true)
  assert.ok(Number.isFinite(result.strengths.a))
  assert.ok(Number.isFinite(result.strengths.b))
  assert.ok(Math.abs(result.strengths.a) < 1e6)
  assert.ok(Math.abs(result.strengths.b) < 1e6)
  assert.ok(Math.abs(result.diagnostics.regularization - 0.5) < 1e-12)
  assert.ok(Number.isFinite(result.diagnostics.logLikelihood))
  assert.ok(Number.isFinite(result.uncertainty.standardErrors.a))
  assert.ok(Number.isFinite(result.uncertainty.standardErrors.b))
  assert.ok(result.uncertainty.standardErrors.a >= 0)
  assert.ok(result.uncertainty.standardErrors.b >= 0)
})
