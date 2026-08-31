// requirements/finality/tests/blessing-admission.test.mjs
//
// WHAT[FINALITY-002] & WHAT[FINALITY-016]: Finality Blessing authorization
// requires a matching current tree and a complete ConfirmedReviewWitness.
// Stale witnesses (current tree != witness tree) are strictly rejected with StaleWitness.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as finality from '../../../dist/Mission/Manager/FinalitySurface.js'

const TREE = 'tree_original_hash'
const OTHER_TREE = 'tree_stale_after_rebase'
const LIFE = 'life_1'
const REQ = 'req_1'
const REVIEWER_1 = 'ses_rev_1'
const REVIEWER_2 = 'ses_rev_2'
const BARRIER_1 = 'bar_1'
const BARRIER_2 = 'bar_2'
const PHYSICAL_1 = 'msg_phys_1'
const PHYSICAL_2 = 'msg_phys_2'

const confirmedWitness = (tree = TREE) => ({
  state: 'Confirmed',
  barrier: BARRIER_1,
  tree,
  first: {
    run: 'run_1',
    call: 'call_1',
    tree,
    reviewer: REVIEWER_1,
  },
  second: {
    run: 'run_2',
    call: 'call_2',
    tree,
    reviewer: REVIEWER_1,
  },
  firstPhysical: PHYSICAL_1,
  secondPhysical: PHYSICAL_2,
})

const confirmedWitness2 = (tree = TREE) => ({
  state: 'Confirmed',
  barrier: BARRIER_2,
  tree,
  first: {
    run: 'run_3',
    call: 'call_3',
    tree,
    reviewer: REVIEWER_2,
  },
  second: {
    run: 'run_4',
    call: 'call_4',
    tree,
    reviewer: REVIEWER_2,
  },
  firstPhysical: PHYSICAL_1,
  secondPhysical: PHYSICAL_2,
})

test('WHAT[FINALITY-002] finality_admission_grants_blessing_for_matching_tree_witness', () => {
  const memberWitnesses = [
    { reviewer: REVIEWER_1, barrier: BARRIER_1, witness: confirmedWitness(TREE) },
    { reviewer: REVIEWER_2, barrier: BARRIER_2, witness: confirmedWitness2(TREE) },
  ]
  const projected = finality.projectConfirmedReview(LIFE, REQ, TREE, memberWitnesses)
  assert.equal(projected.ok, true)
  assert.equal(finality.confirmedReviewWitnessTree(projected.witness), TREE)

  const admitted = finality.grantBlessing(TREE, projected.witness)
  assert.equal(admitted.ok, true)
  assert.equal(admitted.permit.tree, TREE)
  assert.equal(admitted.permit.lifeId, LIFE)
  assert.equal(admitted.permit.requestId, REQ)
})

test('WHAT[FINALITY-002] finality_admission_rejects_structurally_forged_confirmations', () => {
  const first = confirmedWitness(TREE)
  const second = confirmedWitness2(TREE)
  const member1 = { reviewer: REVIEWER_1, barrier: BARRIER_1, witness: first }
  const member2 = { reviewer: REVIEWER_2, barrier: BARRIER_2, witness: second }
  const counterworlds = [
    {
      name: 'cohort reviewer differs from the witnessed reviewer',
      members: [{ ...member1, reviewer: 'ses_wrong_valid' }, member2],
    },
    {
      name: 'second verdict names a different reviewer',
      members: [
        { ...member1, witness: { ...first, second: { ...first.second, reviewer: REVIEWER_2 } } },
        member2,
      ],
    },
    {
      name: 'first verdict names a different tree',
      members: [
        { ...member1, witness: { ...first, first: { ...first.first, tree: OTHER_TREE } } },
        member2,
      ],
    },
    {
      name: 'both verdicts use the same provider run',
      members: [
        { ...member1, witness: { ...first, second: { ...first.second, run: first.first.run } } },
        member2,
      ],
    },
    {
      name: 'both verdicts use the same tool call',
      members: [
        { ...member1, witness: { ...first, second: { ...first.second, call: first.first.call } } },
        member2,
      ],
    },
  ]

  for (const counterworld of counterworlds) {
    assert.deepEqual(
      finality.projectConfirmedReview(LIFE, REQ, TREE, counterworld.members),
      { ok: false, error: 'not all cohort reviewers have confirmed dual-PERFECT on the request tree' },
      counterworld.name,
    )
  }
})

test('WHAT[FINALITY-002] finality_admission_rejects_stale_witness_when_tree_differs', () => {
  const memberWitnesses = [
    { reviewer: REVIEWER_1, barrier: BARRIER_1, witness: confirmedWitness(TREE) },
    { reviewer: REVIEWER_2, barrier: BARRIER_2, witness: confirmedWitness2(TREE) },
  ]
  const projected = finality.projectConfirmedReview(LIFE, REQ, TREE, memberWitnesses)
  assert.equal(projected.ok, true)

  const stale = finality.grantBlessing(OTHER_TREE, projected.witness)
  assert.equal(stale.ok, false)
  assert.equal(stale.error, 'StaleWitness')
  assert.equal(stale.currentTree, OTHER_TREE)
  assert.equal(stale.witnessTree, TREE)
})

test('WHAT[FINALITY-016] blessing_admission_requires_complete_cohort_witness', () => {
  const incomplete = [
    { reviewer: REVIEWER_1, barrier: BARRIER_1, witness: confirmedWitness(TREE) },
  ]
  const failedProjection = finality.projectConfirmedReview(LIFE, REQ, TREE, incomplete)
  assert.equal(failedProjection.ok, false)
  assert.match(failedProjection.error, /at least two legitimate reviewers/)

  const unconfirmedMember = [
    { reviewer: REVIEWER_1, barrier: BARRIER_1, witness: confirmedWitness(TREE) },
    { reviewer: REVIEWER_2, barrier: BARRIER_2, witness: { state: 'NoReview' } },
  ]
  const unconfirmedProjection = finality.projectConfirmedReview(LIFE, REQ, TREE, unconfirmedMember)
  assert.equal(unconfirmedProjection.ok, false)
  assert.match(unconfirmedProjection.error, /confirmed dual-PERFECT/)
})
