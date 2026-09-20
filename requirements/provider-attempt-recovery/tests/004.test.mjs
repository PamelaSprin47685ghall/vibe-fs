import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  JournalSurface_bootWithWriterId as bootWithWriterId,
  JournalSurface_dispose as dispose,
} from '../../../dist/Persistence/Journal/Surface.js'
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

test('WHAT[provider-attempt-recovery-004] failure_adds_one_to_the_consecutive_count', () => {
  assert.deepEqual(budget.read(budget.recordFailure({ failures: 0 })), { failures: 1 })
  assert.deepEqual(budget.read(budget.recordFailure({ failures: 7 })), { failures: 8 })
})

test('WHAT[provider-attempt-recovery-004] success_resets_the_budget', () => {
  const afterFailure = budget.recordFailure(budget.initial)
  const afterSuccess = budget.recordSuccess(afterFailure)

  assert.deepEqual(budget.read(afterSuccess), { failures: 0 })
  assert.deepEqual(budget.read(budget.recordFailure(afterSuccess)), { failures: 1 })
})

test('WHAT[provider-attempt-recovery-004] success_is_a_durable_fact_that_zeroes_the_count', () => {
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

test('WHAT[provider-attempt-recovery-004] recording_success_clears_the_dedupe_window_too', () => {
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

test('WHAT[provider-attempt-recovery-004] an_accepted_execution_without_a_start_fact_still_names_its_ordinary_kind', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-par004-kind-'))
  const created = await bootWithWriterId(directory, 'writer-par004-kind', 'rt_par004_kind', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    const session = 'ses_par004_kind'
    const physical = 'msg_par004_kind'

    // No durable execution at all: the evidence refuses to name a kind.
    assert.equal(failureOwner.requestKindFor(journal, session, physical), '')

    // provider-attempt-recovery-023 shape: Accepted without ProviderStarted still
    // names its ordinary kind through the accepted evidence origin, so a
    // confirmed failure of such an attempt stays retryable.
    const accepted = await failureOwner.establishAcceptedExecution(journal, session, physical)
    assert.equal(accepted.ok, true)
    assert.equal(failureOwner.requestKindFor(journal, session, physical), 'work-main')

    // Once the exact start fact exists, the same request keeps its frozen kind.
    const started = await failureOwner.establishProviderRun(journal, session, physical, 'run_par004_kind')
    assert.equal(started.ok, true, started.ok ? '' : JSON.stringify(started))
    assert.equal(failureOwner.requestKindFor(journal, session, physical), 'work-main')
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
