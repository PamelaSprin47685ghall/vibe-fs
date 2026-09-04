import test from 'node:test'
import assert from 'node:assert/strict'

import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

const subjects8 = ['s01', 's02', 's03', 's04', 's05', 's06', 's07', 's08']
const treatmentsAB = ['wording-a', 'wording-b']
const candidates3 = ['c1', 'c2', 'c3']

const assignmentInput = (seed) => ({
  rootSnapshot: 'snap-split-a',
  seed,
  subjects: [...subjects8],
  treatments: [...treatmentsAB],
  candidates: [...candidates3],
})

test('WHAT[EPI-023] deterministic-seed-reproduces-identical-balanced-assignment-matrix', async () => {
  const first = await gecSurface.splitBallot(assignmentInput(1234))
  const second = await gecSurface.splitBallot(assignmentInput(1234))
  assert.equal(first.ok, true)
  assert.equal(second.ok, true)
  assert.deepEqual(second, first)
  assert.equal(first.assignments.length, 8)
  const counts = { 'wording-a': 0, 'wording-b': 0 }
  const tokens = new Set()
  for (const item of first.assignments) {
    assert.ok(treatmentsAB.includes(item.treatment))
    assert.equal(typeof item.blindToken, 'string')
    assert.ok(item.blindToken.length > 0)
    tokens.add(item.blindToken)
    counts[item.treatment] += 1
    assert.deepEqual([...item.labelPermutation].sort(), [...candidates3].sort())
    assert.deepEqual([...item.orderPermutation].sort(), [...candidates3].sort())
  }
  assert.equal(counts['wording-a'], 4)
  assert.equal(counts['wording-b'], 4)
  assert.equal(tokens.size, 8)
})

test('WHAT[EPI-023] blind-branch-view-exposes-no-sibling-answer-ranking-or-aggregate', async () => {
  const result = await gecSurface.splitBallot(assignmentInput(77))
  assert.equal(result.ok, true)
  for (const item of result.assignments) {
    assert.ok(!('siblingAnswer' in item))
    assert.ok(!('siblingTreatment' in item))
    assert.ok(!('ranking' in item))
    assert.ok(!('aggregateTendency' in item))
    assert.ok(!('aggregate' in item))
    assert.equal(item.subject.slice(0, 1), 's')
    assert.equal(typeof item.blindToken, 'string')
  }
  const seen = result.assignments.map((item) => `${item.subject}:${item.blindToken}`)
  assert.equal(new Set(seen).size, result.assignments.length)
})

test('WHAT[EPI-023] wording-effect-reports-signed-difference-in-means-not-absolute-distance', async () => {
  const base = {
    rootSnapshot: 'snap-split-b',
    seed: 9,
    subjects: ['s1', 's2', 's3', 's4', 's5', 's6'],
    treatments: [...treatmentsAB],
    candidates: ['c1', 'c2'],
  }
  const assigned = await gecSurface.splitBallot(base)
  assert.equal(assigned.ok, true)
  const groupA = assigned.assignments.filter((item) => item.treatment === 'wording-a').map((item) => item.subject)
  const groupB = assigned.assignments.filter((item) => item.treatment === 'wording-b').map((item) => item.subject)
  assert.equal(groupA.length, 3)
  assert.equal(groupB.length, 3)
  const outcomes = {}
  for (const subject of groupA) outcomes[subject] = 1
  outcomes[groupB[0]] = 0
  outcomes[groupB[1]] = 0
  outcomes[groupB[2]] = 1
  const estimated = await gecSurface.splitBallot({
    ...base,
    outcomes,
    estimand: 'difference-in-means',
    contrast: ['wording-b', 'wording-a'],
  })
  assert.equal(estimated.ok, true)
  assert.equal(estimated.effect.estimand, 'difference-in-means')
  assert.ok(Math.abs(estimated.effect.estimate - -2 / 3) < 1e-12)
  assert.ok(estimated.effect.estimate < 0)
})

test('WHAT[EPI-023] ate-interpretation-declares-causal-assumptions-and-permutation-uncertainty', async () => {
  const base = {
    rootSnapshot: 'snap-split-c',
    seed: 21,
    subjects: ['t1', 't2', 't3', 't4'],
    treatments: [...treatmentsAB],
    candidates: ['c1', 'c2'],
  }
  const assigned = await gecSurface.splitBallot(base)
  assert.equal(assigned.ok, true)
  const outcomes = {}
  for (const item of assigned.assignments) outcomes[item.subject] = item.treatment === 'wording-a' ? 1 : 0
  const estimated = await gecSurface.splitBallot({
    ...base,
    outcomes,
    estimand: 'difference-in-means',
    contrast: ['wording-b', 'wording-a'],
  })
  assert.equal(estimated.ok, true)
  const assumptions = estimated.effect.assumptions
  assert.ok(Array.isArray(assumptions))
  for (const required of [
    'sutva-no-interference',
    'positivity',
    'same-prefix',
    'no-differential-attrition',
    'estimand-specified',
  ]) {
    assert.ok(assumptions.includes(required), `missing assumption ${required}`)
  }
  assert.ok(estimated.effect.uncertainty)
  assert.ok(estimated.effect.permutationNull)
  const pValue = estimated.effect.permutationNull.pValue
  assert.ok(Number.isFinite(pValue))
  assert.ok(pValue >= 0 && pValue <= 1)
})

