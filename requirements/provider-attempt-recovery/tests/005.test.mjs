import assert from 'node:assert/strict'
import test from 'node:test'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  JournalSurface_bootWithWriterId as bootWithWriterId,
  JournalSurface_dispose as dispose,
} from '../../../dist/Persistence/Journal/Surface.js'

// failure-budget.test.mjs — PAR-001/002/003/004/005/007/009/020.
//
// One retry world: ProviderFailureBudget counts consecutive failures,
// ProviderFailureProjection holds the durable per-run budget view,
// ProviderFailureLedger (tested in provider-failure-ledger.test.mjs) is the
// only writer. Everything here drives the production
// Fallback/ProviderFailureSurface.js — no copied budget model.

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
  acceptHumanRoot: failureAcceptHumanRoot,
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

async function acceptHumanRoot(journal, userMessageId) {
  const accepted = await failureAcceptHumanRoot(journal, SESSION, userMessageId, 'coder')
  assert.equal(accepted.ok, true, `AcceptHumanRoot failed: ${accepted.error}`)
}

async function admit(journal, providerRunName) {
  const recorded = await failureOwner.recordConfirmedFailure(
    journal,
    budget.defaultBudget,
    SESSION,
    providerRunName,
    'provider_error',
  )
  assert.equal(recorded.ok, true, `recordConfirmedFailure failed: ${recorded.error}`)
  return failureOwner.admit(journal, budget.defaultBudget, SESSION, providerRunName)
}

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

test('WHAT[PAR-005] twelfth_failure_admission_is_retry_exhausted', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-admission-'))
  const created = await bootWithWriterId(directory, 'writer-ledger-admission', 'rt_ledger_admission', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_adm')

    // Drive the budget to the 11th consecutive failure (default 12).
    for (let i = 1; i <= 11; i += 1) {
      const advanced = await failureOwner.recordConfirmedFailure(
        journal,
        budget.defaultBudget,
        SESSION,
        `run-${i}`,
        'provider_error',
      )
      assert.deepEqual(advanced, { ok: true, outcome: 'RetryAuthorized' }, `attempt ${i} must authorize retry`)
    }

    // The 12th failure is immediately final: the admission that decides whether
    // the controller may issue another automatic request must say stop.
    const admission = await admit(journal, 'run-12')
    assert.equal(admission.ok, true, admission.ok ? '' : admission.error)
    assert.equal(admission.value, 'RetryExhausted')

    const state = failureOwner.snapshot(journal, SESSION)
    assert.equal(state.exhausted, true, 'RetryExhausted must be durable')

    // Post-exhaustion observes replay the terminal decision: no second
    // RetryExhausted fact and no budget mutation.
    const thirteenth = await failureOwner.recordConfirmedFailure(
      journal,
      budget.defaultBudget,
      SESSION,
      'run-13',
      'provider_error',
    )
    assert.deepEqual(thirteenth, { ok: true, outcome: 'RetryExhausted' })
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[PAR-005] admission_continues_while_budget_remains', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-continue-'))
  const created = await bootWithWriterId(directory, 'writer-ledger-continue', 'rt_ledger_continue', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_cont')

    const admission = await admit(journal, 'msg_asst_cont_1')
    assert.equal(admission.ok, true, admission.ok ? '' : admission.error)
    assert.equal(admission.value, 'RetryAuthorized')
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
