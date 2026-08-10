// tests/unit/session/sync-delegate-ce-collapse.test.mjs — EXEC-026 / EXEC-027 / EXEC-028
//
// SyncDelegateRuntime CE / registry collapse proofs. Complements
// sync-delegate-runtime.test.mjs (single-flight, reuse, dual-await) with dispose,
// cancel-after-return, duplicate return + TextComplete redelivery, idle-budget
// exhaustion, and whitespace normalization.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { DefaultAutoRecoveryBudget } from '../../../dist/Domain/AgentPairCursor.js'
import {
  SessionQuiescenceGate_$ctor as createQuiescenceGate,
  SessionQuiescenceGate__BeginProviderAttempt_Z31B28506 as beginProviderAttempt,
  SessionQuiescenceGate__ObserveIdle_Z31B28506 as observeIdle,
} from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import { ReconciledTurn } from '../../../dist/Application/Reconciliation/ReconciledTurn.js'
import { SyncDelegateRole } from '../../../dist/Kernel/SyncDelegate.js'
import {
  AttachedSessionRuntime_$ctor_Z5DA00426 as createAttached,
} from '../../../dist/Session/AttachedSessionRuntime.js'
import {
  SyncDelegateRuntime,
  SyncDelegateRuntime__Invoke_1B1DD6DD as invoke,
  SyncDelegateRuntime__Return_Z65460A0C as returnAnswer,
  SyncDelegateRuntime__HandleTurn_Z7791586C as handleTurn,
  SyncDelegateRuntime__TextComplete_541DA560 as textComplete,
  SyncDelegateRuntime__CancelSession_Z31B28506 as cancelSession,
  SyncDelegateRuntime__Dispose as disposeRuntime,
} from '../../../dist/Session/SyncDelegateRuntime.js'
import {
  agentJournal,
  authorityRoot,
  idValue,
  okResult,
  physicalUser,
  promptDispatcher,
  providerRun,
  reconcileSupervisor,
  resultOf,
  roles,
  sessionId,
} from '../support/domain.mjs'

const SYNC_RETURN_COMPLETION = 'Sync delegate answer returned to caller.'

const waitFor = async (predicate, message, ms = 2000) => {
  const deadline = Date.now() + ms
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

const turn = (delegateKey, role, text, runId = 'asst_turn') =>
  new ReconciledTurn(
    sessionId(delegateKey),
    physicalUser('msg_phys_turn'),
    authorityRoot('msg_root_turn'),
    providerRun(runId),
    role,
    undefined,
    [reconcileSupervisor.textPart(text)],
    'stop',
    undefined,
    undefined,
    TurnOutcome.TurnCompleted,
    undefined,
  )

const withHarness = async (fn, { tier = 'Fast' } = {}) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-sync-ce-'))
  const opened = agentJournal.create({ directory: base })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))

  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const createCalls = []
  const prompts = []
  let physicalSeq = 0
  const quiescence = createQuiescenceGate()

  const sessions = {
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    SendPrompt: async (session, text, options) => {
      physicalSeq += 1
      prompts.push({
        session: idValue.session(session),
        text,
        agent: options?.Agent,
      })
      return promptDispatcher.admittedWithPhysicalMessage(`msg_phys_${physicalSeq}`)
    },
    CreateChildSession: async (parentId, options) => {
      const child = sessionId(`delegate-${createCalls.length + 1}`)
      createCalls.push({
        parent: idValue.session(parentId),
        agent: options?.Agent,
        title: options?.Title,
        child: idValue.session(child),
      })
      return okResult(child)
    },
  }

  const attached = createAttached()
  const runtime = new SyncDelegateRuntime(
    sessions,
    dispatcher,
    opened.journal,
    attached,
    (_owner) => roles.tier(tier),
    (_delegateSession, _agent) => {},
    quiescence,
    undefined,
  )

  try {
    await fn({
      runtime,
      createCalls,
      prompts,
      quiescence,
    })
  } finally {
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(base, { recursive: true, force: true })
  }
}

const mintIdlePermit = (quiescence, delegateKey) => {
  const sid = sessionId(delegateKey)
  beginProviderAttempt(quiescence, sid)
  return observeIdle(quiescence, sid)
}

test('EXEC_027_dispose_fails_unsettled_sync_delegate_call_scope', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_dispose'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'waiting to be disposed')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')

    disposeRuntime(runtime)
    const done = resultOf(await pending)
    assert.equal(done.ok, false)
    assert.match(done.error, /disposed/i)
  })
})

