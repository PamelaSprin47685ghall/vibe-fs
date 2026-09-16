// provider-failure-ledger.test.mjs — PAR-001/003/005/014 at the Application
// single-writer boundary: ProviderFailureLedger is the only writer of
// FailureRecorded / RetryExhausted. It dedupes one failed attempt, refuses to
// record outside a Logical Run, maps budget exhaustion to the host-facing
// "stop automatic recovery" admission, and never writes for a run that does
// not exist. Every assertion drives the production journal + ledger through
// Fallback/ProviderFailureSurface.js.

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
const { budget, acceptHumanRoot: failureAcceptHumanRoot } = failureOwner

const SESSION = 'ses_ledger'

test('WHAT[PAR-001] no_active_run_records_nothing_and_writes_no_fact', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-norun-'))
  const created = await bootWithWriterId(directory, 'writer-ledger-norun', 'rt_ledger_norun', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    // No AcceptHumanRoot: the session has no failure budget (PAR-001: the
    // budget belongs to a Logical Run; there is no run yet).
    const outcome = await failureOwner.recordConfirmedFailure(
      journal,
      budget.defaultBudget,
      SESSION,
      'msg_asst_ghost',
      'provider_error',
    )
    assert.deepEqual(outcome, { ok: true, outcome: 'NoActiveRun' })

    const state = failureOwner.snapshot(journal, SESSION)
    assert.equal(state, null, 'no budget may exist outside a Logical Run')
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[PAR-003] same_failure_observed_twice_advances_once', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-dedupe-'))
  const created = await bootWithWriterId(directory, 'writer-ledger-dedupe', 'rt_ledger_dedupe', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_dup')

    const first = await failureOwner.recordConfirmedFailure(
      journal,
      budget.defaultBudget,
      SESSION,
      'msg_asst_1',
      'provider_error',
    )
    assert.deepEqual(first, { ok: true, outcome: 'RetryAuthorized' })

    // A second observe replays the same recovery authorization. The exact
    // prompt gate dedupes its physical send; the failure count stays one.
    const second = await failureOwner.recordConfirmedFailure(
      journal,
      budget.defaultBudget,
      SESSION,
      'msg_asst_1',
      'provider_error',
    )
    assert.deepEqual(second, { ok: true, outcome: 'RetryAuthorized' })

    const state = failureOwner.snapshot(journal, SESSION)
    assert.deepEqual(
      { failures: state.failures, exhausted: state.exhausted },
      { failures: 1, exhausted: false },
    )
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[PAR-003] an_older_failed_run_is_absorbed_after_its_successor_advances', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-superseded-'))
  const created = await bootWithWriterId(directory, 'writer-ledger-superseded', 'rt_ledger_superseded', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_superseded')

    assert.deepEqual(
      await failureOwner.recordConfirmedFailure(journal, budget.defaultBudget, SESSION, 'run-1', 'provider_error'),
      { ok: true, outcome: 'RetryAuthorized' },
    )
    assert.deepEqual(
      await failureOwner.recordConfirmedFailure(journal, budget.defaultBudget, SESSION, 'run-2', 'provider_error'),
      { ok: true, outcome: 'RetryAuthorized' },
    )
    assert.deepEqual(
      await failureOwner.recordConfirmedFailure(journal, budget.defaultBudget, SESSION, 'run-1', 'provider_error'),
      { ok: true, outcome: 'EpisodeSuperseded' },
    )

    const state = failureOwner.snapshot(journal, SESSION)
    assert.deepEqual(
      { failures: state.failures, exhausted: state.exhausted },
      { failures: 2, exhausted: false },
    )
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

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

test('WHAT[PAR-014] a_continuation_has_a_unique_accounted_and_budgeted_occasion', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-continuation-'))
  const created = await bootWithWriterId(directory, 'writer-ledger-continuation', 'rt_ledger_continuation', 1, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_cont_seq')

    // One confirmed failure recorded within budget → RetryAuthorized. That is
    // the continuation's only occasion.
    const first = await failureOwner.recordConfirmedFailure(
      journal,
      budget.defaultBudget,
      SESSION,
      'msg_asst_seq_1',
      'provider_error',
    )
    assert.deepEqual(first, { ok: true, outcome: 'RetryAuthorized' })

    // The same failure observed again replays the same authorization; the
    // durable prompt gate refuses a second physical send and the budget still
    // advances only once.
    const second = await failureOwner.recordConfirmedFailure(
      journal,
      budget.defaultBudget,
      SESSION,
      'msg_asst_seq_1',
      'provider_error',
    )
    assert.deepEqual(second, { ok: true, outcome: 'RetryAuthorized' })

    const state = failureOwner.snapshot(journal, SESSION)
    // The continuation itself advances nothing: one record, exactly one unit.
    assert.deepEqual(
      { failures: state.failures, exhausted: state.exhausted },
      { failures: 1, exhausted: false },
    )
  } finally {
    dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

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

  if (!recorded.ok) return recorded
  return {
    ok: true,
    value: recorded.outcome === 'RetryExhausted' ? 'RetryExhausted' : 'RetryAuthorized',
  }
}
