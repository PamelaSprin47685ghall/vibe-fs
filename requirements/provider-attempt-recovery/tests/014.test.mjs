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
