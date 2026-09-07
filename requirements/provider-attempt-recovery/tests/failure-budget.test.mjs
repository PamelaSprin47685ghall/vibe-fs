// failure-budget.test.mjs — PAR-001/002/003/004/005/007/009/020.
//
// One retry world: ProviderFailureBudget counts consecutive failures,
// ProviderFailureProjection holds the durable per-run budget view,
// ProviderFailureLedger (tested in provider-failure-ledger.test.mjs) is the
// only writer. Everything here drives the production
// Fallback/ProviderFailureSurface.js — no copied budget model.

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
    selectedAgent: 'coder',
    canonicalRole: 'coder',
    persona: 'Coder',
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

/** Fold a sequence of facts, numbering LocalSeq from 1. */
const foldFacts = (facts) =>
  foldFactsThroughOwner(facts.map((value, index) => ownerEnvelope({ seq: index + 1, session: SESSION, fact: value })))

const budgetOf = (projection) => providerFailureProjection.read(projection)

// ── PAR-002: the budget starts empty ─────────────────────────────────────────

test('WHAT[PAR-002] a_fresh_budget_starts_at_zero_with_no_budget_spent', () => {
  assert.deepEqual(budget.read(budget.initial), { failures: 0 })

  assert.deepEqual(
    providerFailureProjection.read(providerFailureProjection.forAuthority(RUN, ROOT)),
    {
      logicalRun: 'run_L',
      authorityRoot: 'msg_u1',
      failures: 0,
      dedupeKeys: 0,
      exhausted: false,
    },
  )
})

test('WHAT[PAR-002] fixed_participant_holds_across_failures', () => {
  // The budget carries no participant at all: identity is fixed elsewhere and
  // the retry-policy suite proves it immutable. The budget answers only the
  // count, so there is nothing here that could switch a participant.
  assert.deepEqual(budget.read(budget.recordFailure(budget.initial)), { failures: 1 })
  assert.deepEqual(Object.keys(budget.read(budget.initial)).sort(), ['failures'])
})

test('WHAT[PAR-020] budget_replay_exposes_domain_evidence_without_resume_authority', () => {
  const facts = [rootFact(), failureFact({ run: 'provider-1', count: 1 })]
  const first = budgetOf(foldFacts(facts).value)
  const replay = budgetOf(foldFacts(facts).value)

  assert.deepEqual(replay, first)
  assert.deepEqual(first, {
    logicalRun: RUN,
    authorityRoot: ROOT,
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
  assert.deepEqual(Object.keys(first).sort(), [
    'authorityRoot',
    'dedupeKeys',
    'exhausted',
    'failures',
    'logicalRun',
  ])
})

// ── PAR-004: failure adds one, success resets ────────────────────────────────

test('WHAT[PAR-004] failure_adds_one_to_the_consecutive_count', () => {
  assert.deepEqual(budget.read(budget.recordFailure({ failures: 0 })), { failures: 1 })
  assert.deepEqual(budget.read(budget.recordFailure({ failures: 7 })), { failures: 8 })
})

test('WHAT[PAR-004] success_resets_the_budget', () => {
  const afterFailure = budget.recordFailure(budget.initial)
  const afterSuccess = budget.recordSuccess(afterFailure)

  assert.deepEqual(budget.read(afterSuccess), { failures: 0 })
  assert.deepEqual(budget.read(budget.recordFailure(afterSuccess)), { failures: 1 })
})

test('WHAT[PAR-004] success_is_a_durable_fact_that_zeroes_the_count', () => {
  assert.deepEqual(
    providerFailureFactCaseNames.slice().sort(),
    ['FailureRecorded', 'RetryExhausted', 'SuccessRecorded'],
    'the budget has three durable facts: record, terminal, and success',
  )

  const succeeded = providerSuccessRecorded({
    session: SESSION,
    logicalRun: RUN,
    authorityRoot: ROOT,
    providerRun: 'run_success_1',
  })
  const foldedOnce = foldFacts([rootFact(), failureFact({ run: 'run_1', count: 1 }), succeeded])
  assert.equal(foldedOnce.ok, true, foldedOnce.ok ? '' : JSON.stringify(foldedOnce.error))
  assert.deepEqual(
    { failures: budgetOf(foldedOnce.value).failures },
    { failures: 0 },
    'success zeroes the count',
  )

  const foldedDup = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    succeeded,
    succeeded,
  ])
  assert.equal(foldedDup.ok, true, foldedDup.ok ? '' : JSON.stringify(foldedDup.error))
  assert.deepEqual({ failures: budgetOf(foldedDup.value).failures }, { failures: 0 })

  const staleSuccess = providerSuccessRecorded({
    session: SESSION,
    logicalRun: 'run_other',
    authorityRoot: 'msg_other',
    providerRun: 'run_success_stale',
  })
  const foldedStale = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    staleSuccess,
  ])
  assert.equal(foldedStale.ok, true, foldedStale.ok ? '' : JSON.stringify(foldedStale.error))
  assert.deepEqual(
    { failures: budgetOf(foldedStale.value).failures },
    { failures: 1 },
    'success naming another run is absorbed, not applied',
  )
})

