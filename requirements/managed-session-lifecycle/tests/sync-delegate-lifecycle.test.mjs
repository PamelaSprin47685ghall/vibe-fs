// Split from tests/unit/session/sync-delegate-runtime.test.mjs (cutover Wave 2a); owner: managed-session-lifecycle.
// Split from tests/unit/session/sync-delegate-ce-collapse.test.mjs (cutover Wave 2a); owner: managed-session-lifecycle.
//
// SyncDelegate 生命周期面：completion 后复用不 spawn（MANAGED-SESSION-004）、
// deleted Inspector child retire + owner scope-close 存活（MANAGED-SESSION-014）、
// owner CancelSession 使 pending invoke fail（级联取消）、dispose/cancel scope
// fail（EXEC-027）。batch/tier/WorkRecord 断言已随 SPLIT@cutover 迁
// requirements/delegation/tests/sync-delegate-runtime.test.mjs 与
// requirements/delegation/tests/sync-delegate-ce-collapse.test.mjs。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/OpenCode/Host/SessionQuiescenceGate.js'
import { clearAllForTests as SessionPersona_clearAllForTests } from '../../../dist/Participant/Persona/SessionPersona.js'
import { TurnOutcome } from '../../../dist/Composition/Turn/Program.js'
import { ReconciledTurn } from '../../../dist/Composition/Turn/Observation.js'
import { SyncDelegateRole } from '../../../dist/Execution/Delegation/SyncDelegate/Model.js'
const attachedModule = await import('../../../dist/Execution/Session/Attachment/AttachedRuntime.js')
const createAttached = Object.entries(attachedModule).find(([k]) => k.startsWith('AttachedSessionRuntime_$ctor'))?.[1]
const syncDelegateModule = await import('../../../dist/Execution/Delegation/SyncDelegate/Runtime.js')
const { SyncDelegateRuntime, SyncDelegateRuntime__Dispose: disposeRuntime } = syncDelegateModule
// Resolve Fable-exported members by prefix; the hash suffix is a compiler
// artifact and must not be pinned in tests (VERIFY-008).
const syncDelegateMemberOf = (name) => Object.entries(syncDelegateModule).find(([k]) => k.startsWith(`SyncDelegateRuntime__${name}_`))?.[1]
const invoke = syncDelegateMemberOf('Invoke')
const handleTurn = syncDelegateMemberOf('HandleTurn')
const cancelSession = syncDelegateMemberOf('CancelSession')
const stageDeletedInspector = syncDelegateMemberOf('StageDeletedInspector')
const tryFind = syncDelegateMemberOf('TryFind')
const tryFindForScopeClose = syncDelegateMemberOf('TryFindForScopeClose')
import {
  agentJournal,
  authorityRoot,
  idValue,
  lifecycleWorkRecordProjection,
  okResult,
  physicalUser,
  promptDispatcher,
  providerRun,
  reconcileSupervisor,
  resultOf,
  roles,
  sessionId,
  xTraceCapture,
} from '../../verification-system/tests/support/domain.mjs'

const completionTurn = (delegateKey, role, answer, runId = 'asst_turn') =>
  new ReconciledTurn(
    sessionId(delegateKey),
    physicalUser('msg_phys_turn'),
    authorityRoot('msg_root_turn'),
    providerRun(runId),
    role,
    undefined,
    [reconcileSupervisor.textPart(answer)],
    'stop',
    undefined,
    undefined,
    TurnOutcome.TurnCompleted,
    undefined,
  )

let activeJournal
const transcripts = new Map()

const captureLastAssistant = async (delegateKey, answer) => {
  const key = String(delegateKey)
  const messages = transcripts.get(key) ?? []
  messages.push({ role: 'assistant', parts: [xTraceCapture.text(answer)] })
  transcripts.set(key, messages)
  await xTraceCapture.captureProjection(
    activeJournal,
    sessionId(delegateKey),
    xTraceCapture.semantic({ messages }),
  )
}

