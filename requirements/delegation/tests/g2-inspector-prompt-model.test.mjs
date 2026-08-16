// Split from tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs (cutover Wave 2a); owner: delegation.
//
// SyncDelegate 只决定 Inspector 的 EffectiveAgent；物理 model 由外层
// execution-model-routing session lease 接管。PREFIX LAW 断言已随 SPLIT@cutover
// 迁 requirements/prefix-stability/tests/g2-inspector-provider-wire-prefix.test.mjs。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { clearAllForTests as SessionPersona_clearAllForTests } from '../../../dist/Participant/Persona/SessionPersona.js'
import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/OpenCode/Host/SessionQuiescenceGate.js'
import { TurnOutcome } from '../../../dist/Composition/Turn/Program.js'
import { ReconciledTurn } from '../../../dist/Composition/Turn/Observation.js'
import { SyncDelegateRole } from '../../../dist/Execution/Delegation/SyncDelegate/Model.js'
const attachedModule = await import('../../../dist/Execution/Session/Attachment/AttachedRuntime.js')
const createAttached = Object.entries(attachedModule).find(([k]) => k.startsWith('AttachedSessionRuntime_$ctor'))?.[1]
const syncDelegateRuntimeModule = await import('../../../dist/Execution/Delegation/SyncDelegate/Runtime.js')
const { SyncDelegateRuntime, SyncDelegateRuntime__Dispose: disposeRuntime } = syncDelegateRuntimeModule
const invoke = Object.entries(syncDelegateRuntimeModule).find(([k]) => k.startsWith('SyncDelegateRuntime__Invoke_'))?.[1]
const handleTurn = Object.entries(syncDelegateRuntimeModule).find(([k]) => k.startsWith('SyncDelegateRuntime__HandleTurn_'))?.[1]
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

let activeJournal

const withHarness = async (fn, options = {}) => {
  SessionPersona_clearAllForTests()
  const { tier = 'Fast' } = options
  const base = mkdtempSync(join(tmpdir(), 'wxs-g2-model-'))
  const opened = await agentJournal.create({ directory: base })
  activeJournal = opened.journal
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))

  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const createCalls = []
  const prompts = []
  const captures = []
  let physicalSeq = 0

  const sessions = {
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    SendPrompt: async (session, text, options) => {
      physicalSeq += 1
      const sessionKey = idValue.session(session)
      prompts.push({
        session: sessionKey,
        text,
        agent: options?.Agent,
        model: options?.Model,
      })
      captures.push({
        session: sessionKey,
        agent: options?.Agent,
        model: options?.Model,
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
    createQuiescenceGate(),
    undefined,
    undefined,
    undefined,
    undefined,
    // EXEC-031: bounded WorkRecord via the real journal projector.
    (_sid, range) => lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(opened.journal, _sid, range),
  )

  try {
    await fn({ runtime, prompts, createCalls, captures })
  } finally {
    SessionPersona_clearAllForTests()
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(base, { recursive: true, force: true })
  }
}

const settlePendingInvoke = async (runtime, delegateKey, role, answer, runId) => {
  const messages = [{ role: 'assistant', parts: [xTraceCapture.text(answer)] }]
  await xTraceCapture.captureProjection(
    activeJournal,
    sessionId(delegateKey),
    xTraceCapture.semantic({ messages }),
  )
  const handled = await handleTurn(
    runtime,
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
    ),
    undefined,
  )
  assert.equal(handled, true)
}

test('WHAT[DELEG-010] G2_inspector_selects_tier_agent_without_owning_a_static_model_binding', async () => {
  const runOnce = async (tier, expectedAgent) => {
    await withHarness(
      async (harness) => {
        const { runtime, prompts, createCalls, captures } = harness
        const inspector = roles.of('Inspector')
        const q1 = invoke(runtime, 'ses_owner_model_for', SyncDelegateRole.Inspector, 'Q1')
        await waitFor(() => createCalls.length === 1 && captures.length === 1, 'create/send missing')
        assert.equal(createCalls[0].agent, expectedAgent)
        assert.equal(prompts[0].agent, expectedAgent)
        assert.equal(captures[0].agent, expectedAgent)
        assert.equal(captures[0].model, undefined, 'SyncDelegate must leave physical model authority to ModelRouting')

        await settlePendingInvoke(runtime, createCalls[0].child, inspector, 'answer Q1', 'asst_q1')
        const done = resultOf(await q1)
        assert.equal(done.ok, true, done.error)
      },
      { tier },
    )
  }

  await runOnce('Fast', 'fast-inspector')
  await runOnce('Deep', 'deep-inspector')
})