// ── PAR-005: 0/1/11/12 boundary ──────────────────────────────────────────────

test('WHAT[PAR-005] the_default_automatic_retry_budget_is_twelve', () => {
  assert.equal(budget.defaultBudget, 12)
})

test('WHAT[PAR-005] verdict_boundaries_zero_one_eleven_twelve', () => {
  const at = (count) => ({ failures: count })
  // 0: fresh run may retry.
  assert.equal(budget.verdict(12, at(0)), 'MayRetry')
  // 1: first failure still retries.
  assert.equal(budget.verdict(12, at(1)), 'MayRetry')
  // 11: last retry still allowed.
  assert.equal(budget.verdict(12, at(11)), 'MayRetry')
  // 12: immediately final — no 13th physical request.
  assert.equal(budget.verdict(12, at(12)), 'Exhausted')

  let value = budget.initial
  const verdicts = []
  for (let attempt = 1; attempt <= 12; attempt += 1) {
    value = budget.recordFailure(value)
    verdicts.push(budget.verdict(12, value))
  }
  assert.deepEqual(budget.read(value), { failures: 12 })
  assert.deepEqual(verdicts.slice(0, 11), Array(11).fill('MayRetry'))
  assert.equal(verdicts[11], 'Exhausted')
})

test('WHAT[PAR-005] a_configured_budget_is_honoured_and_never_infinite', () => {
  const at = (count) => ({ failures: count })
  assert.deepEqual(
    [1, 2, 3].map((limit) => budget.verdict(limit, at(2))),
    ['Exhausted', 'Exhausted', 'MayRetry'],
  )
  assert.equal(budget.verdict(0, budget.initial), 'Exhausted')
})

test('WHAT[PAR-005] exhaustion_is_stored_rather_than_re_derived_from_the_count', () => {
  const advanced = providerFailureProjection.applyFailure(
    identityFor('run_1'),
    1,
    providerFailureProjection.forAuthority(RUN, ROOT),
  )
  assert.equal(advanced.ok, true)
  const exhausted = providerFailureProjection.applyExhausted(advanced.value)

  assert.equal(providerFailureProjection.read(exhausted).exhausted, true)
  assert.equal(providerFailureProjection.read(exhausted).failures, 1)
  assert.equal(providerFailureProjection.mayRetry(12, exhausted), false)
  assert.equal(providerFailureProjection.mayRetry(9999, exhausted), false)
})

test('WHAT[PAR-005] may_retry_answers_the_projection_level_question', () => {
  let current = providerFailureProjection.forAuthority(RUN, ROOT)
  assert.equal(providerFailureProjection.mayRetry(3, current), true)

  for (let attempt = 1; attempt <= 3; attempt += 1) {
    current = providerFailureProjection.applyFailure(identityFor(`run_${attempt}`), attempt, current).value
  }

  assert.equal(providerFailureProjection.read(current).failures, 3)
  assert.equal(providerFailureProjection.mayRetry(3, current), false)
  assert.equal(providerFailureProjection.mayRetry(4, current), true)
})

