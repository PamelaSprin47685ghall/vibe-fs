// requirements/provider-attempt-recovery/tests/fallback-ledger.test.mjs
//
// FALLBACK-003/001/005 at the Application single-writer boundary: FallbackLedger
// is the only writer of FallbackCursorAdvanced / FallbackExhausted. It dedupes
// one failed attempt, refuses to advance outside a Logical Run, maps budget
// exhaustion to the host-facing "stop automatic recovery" admission, and never
// writes for a run that does not exist.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  agentJournal,
  cursor,
  fallbackController,
  fallbackProjection,
  fold,
  physicalUser,
  promptDispatcher,
  providerRun,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'

const { FallbackLedger_admitConfirmedFailure } = await import(
  '../../../dist/Participant/Provider/Attempt/Fallback/Ledger.js'
)

const SESSION = 'ses_ledger'

test('WHAT[PAR-001] PAR_FALLBACK_001_no_active_run_advances_nothing_and_writes_no_fact', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-norun-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_ledger_norun' })
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    // No AcceptHumanRoot: the session has no Fallback cursor (FALLBACK-001:
    // fallback belongs to a Logical Run; there is no run yet).
    const outcome = await fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'msg_asst_ghost',
      'provider_error',
    )
    assert.deepEqual(outcome, { ok: true, outcome: 'NoActiveRun' })

    const state = fold.session(promptDispatcher.journalSnapshot(journal), SESSION)
    assert.equal(state, undefined, 'no cursor may exist outside a Logical Run')
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[PAR-003] PAR_FALLBACK_003_same_failure_observed_twice_advances_once', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-dedupe-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_ledger_dedupe' })
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_dup')

    const first = await fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'msg_asst_1',
      'provider_error',
    )
    assert.deepEqual(first, { ok: true, outcome: 'Advanced' })

    // A second observe of the same provider run (idle + retry race) must not
    // advance twice: the same failure is only counted once (FALLBACK-003).
    const second = await fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'msg_asst_1',
      'provider_error',
    )
    assert.deepEqual(second, { ok: true, outcome: 'AlreadyRecorded' })

    const state = fallbackProjection.read(
      fold.session(promptDispatcher.journalSnapshot(journal), SESSION).Fallback,
    )
    assert.deepEqual(
      { offset: state.offset, failures: state.failures, exhausted: state.exhausted },
      { offset: 1, failures: 1, exhausted: false },
    )
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[PAR-005] PAR_FALLBACK_005_twelfth_failure_admission_is_recovery_exhausted', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-admission-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_ledger_admission' })
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_adm')

    // Drive the budget to the 11th consecutive failure (FALLBACK-005 default 12).
    for (let i = 1; i <= 11; i += 1) {
      const advanced = await fallbackController.recordConfirmedFailure(
        journal,
        cursor.defaultBudget,
        SESSION,
        `run-${i}`,
        'provider_error',
      )
      assert.deepEqual(advanced, { ok: true, outcome: 'Advanced' }, `attempt ${i} must advance`)
    }

    // The 12th failure is immediately final: the admission that decides whether
    // the controller may issue another automatic request must say stop.
    const admission = await admit(journal, 'run-12')
    assert.equal(admission.ok, true, admission.ok ? '' : admission.error)
    assert.equal(admission.value, 'RecoveryExhausted')

    const state = fallbackProjection.read(
      fold.session(promptDispatcher.journalSnapshot(journal), SESSION).Fallback,
    )
    assert.equal(state.exhausted, true, 'FallbackExhausted must be durable')

    // Post-exhaustion observes are absorbed: no second FallbackExhausted, no
    // cursor mutation (FALLBACK-007 fold rejection AlreadyExhausted).
    const thirteenth = await fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'run-13',
      'provider_error',
    )
    assert.deepEqual(thirteenth, { ok: true, outcome: 'AlreadyRecorded' })
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[PAR-005] PAR_FALLBACK_005_admission_continues_while_budget_remains', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-continue-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_ledger_continue' })
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_cont')

    const admission = await admit(journal, 'msg_asst_cont_1')
    assert.equal(admission.ok, true, admission.ok ? '' : admission.error)
    assert.equal(admission.value, 'ContinueRecovery')
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[PAR-014] PAR_014_a_continuation_has_a_unique_accounted_and_budgeted_occasion', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-ledger-continuation-'))
  const created = await agentJournal.create({ directory, runtime: 'rt_ledger_continuation' })
  assert.equal(created.ok, true, created.ok ? '' : created.error)

  try {
    const journal = created.journal
    await acceptHumanRoot(journal, 'msg_u_cont_seq')

    // 一次已确认失败记账完成且预算允许 → Advanced。这是 continuation 的唯一时机
    // (FALLBACK-004/009:仅当 Host 已停止自动重试才发 continuation,本包只保证时序)。
    const first = await fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'msg_asst_seq_1',
      'provider_error',
    )
    assert.deepEqual(first, { ok: true, outcome: 'Advanced' })

    // 同一失败第二次 observe → AlreadyRecorded:不产生第二个 continuation,
    // 也不触发第二次 cursor 推进(FALLBACK-003 去重,第一个 observe 保持 owner)。
    const second = await fallbackController.recordConfirmedFailure(
      journal,
      cursor.defaultBudget,
      SESSION,
      'msg_asst_seq_1',
      'provider_error',
    )
    assert.deepEqual(second, { ok: true, outcome: 'AlreadyRecorded' })

    const state = fallbackProjection.read(
      fold.session(promptDispatcher.journalSnapshot(journal), SESSION).Fallback,
    )
    // continuation 本身不得触发第二次推进:一次记账恰好一次 Advance,offset 1 / failures 1。
    assert.deepEqual(
      { offset: state.offset, failures: state.failures, exhausted: state.exhausted },
      { offset: 1, failures: 1, exhausted: false },
    )
  } finally {
    created.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
})

async function acceptHumanRoot(journal, userMessageId) {
  const { Runtime__AcceptHumanRoot } = await import('../../../dist/Interaction/Dispatch/Dispatcher.js')
  const runtime = promptDispatcher.forJournal(journal)
  const accepted = await Runtime__AcceptHumanRoot(runtime, sessionId(SESSION), physicalUser(userMessageId), 'fast-coder')
  assert.equal(accepted.tag ?? 0, 0, `AcceptHumanRoot failed: ${accepted.fields?.[0]}`)
}

/// Run FallbackLedger.admitConfirmedFailure and project the Fable Result +
/// RecoveryAdmission union into a plain JS shape.
async function admit(journal, providerRunName) {
  const result = await FallbackLedger_admitConfirmedFailure(
    journal,
    cursor.defaultBudget,
    sessionId(SESSION),
    providerRun(providerRunName),
    'provider_error',
  )
  if (result.tag !== 0) return { ok: false, error: result.fields[0] }
  const admission = result.fields[0]
  return { ok: true, value: admission.tag === 1 ? 'RecoveryExhausted' : 'ContinueRecovery' }
}
