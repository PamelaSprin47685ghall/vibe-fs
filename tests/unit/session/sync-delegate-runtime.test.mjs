// tests/unit/session/sync-delegate-runtime.test.mjs — EXEC-026 / EXEC-028
//
// SyncDelegateRuntime without full Host: real runtime + fake ISessionHostPort /
// AttachedSessionRuntime / PromptDispatcher journal, mirroring satellite-runtime
// and prompt fire-and-forget unit patterns.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'
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

const completionTurn = (delegateKey, role) =>
  new ReconciledTurn(
    sessionId(delegateKey),
    physicalUser('msg_phys_turn'),
    authorityRoot('msg_root_turn'),
    providerRun('asst_turn'),
    role,
    undefined,
    [reconcileSupervisor.textPart(SYNC_RETURN_COMPLETION)],
    'stop',
    undefined,
    undefined,
    TurnOutcome.TurnCompleted,
    undefined,
  )

const withHarness = async (fn, { tier = 'Fast' } = {}) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-sync-delegate-'))
  const opened = agentJournal.create({ directory: base })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))

  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const createCalls = []
  const prompts = []
  let physicalSeq = 0

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
  const ready = []
  const runtime = new SyncDelegateRuntime(
    sessions,
    dispatcher,
    opened.journal,
    attached,
    (_owner) => roles.tier(tier),
    (delegateSession, agent) => {
      ready.push({ session: idValue.session(delegateSession), agent })
    },
    createQuiescenceGate(),
    undefined,
  )

  try {
    await fn({
      runtime,
      createCalls,
      prompts,
      ready,
      sessions,
    })
  } finally {
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(base, { recursive: true, force: true })
  }
}

const settlePendingInvoke = async (runtime, delegateKey, role, answer, runId = 'asst_return') => {
  const returned = resultOf(await returnAnswer(runtime, delegateKey, providerRun(runId), answer))
  assert.equal(returned.ok, true, returned.ok ? '' : returned.error)

  const handled = await handleTurn(runtime, completionTurn(delegateKey, role), undefined)
  assert.equal(handled, true)
}

test('EXEC_026_sync_delegate_second_invoke_while_in_flight_is_rejected', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_flight'
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect first')

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'first Invoke did not send')

    const second = resultOf(await invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect second'))
    assert.equal(second.ok, false)
    assert.match(second.error, /in flight/i)

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'first answer')
    const firstDone = resultOf(await first)
    assert.equal(firstDone.ok, true)
    assert.equal(firstDone.value, 'first answer')
  })
})

test('EXEC_026_sync_delegate_fast_tier_nails_inspector_and_coder_agent_names', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_tier'
    const inspectorP = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect please')
    await waitFor(() => createCalls.length === 1 && prompts.length === 1, 'inspector child not created')
    assert.equal(createCalls[0].agent, 'fast-inspector')
    assert.equal(createCalls[0].title, 'fast-inspector')
    assert.equal(prompts[0].agent, 'fast-inspector')

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'inspector done', 'asst_insp')
    assert.equal((resultOf(await inspectorP)).ok, true)

    const coderP = invoke(runtime, owner, SyncDelegateRole.Coder, 'code please')
    await waitFor(() => createCalls.length === 2 && prompts.length === 2, 'coder child not created')
    assert.equal(createCalls[1].agent, 'fast-coder')
    assert.equal(createCalls[1].title, 'fast-coder')
    assert.equal(prompts[1].agent, 'fast-coder')

    await settlePendingInvoke(runtime, createCalls[1].child, roles.of('Coder'), 'coder done', 'asst_coder')
    assert.equal((resultOf(await coderP)).ok, true)
  })
})

test('EXEC_026_sync_delegate_reuses_session_after_full_completion', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_reuse'
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'pass one')
    await waitFor(() => createCalls.length === 1 && prompts.length === 1, 'first create/send missing')
    const delegateId = createCalls[0].child

    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'answer one', 'asst_one')
    const firstDone = resultOf(await first)
    assert.equal(firstDone.ok, true)
    assert.equal(firstDone.value, 'answer one')

    const second = invoke(runtime, owner, SyncDelegateRole.Inspector, 'pass two')
    await waitFor(() => prompts.length === 2, 'second send missing')
    assert.equal(createCalls.length, 1, 'GetOrCreate must reuse; createChild once')

    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'answer two', 'asst_two')
    const secondDone = resultOf(await second)
    assert.equal(secondDone.ok, true)
    assert.equal(secondDone.value, 'answer two')
    assert.equal(createCalls[0].child, delegateId)
  })
})

test('EXEC_028_sync_delegate_return_settles_before_completion_keeps_invoke_pending', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_dual'
    const answer = 'durable answer for caller'
    const invokeP = invoke(runtime, owner, SyncDelegateRole.Inspector, 'please answer')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not reach await Returned')

    let invokeSettled = false
    let invokeResult
    invokeP.then((value) => {
      invokeSettled = true
      invokeResult = value
    })

    const returned = resultOf(
      await returnAnswer(runtime, createCalls[0].child, providerRun('asst_dual'), answer),
    )
    assert.equal(returned.ok, true, returned.ok ? '' : returned.error)

    // Return settles the answer side; Completion is still open.
    await new Promise((resolve) => setImmediate(resolve))
    await new Promise((resolve) => setImmediate(resolve))
    assert.equal(invokeSettled, false, 'Invoke must stay pending until HandleTurn Completion')

    const handled = await handleTurn(
      runtime,
      completionTurn(createCalls[0].child, roles.of('Inspector')),
      undefined,
    )
    assert.equal(handled, true)

    await waitFor(() => invokeSettled, 'Invoke did not complete after TurnCompleted')
    const done = resultOf(invokeResult)
    assert.equal(done.ok, true)
    assert.equal(done.value, answer)
  })
})