test('WHAT[EPI-023] treatment-details-configure-wording-polarity-and-order', async () => {
  const result = await gecSurface.splitBallot({
    rootSnapshot: 'snap-split-d',
    seed: 31,
    subjects: ['s1', 's2', 's3', 's4'],
    treatments: ['wording-a', 'wording-b'],
    treatmentDetails: {
      'wording-b': { wording: 'reversed text', polarity: -1, openFirst: false },
    },
    candidates: ['c1', 'c2'],
  })
  assert.equal(result.ok, true)
  const reversed = result.assignments.filter((item) => item.treatment === 'wording-b')
  assert.equal(reversed.length, 2)
  for (const item of reversed) {
    assert.equal(item.wording, 'reversed text')
    assert.equal(item.polarity, -1)
    assert.equal(item.openFirst, false)
  }
  const plain = result.assignments.filter((item) => item.treatment === 'wording-a')
  assert.equal(plain.length, 2)
  for (const item of plain) {
    assert.equal(item.wording, 'wording-a')
    assert.equal(item.polarity, 1)
    assert.equal(item.openFirst, true)
  }
})

test('WHAT[EPI-023] invalid-treatment-polarity-fails-closed', async () => {
  const result = await gecSurface.splitBallot({
    rootSnapshot: 'snap-split-e',
    seed: 33,
    subjects: ['s1', 's2'],
    treatments: ['wording-a', 'wording-b'],
    treatmentDetails: { 'wording-b': { polarity: 0 } },
    candidates: ['c1', 'c2'],
  })
  assert.equal(result.ok, false)
  assert.match(result.error.code, /invalid-polarity/i)
})

test('WHAT[EPI-023] carryover-permutation-null-is-seeded-deterministic-and-capped', async () => {
  const input = (seed, permutations) => ({
    responses: subjects8.map((subject, index) => ({ subject, response: index < 4 ? 4.0 : 1.0 })),
    priorExposure: Object.fromEntries(subjects8.map((subject, index) => [subject, index < 4 ? 'arm-a' : 'arm-b'])),
    currentTreatment: Object.fromEntries(subjects8.map((subject) => [subject, 'arm-c'])),
    focalCurrent: 'arm-c',
    control: 'arm-b',
    treatment: 'arm-a',
    permutations,
    seed,
  })
  const first = await gecSurface.carryover(input(7, 64))
  const second = await gecSurface.carryover(input(7, 64))
  assert.equal(first.ok, true)
  assert.deepEqual(second, first)
  assert.equal(first.estimand, 'carryover-difference-in-means')
  assert.equal(first.uncertainty.kind, 'permutation-null')
  assert.equal(first.uncertainty.nullPermutations, 64)
  assert.ok(first.uncertainty.pValue >= 0 && first.uncertainty.pValue <= 1)

  const other = await gecSurface.carryover(input(99, 64))
  const otherAgain = await gecSurface.carryover(input(99, 64))
  assert.equal(other.ok, true)
  assert.deepEqual(otherAgain, other)
  // The seed reaches the permutation null: distinct seeds draw distinct
  // nulls (a seed-ignoring null would report one shared p-value).
  assert.ok(Math.abs(first.uncertainty.pValue - 0.06153846153846154) < 1e-12)
  assert.ok(Math.abs(other.uncertainty.pValue - 0.015384615384615385) < 1e-12)
  assert.notEqual(other.uncertainty.pValue, first.uncertainty.pValue)

  const capped = await gecSurface.carryover(input(7, 4096))
  assert.equal(capped.ok, true)
  assert.equal(capped.uncertainty.nullPermutations, 1024)
})

test('WHAT[EPI-023] missing-root-snapshot-fails-closed-before-randomization', async () => {
  const result = await gecSurface.splitBallot({
    seed: 5,
    subjects: ['s1', 's2'],
    treatments: [...treatmentsAB],
    candidates: ['c1', 'c2'],
  })
  assert.equal(result.ok, false)
  assert.ok(typeof result.error === 'string')
  assert.match(result.error, /snapshot/i)
})
