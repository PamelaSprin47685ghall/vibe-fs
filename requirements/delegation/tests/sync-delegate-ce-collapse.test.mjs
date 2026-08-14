// Split from tests/unit/session/sync-delegate-ce-collapse.test.mjs (cutover Wave 2a); owner: delegation.
//
// EXEC-031 单栈断言：whitespace-normalized completion 经 bounded WorkRecord
// Recent work 解析 invoke（无 deleted return/TextComplete 通道）。dispose/cancel
// scope 断言已随 SPLIT@cutover 迁
// requirements/managed-session-lifecycle/tests/sync-delegate-lifecycle.test.mjs。

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
  SyncDelegateRuntime__HandleTurn_Z7791586C as handleTurn,
  SyncDelegateRuntime__Dispose as disposeRuntime,
} from '../../../dist/Session/SyncDelegateRuntime.js'
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

let activeJournal

const withHarness = async (fn) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-sync-ce-'))
  const opened = await agentJournal.create({ directory: base })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  activeJournal = opened.journal

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

  const quiescence = createQuiescenceGate()
  const attached = createAttached()
  const runtime = new SyncDelegateRuntime(
    sessions,
    dispatcher,
    opened.journal,
    attached,
    (_owner) => roles.tier('Fast'),
    (_delegateSession, _agent) => {},
    quiescence,
    undefined,
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

test('EXEC_031_whitespace_normalized_completion_resolves_invoke', async () => {
  await withHarness(async ({ runtime, prompts, createCalls }) => {
    const owner = 'ses_owner_normalize'
    const pending = invoke(runtime, owner, SyncDelegateRole.Inspector, 'normalize')
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Invoke did not send')
    const delegate = createCalls[0].child

    const answer = '  normalized answer  \n'
    xTraceCapture.captureProjection(
      activeJournal,
      sessionId(delegate),
      xTraceCapture.semantic({
        messages: [{ role: 'assistant', parts: [xTraceCapture.text(answer)] }],
      }),
    )
    const handled = await handleTurn(
      runtime,
      turn(delegate, roles.of('Inspector'), answer, 'asst_norm_complete'),
      undefined,
    )
    assert.equal(handled, true)

    const done = resultOf(await pending)
    assert.equal(done.ok, true, done.error)
    // The answer travels inside the bounded WorkRecord's Recent work, not as
    // a trimmed raw last-message payload (EXEC-031).
    assert.match(done.value, /normalized answer/)
    assert.match(done.value, /Recent work/)
    assert.doesNotMatch(done.value, /Closing report/)
  })
})