test('EXEC_027_duplicate_return_is_rejected_and_TextComplete_redelivery_is_idempotent', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_idempotent'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'please answer')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')
    const delegate = createCalls[0].child

    const first = resultOf(await returnAnswer(runtime, delegate, providerRun('asst_tool'), '答案一次'))
    assert.equal(first.ok, true, first.ok ? '' : first.error)

    const duplicate = resultOf(await returnAnswer(runtime, delegate, providerRun('asst_tool_dup'), '答案两次'))
    assert.equal(duplicate.ok, false)
    assert.match(duplicate.error, /already pending/)

    const completionOutput = { text: 'provider trailing prose' }
    textComplete(
      runtime,
      { sessionID: delegate, messageID: 'asst_complete', partID: 'part_complete' },
      completionOutput,
    )
    assert.equal(completionOutput.text, SYNC_RETURN_COMPLETION)

    const again = { text: 'another trailing prose' }
    textComplete(
      runtime,
      { sessionID: delegate, messageID: 'asst_complete_2', partID: 'part_complete_2' },
      again,
    )
    assert.equal(again.text, SYNC_RETURN_COMPLETION)

    const handled = await handleTurn(
      runtime,
      turn(delegate, roles.of('Inspector'), SYNC_RETURN_COMPLETION, 'asst_complete'),
      undefined,
    )
    assert.equal(handled, true)

    const done = resultOf(await pending)
    assert.equal(done.ok, true)
    assert.equal(done.value, '答案一次')
  })
})

test(
  'EXEC_027_payload_mismatch_nudges_until_budget_exhausts_sync_delegate',
  { timeout: 120_000 },
  async () => {
    await withHarness(async ({ runtime, prompts, createCalls, quiescence }) => {
      const owner = 'ses_owner_budget'
      let settled = false
      const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'need budget').finally(() => {
        settled = true
      })

      await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')
      const delegate = createCalls[0].child

      assert.equal(typeof DefaultAutoRecoveryBudget, 'number')
      assert.ok(DefaultAutoRecoveryBudget >= 1)

      for (let i = 0; i < DefaultAutoRecoveryBudget; i += 1) {
        const permit = mintIdlePermit(quiescence, delegate)
        const handled = await handleTurn(
          runtime,
          turn(delegate, roles.of('Inspector'), '  plain prose  ', `asst_plain_${i}`),
          permit,
        )
        assert.equal(handled, true)
        await waitFor(
          () => prompts.length === 2 + i,
          `nudge ${i + 1} was not sent (prompts=${prompts.length})`,
          10_000,
        )
        assert.equal(settled, false, `budget must not settle Invoke before exhaustion (at nudge ${i + 1})`)
      }

      const finalPermit = mintIdlePermit(quiescence, delegate)
      const finalHandled = await handleTurn(
        runtime,
        turn(delegate, roles.of('Inspector'), 'still not the fixed completion', 'asst_final'),
        finalPermit,
      )
      assert.equal(finalHandled, true)

      const done = resultOf(await pending)
      assert.equal(done.ok, false)
      assert.match(done.error, /budget exhausted/i)
      assert.equal(settled, true)
    })
  },
)

test('EXEC_027_cancel_after_Returned_before_Completion_fails_Invoke_at_second_await', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_second_await'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'return then cancel')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')
    const delegate = createCalls[0].child

    const returned = resultOf(await returnAnswer(runtime, delegate, providerRun('asst_return'), '已落盘答案'))
    assert.equal(returned.ok, true, returned.ok ? '' : returned.error)

    let invokeSettled = false
    let invokeResult
    pending.then((value) => {
      invokeSettled = true
      invokeResult = value
    })
    await new Promise((resolve) => setImmediate(resolve))
    await new Promise((resolve) => setImmediate(resolve))
    assert.equal(invokeSettled, false, 'Invoke must stay pending until Completion or cancel')

    cancelSession(runtime, sessionId(owner))
    await waitFor(() => invokeSettled, 'Invoke did not settle after CancelSession')

    const done = resultOf(invokeResult)
    assert.equal(done.ok, false)
    assert.match(done.error, /cancelled/i)
  })
})

test('EXEC_026_whitespace_normalized_fixed_completion_still_resolves_Invoke', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_normalize'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'normalize')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')
    const delegate = createCalls[0].child

    const returned = resultOf(await returnAnswer(runtime, delegate, providerRun('asst_norm_tool'), '规范答案'))
    assert.equal(returned.ok, true, returned.ok ? '' : returned.error)

    const completionOutput = { text: 'provider trailing' }
    textComplete(
      runtime,
      { sessionID: delegate, messageID: 'asst_norm_complete', partID: 'part_norm' },
      completionOutput,
    )
    assert.equal(completionOutput.text, SYNC_RETURN_COMPLETION)

    const handled = await handleTurn(
      runtime,
      turn(delegate, roles.of('Inspector'), `  ${SYNC_RETURN_COMPLETION}  \n`, 'asst_norm_complete'),
      undefined,
    )
    assert.equal(handled, true)

    const done = resultOf(await pending)
    assert.equal(done.ok, true)
    assert.equal(done.value, '规范答案')
  })
})
