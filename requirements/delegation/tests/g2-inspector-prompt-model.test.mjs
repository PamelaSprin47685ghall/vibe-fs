// Split from tests/unit/session/g2-inspector-provider-wire-prefix.test.mjs (cutover Wave 2a); owner: delegation.
//
// SyncDelegate promptModelFor：Deep/Fast owner 各自落在自己的 model 上，不塌缩到
// 另一 tier（DELEG-010 tier 确定性映射的 model 面）。PREFIX LAW 断言已随
// SPLIT@cutover 迁 requirements/prefix-stability/tests/g2-inspector-provider-wire-prefix.test.mjs。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { clearAllForTests as SessionPersona_clearAllForTests } from '../../../dist/Session/SessionPersona.js'
import { OpencodeModel } from '../../../dist/Infrastructure/OpenCode/Codec/OpencodeTypes.js'
import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import { ReconciledTurn } from '../../../dist/Application/Reconciliation/ReconciledTurn.js'
import { SyncDelegateRole } from '../../../dist/Kernel/SyncDelegate.js'
import {
  AttachedSessionRuntime_$ctor_Z5DA00426 as createAttached,
} from '../../../dist/Session/AttachedSessionRuntime.js'
import {
  SyncDelegateRuntime,
  SyncDelegateRuntime__Invoke_FCBDD42 as invoke,
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

const modelIdOf = (options) => {
  const model = options?.Model
  return model?.modelID ?? model?.ModelId ?? undefined
}

let activeJournal

const withHarness = async (fn, options = {}) => {
  SessionPersona_clearAllForTests()
  const { tier = 'Fast' } = options
  const promptModel = Object.prototype.hasOwnProperty.call(options, 'promptModel')
    ? options.promptModel
    : new OpencodeModel('g2-test-provider', 'g2-inspector-model', undefined)
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
        modelId: modelIdOf(options),
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
    promptModel,
    // EXEC-031: bounded WorkRecord via the real journal projector.
    (_sid, range) => lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(opened.journal, _sid, range),
    options.promptModelFor,
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

test('G2_inspector_promptModelFor_keeps_deep_and_fast_owners_on_their_own_models', async () => {
  const FAST_MODEL = new OpencodeModel('g2-test-provider', 'g2-fast-inspector', undefined)
  const DEEP_MODEL = new OpencodeModel('g2-test-provider', 'g2-deep-inspector', undefined)
  const lookup = (agent) => {
    if (agent === 'fast-inspector') return FAST_MODEL
    if (agent === 'deep-inspector') return DEEP_MODEL
    return undefined
  }

  const runOnce = async (tier, expectedAgent, expectedModelId) => {
    await withHarness(
      async (harness) => {
        const { runtime, prompts, createCalls, captures } = harness
        const inspector = roles.of('Inspector')
        const q1 = invoke(runtime, 'ses_owner_model_for', SyncDelegateRole.Inspector, 'Q1')
        await waitFor(() => createCalls.length === 1 && captures.length === 1, 'create/send missing')
        assert.equal(createCalls[0].agent, expectedAgent)
        assert.equal(prompts[0].agent, expectedAgent)
        assert.equal(captures[0].agent, expectedAgent)
        assert.equal(captures[0].modelId, expectedModelId)
        assert.notEqual(
          captures[0].modelId,
          expectedModelId === 'g2-deep-inspector' ? 'g2-fast-inspector' : 'g2-deep-inspector',
          'mixed Deep/Fast owners must not collapse onto the other tier\'s model',
        )

        await settlePendingInvoke(runtime, createCalls[0].child, inspector, 'answer Q1', 'asst_q1')
        const done = resultOf(await q1)
        assert.equal(done.ok, true, done.error)
      },
      { tier, promptModel: undefined, promptModelFor: lookup },
    )
  }

  await runOnce('Fast', 'fast-inspector', 'g2-fast-inspector')
  await runOnce('Deep', 'deep-inspector', 'g2-deep-inspector')
})