test('WHAT[PAR-005] an_advance_after_exhaustion_is_absorbed_not_applied', () => {
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    exhaustedFact({ count: 1 }),
    failureFact({ run: 'run_2', count: 2 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = budgetOf(folded.value)
  assert.deepEqual(
    { failures: state.failures, exhausted: state.exhausted },
    { failures: 1, exhausted: true },
  )
})

// ── PAR-007: fold validates every record ─────────────────────────────────────

test('WHAT[PAR-007] a_valid_record_moves_the_durable_budget', () => {
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

test('WHAT[PAR-007] the_count_must_advance_by_exactly_one_from_the_folded_state', () => {
  assert.equal(budget.isValidRecord(0, 1), true)
  assert.equal(budget.isValidRecord(4, 5), true)
  assert.equal(budget.isValidRecord(2, 3), true)

  for (const [previousCount, nextCount] of [[2, 1], [5, 1], [4, 4], [4, 6], [4, 3], [4, 0], [4, 2]]) {
    assert.equal(budget.isValidRecord(previousCount, nextCount), false, `${previousCount}→${nextCount} refused`)
  }
})

test('WHAT[PAR-007] each_rejection_names_a_different_cause', () => {
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

test('WHAT[PAR-007] a_replayed_journal_reaches_the_same_budget', () => {
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    failureFact({ run: 'run_2', count: 2 }),
    failureFact({ run: 'run_3', count: 3 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.deepEqual({ failures: budgetOf(folded.value).failures }, { failures: 3 })
})

test('WHAT[PAR-007] a_replayed_journal_with_intervening_success_restart_reaches_the_same_budget', () => {
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

test('WHAT[PAR-007] an_advance_naming_another_run_is_absorbed_not_applied', () => {
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1, logical: 'run_M', root: 'msg_u2' }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  const state = budgetOf(folded.value)
  assert.deepEqual({ run: state.logicalRun, failures: state.failures }, { run: 'run_L', failures: 0 })
})

// ── PAR-003: exact duplicate advances once ───────────────────────────────────

test('WHAT[PAR-003] the_same_attempt_observed_twice_advances_once', () => {
  let current = providerFailureProjection.forAuthority(RUN, ROOT)
  const first = providerFailureProjection.applyFailure(identityFor('run_1'), 1, current)
  assert.equal(first.ok, true)
  current = first.value

  assert.deepEqual(providerFailureProjection.applyFailure(identityFor('run_1'), 2, current), {
    ok: false,
    error: 'AlreadyObserved',
  })

  const second = providerFailureProjection.applyFailure(identityFor('run_2'), 2, current)
  assert.equal(second.ok, true)
  assert.equal(providerFailureProjection.read(second.value).failures, 2)
})

test('WHAT[PAR-003] the_dedupe_window_is_bounded', () => {
  let current = providerFailureProjection.forAuthority(RUN, ROOT)

  for (let attempt = 1; attempt <= 60; attempt += 1) {
    const applied = providerFailureProjection.applyFailure(identityFor(`run_${attempt}`), attempt, current)
    assert.equal(applied.ok, true, applied.ok ? '' : `advance ${attempt}: ${applied.error}`)
    current = applied.value
  }

  const state = providerFailureProjection.read(current)
  assert.equal(state.failures, 60)
  assert.equal(state.dedupeKeys, 32)
})

test('WHAT[PAR-004] recording_success_clears_the_dedupe_window_too', () => {
  const advanced = providerFailureProjection.applyFailure(
    identityFor('run_1'),
    1,
    providerFailureProjection.forAuthority(RUN, ROOT),
  )
  assert.equal(advanced.ok, true)

  const afterSuccess = providerFailureProjection.recordSuccess(advanced.value)
  assert.deepEqual(providerFailureProjection.read(afterSuccess), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 0,
    dedupeKeys: 0,
    exhausted: false,
  })

  const again = providerFailureProjection.applyFailure(identityFor('run_1'), 1, afterSuccess)
  assert.equal(again.ok, true)
})

test('WHAT[PAR-003] a_duplicate_line_is_absorbed_because_replay_produces_it', () => {
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    failureFact({ run: 'run_1', count: 2 }),
  ])

  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.deepEqual({ failures: budgetOf(folded.value).failures }, { failures: 1 })
})

// ── PAR-009: Host attempt number is not the domain count ─────────────────────

test('WHAT[PAR-009] the_domain_count_is_reachable_only_through_a_confirmed_failure', () => {
  assert.equal(budget.recordFailure.length, 1)
  assert.equal(budget.recordSuccess.length, 1)

  let current = providerFailureProjection.forAuthority(RUN, ROOT)
  const first = providerFailureProjection.applyFailure(identityFor('run_same'), 1, current)
  assert.equal(first.ok, true)
  assert.deepEqual(providerFailureProjection.applyFailure(identityFor('run_same'), 2, first.value), {
    ok: false,
    error: 'AlreadyObserved',
  })
})

test('WHAT[PAR-009] the_dedupe_identity_names_the_run_the_root_and_the_attempt', () => {
  const identity = identityFor('run_1')

  assert.deepEqual(identity, { session: 'ses_a', run: 'run_L', root: 'msg_u1', attempt: 'run_1' })
  assert.equal(budget.dedupeKey(identity), ['ses_a', 'run_L', 'msg_u1', 'run_1'].join(''))

  const base = budget.dedupeKey(identity)
  assert.notEqual(budget.dedupeKey(identityFor('run_2')), base)
  assert.notEqual(budget.dedupeKey(identityFor('run_1', { logical: 'run_other' })), base)
  assert.notEqual(budget.dedupeKey(identityFor('run_1', { root: 'msg_u9' })), base)
})

// ── PAR-001: budget lifetime ─────────────────────────────────────────────────

test('WHAT[PAR-001] an_accepted_authority_root_creates_the_budget', () => {
  const folded = foldFacts([rootFact()])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  assert.deepEqual(budgetOf(folded.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 0,
    dedupeKeys: 0,
    exhausted: false,
  })
})

test('WHAT[PAR-001] a_record_with_no_accepted_root_stops_the_replay', () => {
  const folded = foldFacts([failureFact({ run: 'run_1', count: 1 })])

  assert.equal(folded.ok, false)
  assert.equal(folded.error.Fact, 'FailureRecorded')
  assert.equal(
    folded.error.Reason,
    'provider failure has no active budget: requires an accepted Authority Root',
  )
})

test('WHAT[PAR-001] an_active_authority_root_must_close_before_replacement', () => {
  const folded = foldFacts([
    rootFact(),
    failureFact({ run: 'run_1', count: 1 }),
    failureFact({ run: 'run_2', count: 2 }),
    rootFact({ logical: 'run_M', root: 'msg_u2' }),
  ])

  assert.deepEqual(folded, {
    ok: false,
    error: {
      Fact: 'AuthorityRootAccepted',
      Reason: 'active logical run must close before replacement: active=run_L requested=run_M',
    },
  })
})