const withHarness = async (fn, { tier = 'Fast' } = {}) => {
  SessionPersona_clearAllForTests()
  const base = mkdtempSync(join(tmpdir(), 'wxs-sync-lifecycle-'))
  const opened = await agentJournal.create({ directory: base })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  activeJournal = opened.journal
  transcripts.clear()

  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const createCalls = []
  const prompts = []
  let physicalSeq = 0
  const ownerTier = { current: tier }

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

  const quiescence = createQuiescenceGate()
  const attached = createAttached()
  const runtime = new SyncDelegateRuntime(
    sessions,
    dispatcher,
    opened.journal,
    attached,
    (_owner) => roles.tier(ownerTier.current),
    (_delegateSession, _agent) => {},
    quiescence,
    undefined,
    undefined,
    undefined,
    undefined,
    // EXEC-031: bounded WorkRecord via the real journal projector.
    (_sid, range) => lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(opened.journal, _sid, range),
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

const waitFor = async (predicate, message, ms = 2000) => {
  const deadline = Date.now() + ms
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

const settlePendingInvoke = async (runtime, delegateKey, role, answer, runId = 'asst_return') => {
  await captureLastAssistant(delegateKey, answer)
  const handled = await handleTurn(runtime, completionTurn(delegateKey, role, answer, runId), undefined)
  assert.equal(handled, true)
}

test('EXEC_026_sync_delegate_reuses_session_after_full_completion', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_reuse'
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'pass one')
    await waitFor(() => createCalls.length === 1 && prompts.length === 1, 'first create/send missing')
    const delegateId = createCalls[0].child

    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'answer one', 'asst_one')
    const firstDone = resultOf(await first)
    assert.equal(firstDone.ok, true, firstDone.error)
    assert.match(firstDone.value, /answer one/)

    const second = invoke(runtime, owner, SyncDelegateRole.Inspector, 'pass two')
    await waitFor(() => prompts.length === 2, 'second send missing')
    assert.equal(createCalls.length, 1, 'GetOrCreate must reuse; createChild once')

    await settlePendingInvoke(runtime, delegateId, roles.of('Inspector'), 'answer two', 'asst_two')
    const secondDone = resultOf(await second)
    assert.equal(secondDone.ok, true, secondDone.error)
    assert.match(secondDone.value, /answer two/)
    assert.equal(createCalls[0].child, delegateId)
  })
})

test('G6_deleted_inspector_child_retires_live_binding_but_survives_for_owner_scope_close', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_g6_cascade'
    const inspectorRole = roles.of('Inspector')
    const first = invoke(runtime, owner, SyncDelegateRole.Inspector, 'Q1 before owner cascade')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Inspector Q1 did not send')
    const deletedChild = createCalls[0].child

    await settlePendingInvoke(runtime, deletedChild, inspectorRole, 'A1', 'asst_g6_q1')
    assert.equal(resultOf(await first).ok, true)
    assert.equal(idValue.session(tryFind(runtime, sessionId(owner), SyncDelegateRole.Inspector)), deletedChild)

    assert.equal(
      stageDeletedInspector(runtime, sessionId(owner), sessionId(deletedChild)),
      true,
      'child SessionDeleted must stage the attached Inspector for owner scope close',
    )
    assert.equal(tryFind(runtime, sessionId(owner), SyncDelegateRole.Inspector), undefined, 'dead child is not reusable')
    assert.equal(
      idValue.session(tryFindForScopeClose(runtime, sessionId(owner), SyncDelegateRole.Inspector)),
      deletedChild,
      'owner SessionDeleted can still resolve the retired Inspector for CaseFinalize',
    )

    const second = invoke(runtime, owner, SyncDelegateRole.Inspector, 'Q2 after unexpected child delete')
    await waitFor(() => prompts.length === 2 && createCalls.length === 2, 'continued owner did not replace dead Inspector')
    assert.notEqual(createCalls[1].child, deletedChild)
    assert.equal(
      tryFindForScopeClose(runtime, sessionId(owner), SyncDelegateRole.Inspector)?.fields[0],
      createCalls[1].child,
      'continued owner must discard the staged child and use the replacement binding',
    )

    cancelSession(runtime, sessionId(owner))
    assert.equal(resultOf(await second).ok, false)
  })
})

test('G2_inspector_cancel_owner_fails_pending_invoke_no_extra_child', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_g2_cancel'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'inspect then cancel')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')
    assert.equal(createCalls.length, 1)

    let invokeSettled = false
    let invokeResult
    pending.then((value) => {
      invokeSettled = true
      invokeResult = value
    })

    cancelSession(runtime, sessionId(owner))
    await waitFor(() => invokeSettled, 'Invoke did not fail after owner CancelSession')

    const done = resultOf(invokeResult)
    assert.equal(done.ok, false)
    assert.match(done.error, /cancelled/i)
    assert.equal(createCalls.length, 1, 'cancel must not CreateChildSession again')
  })
})

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

test('EXEC_027_cancel_before_completion_fails_pending_invoke', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_cancel'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'cancel before completion')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')

    let invokeSettled = false
    let invokeResult
    pending.then((value) => {
      invokeSettled = true
      invokeResult = value
    })

    cancelSession(runtime, sessionId(owner))
    await waitFor(() => invokeSettled, 'Invoke did not settle after CancelSession')

    const done = resultOf(invokeResult)
    assert.equal(done.ok, false)
    assert.match(done.error, /cancelled/i)
  })
})
