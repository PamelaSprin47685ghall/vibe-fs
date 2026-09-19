import assert from 'node:assert/strict'
import test from 'node:test'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const {
  budget,
  providerFailureProjection,
  fold: foldFactsThroughOwner,
  authorityRootAccepted,
  providerFailureRecorded,
  providerRetryExhausted,
  providerSuccessRecorded,
  envelope: ownerEnvelope,
  providerFailureFactCaseNames,
} = failureOwner

const SESSION = 'ses_a'

const RUN = 'run_L'

const ROOT = 'msg_u1'

const ROOT_SELECTION_IDENTITY_SEED = {
  kind: 'RootSelection',
  participantIdentity: {
    selectedAgent: 'engineer',
    canonicalRole: 'engineer',
    persona: 'Engineer',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const identityFor = (run, { logical = RUN, root = ROOT } = {}) =>
  budget.attemptIdentity(SESSION, logical, root, run)

const rootFact = ({ kind = 'HumanRoot', logical = RUN, root = ROOT } = {}) =>
  authorityRootAccepted({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    authorityKind: kind,
    identitySeed: ROOT_SELECTION_IDENTITY_SEED,
  })

const failureFact = ({ run, count, logical = RUN, root = ROOT, reason = 'provider_error' }) =>
  providerFailureRecorded({
    session: SESSION,
    logicalRun: logical,
    authorityRoot: root,
    providerRun: run,
    consecutiveFailureCount: count,
    reason,
  })

const exhaustedFact = ({ count }) =>
  providerRetryExhausted({
    session: SESSION,
    logicalRun: RUN,
    authorityRoot: ROOT,
    finalConsecutiveFailureCount: count,
  })

const foldFacts = (facts) =>
  foldFactsThroughOwner(facts.map((value, index) => ownerEnvelope({ seq: index + 1, session: SESSION, fact: value })))

const budgetOf = (projection) => providerFailureProjection.read(projection)

test('WHAT[provider-attempt-recovery-007] a_valid_record_moves_the_durable_budget', () => {
  const before = providerFailureProjection.forAuthority(RUN, ROOT)
  const applied = providerFailureProjection.applyFailure(identityFor('run_1'), 1, before)

  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  assert.deepEqual(providerFailureProjection.read(applied.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
})

test('WHAT[provider-attempt-recovery-007] the_count_must_advance_by_exactly_one_from_the_folded_state', () => {
  assert.equal(budget.isValidRecord(0, 1), true)
  assert.equal(budget.isValidRecord(4, 5), true)
  assert.equal(budget.isValidRecord(2, 3), true)

  for (const [previousCount, nextCount] of [[2, 1], [5, 1], [4, 4], [4, 6], [4, 3], [4, 0], [4, 2]]) {
    assert.equal(budget.isValidRecord(previousCount, nextCount), false, `${previousCount}→${nextCount} refused`)
  }
})

test('WHAT[provider-attempt-recovery-007] each_rejection_names_a_different_cause', () => {
  const base = providerFailureProjection.applyFailure(
    identityFor('run_1'),
    1,
    providerFailureProjection.forAuthority(RUN, ROOT),
  )
  assert.equal(base.ok, true)
  const current = base.value

  assert.deepEqual(
    {
      duplicate: providerFailureProjection.applyFailure(identityFor('run_1'), 2, current).error,
      badCount: providerFailureProjection.applyFailure(identityFor('run_3'), 4, current).error,
      otherRun: providerFailureProjection.applyFailure(identityFor('run_4', { logical: 'run_other' }), 2, current).error,
      afterExhausted: providerFailureProjection.applyFailure(
        identityFor('run_5'),
        2,
        providerFailureProjection.applyExhausted(current),
      ).error,
    },
    {
      duplicate: 'AlreadyObserved',
      badCount: 'InvalidTransition',
      otherRun: 'DifferentRun',
      afterExhausted: 'AlreadyExhausted',
    },
  )
})

test('WHAT[provider-attempt-recovery-007] a_replayed_journal_reaches_the_same_budget', () => {
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    failureFact({ run: 'run_2', count: 2 }),
    failureFact({ run: 'run_3', count: 3 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.deepEqual({ failures: budgetOf(folded.value).failures }, { failures: 3 })
})

test('WHAT[provider-attempt-recovery-007] a_replayed_journal_with_intervening_success_restart_reaches_the_same_budget', () => {
  const succeeded = providerSuccessRecorded({
    session: SESSION,
    logicalRun: RUN,
    authorityRoot: ROOT,
    providerRun: 'run_success_mid',
  })
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    failureFact({ run: 'run_2', count: 2 }),
    succeeded,
    failureFact({ run: 'run_3', count: 1 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.deepEqual({ failures: budgetOf(folded.value).failures }, { failures: 1 })
})

test('WHAT[provider-attempt-recovery-007] an_advance_naming_another_run_is_absorbed_not_applied', () => {
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1, logical: 'run_M', root: 'msg_u2' }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = budgetOf(folded.value)
  assert.deepEqual({ run: state.logicalRun, failures: state.failures }, { run: 'run_L', failures: 0 })
})
