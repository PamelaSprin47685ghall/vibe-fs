// G2 Inspector canary: PREFIX LAW on a reused Inspector child.
//
// same PrefixEpoch: ProviderWire(n) is an exact prefix of ProviderWire(n+1)
// Authority: Domain ProviderProjection.isAppendOnlyPrefix (via
// tests/e2e/support/provider-wire.js wireOf / sealHolds — not a second prefix helper).
//
// sync-delegate-runtime.test.mjs G2_inspector_Q1_Q2_Q3_same_session_serial_reuse
// records prompt text only and does NOT prove ProviderWire.
// StrengthReplicaRuntime is InternalLeaf (FrozenMirror / K+1), not this G2 path.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

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
  SyncDelegateRuntime__Invoke_1B1DD6DD as invoke,
  SyncDelegateRuntime__HandleTurn_Z7791586C as handleTurn,
  SyncDelegateRuntime__Dispose as disposeRuntime,
} from '../../../dist/Session/SyncDelegateRuntime.js'
import {
  isAppendOnlyPrefix,
  sealHolds,
  wireOf,
} from '../../e2e/support/provider-wire.js'
import {
  agentJournal,
  authorityRoot,
  idValue,
  lifecycleWorkRecordProjection,
  listItems,
  mapEntries,
  okResult,
  physicalUser,
  promptDispatcher,
  providerProjection,
  providerRun,
  reconcileSupervisor,
  resultOf,
  roles,
  sessionId,
  xTraceCapture,
} from '../support/domain.mjs'

const SYNC_RETURN_COMPLETION = 'Sync delegate answer returned to caller.'
const INSPECTOR_AGENT = 'fast-inspector'
const INSPECTOR_PROVIDER = 'g2-test-provider'
const INSPECTOR_MODEL_ID = 'g2-inspector-model'
const INSPECTOR_MODEL = new OpencodeModel(INSPECTOR_PROVIDER, INSPECTOR_MODEL_ID, undefined)

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

const modelIdOf = (options) => {
  const model = options?.Model
  return model?.modelID ?? model?.ModelId ?? undefined
}

const openaiToolsFromDispatcher = (options) => {
  const tools = options?.Tools
  assert.ok(
    tools,
    'dispatcher SendPrompt carried no Tools map; cannot obtain a real provider wire from the Inspector send',
  )
  return mapEntries(tools)
    .filter(([, enabled]) => enabled)
    .map(([name]) => ({ type: 'function', function: { name } }))
}

let activeJournal

const withHarness = async (fn, { tier = 'Fast' } = {}) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-g2-inspector-wire-'))
  const opened = agentJournal.create({ directory: base })
  activeJournal = opened.journal
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))

  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const createCalls = []
  const prompts = []
  const captures = []
  const transcripts = new Map()
  let physicalSeq = 0

  const sessionTranscript = (sessionKey) => {
    if (!transcripts.has(sessionKey)) transcripts.set(sessionKey, [])
    return transcripts.get(sessionKey)
  }

  const captureOpenAiBody = (sessionKey, text, options) => {
    const agent = options?.Agent
    const boundModelId = modelIdOf(options)
    assert.ok(
      typeof boundModelId === 'string' && boundModelId.length > 0,
      'Inspector child SendPrompt Model.modelID is missing; PREFIX LAW ModelId is unprovable while ChatParamsHook/PromptDispatcherSend default Model=None',
    )
    sessionTranscript(sessionKey).push({ role: 'user', content: text })
    const body = {
      model: boundModelId,
      tools: openaiToolsFromDispatcher(options),
      messages: sessionTranscript(sessionKey).map((message) => ({ ...message })),
    }
    const wire = wireOf(body)
    assert.ok(listItems(wire.Messages).length > 0, 'wireOf produced an empty message list')
    captures.push({ session: sessionKey, agent, body, wire, modelId: boundModelId })
    return wire
  }

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
        tools: options?.Tools,
      })
      captureOpenAiBody(sessionKey, text, options)
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

  const appendAssistantFromReturn = (sessionKey, answer) => {
    sessionTranscript(sessionKey).push({ role: 'assistant', content: answer })
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
    undefined,
    undefined,
    undefined,
    INSPECTOR_MODEL,
    // EXEC-031: bounded WorkRecord via the real journal projector.
    (_sid, range) => lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(opened.journal, _sid, range),
  )

  try {
    const xtraceMessages = new Map()
    await fn({
      runtime,
      dispatcher,
      createCalls,
      prompts,
      captures,
      ready,
      appendAssistantFromReturn,
      xtraceMessages,
    })
  } finally {
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(base, { recursive: true, force: true })
  }
}

const settlePendingInvoke = async (runtime, { appendAssistantFromReturn, xtraceMessages }, delegateKey, role, answer, runId) => {
  appendAssistantFromReturn(delegateKey, answer)
  const messages = xtraceMessages.get(delegateKey) ?? []
  messages.push({ role: 'assistant', parts: [xTraceCapture.text(answer)] })
  xtraceMessages.set(delegateKey, messages)
  xTraceCapture.captureProjection(
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

test('G2_inspector_Q1_Q2_Q3_provider_wire_append_only_prefix', async () => {
  await withHarness(async (harness) => {
    const { runtime, dispatcher, prompts, createCalls, captures } = harness
    const owner = 'ses_owner_g2_wire'
    const inspector = roles.of('Inspector')
    const questions = ['Q1', 'Q2', 'Q3']
    const answers = ['answer Q1', 'answer Q2', 'answer Q3']

    const q1 = invoke(runtime, owner, SyncDelegateRole.Inspector, questions[0])
    await waitFor(() => createCalls.length === 1 && captures.length === 1, 'Q1 create/send missing')
    const delegateId = createCalls[0].child
    assert.equal(createCalls[0].agent, INSPECTOR_AGENT)
    assert.equal(createCalls[0].title, INSPECTOR_AGENT)
    assert.equal(prompts[0].agent, INSPECTOR_AGENT)
    assert.equal(prompts[0].session, delegateId)
    assert.equal(prompts[0].text, questions[0])
    assert.equal(captures[0].session, delegateId)
    assert.equal(captures[0].agent, INSPECTOR_AGENT)
    assert.equal(captures[0].modelId, INSPECTOR_MODEL_ID)

    await settlePendingInvoke(runtime, harness, delegateId, inspector, answers[0], 'asst_q1')
    const q1Done = resultOf(await q1)
    assert.equal(q1Done.ok, true, q1Done.error)
    assert.match(q1Done.value, /answer Q1/)

    const q2 = invoke(runtime, owner, SyncDelegateRole.Inspector, questions[1])
    await waitFor(() => captures.length === 2, 'Q2 send missing')
    assert.equal(createCalls.length, 1, 'GetOrCreate must reuse; CreateChildSession once')
    assert.equal(createCalls[0].child, delegateId)
    assert.equal(prompts[1].agent, INSPECTOR_AGENT)
    assert.equal(prompts[1].session, delegateId)
    assert.equal(prompts[1].text, questions[1])
    assert.equal(captures[1].session, delegateId)
    assert.equal(captures[1].agent, INSPECTOR_AGENT)
    assert.equal(captures[1].modelId, INSPECTOR_MODEL_ID)

    await settlePendingInvoke(runtime, harness, delegateId, inspector, answers[1], 'asst_q2')
    const q2Done = resultOf(await q2)
    assert.equal(q2Done.ok, true, q2Done.error)
    assert.match(q2Done.value, /answer Q2/)

    const q3 = invoke(runtime, owner, SyncDelegateRole.Inspector, questions[2])
    await waitFor(() => captures.length === 3, 'Q3 send missing')
    assert.equal(createCalls.length, 1, 'GetOrCreate must reuse; CreateChildSession once')
    assert.equal(createCalls[0].child, delegateId)
    assert.equal(prompts[2].agent, INSPECTOR_AGENT)
    assert.equal(prompts[2].session, delegateId)
    assert.equal(prompts[2].text, questions[2])
    assert.equal(captures[2].session, delegateId)
    assert.equal(captures[2].agent, INSPECTOR_AGENT)
    assert.equal(captures[2].modelId, INSPECTOR_MODEL_ID)

    await settlePendingInvoke(runtime, harness, delegateId, inspector, answers[2], 'asst_q3')
    const q3Done = resultOf(await q3)
    assert.equal(q3Done.ok, true, q3Done.error)
    assert.match(q3Done.value, /answer Q3/)

    assert.equal(new Set([delegateId, captures[0].session, captures[1].session, captures[2].session]).size, 1)
    assert.deepEqual(
      [createCalls[0].agent, prompts[0].agent, prompts[1].agent, prompts[2].agent],
      [INSPECTOR_AGENT, INSPECTOR_AGENT, INSPECTOR_AGENT, INSPECTOR_AGENT],
    )
    assert.deepEqual(
      [q1Done.value, q2Done.value, q3Done.value].map((value) => value.match(/answer Q[123]/)?.[0]),
      answers,
    )

    const wireQ1 = captures[0].wire
    const wireQ2 = captures[1].wire
    const wireQ3 = captures[2].wire

    const journalAgent = promptDispatcher.projectionFor(dispatcher, delegateId)?.LastAuthorityProfile?.SelectedAgent
    assert.equal(journalAgent, INSPECTOR_AGENT)

    assert.equal(wireQ1.ModelId, INSPECTOR_MODEL_ID)
    assert.equal(wireQ1.ModelId, wireQ2.ModelId, 'wire.ModelId must be identical on reused Inspector Q1→Q2')
    assert.equal(wireQ2.ModelId, wireQ3.ModelId, 'wire.ModelId must be identical on reused Inspector Q2→Q3')
    assert.equal(captures[0].modelId, INSPECTOR_MODEL_ID)
    assert.equal(captures[0].modelId, captures[1].modelId)
    assert.equal(captures[1].modelId, captures[2].modelId)
    assert.equal(wireQ1.ModelId, captures[0].modelId)
    assert.equal(wireQ2.ModelId, captures[1].modelId)
    assert.equal(wireQ3.ModelId, captures[2].modelId)

    const msgCount = (wire) => listItems(wire.Messages).length
    assert.ok(msgCount(wireQ1) >= 1, 'Q1 wire must include the Inspector user prompt')
    assert.ok(msgCount(wireQ2) > msgCount(wireQ1), 'Q2 wire must accumulate Q1 transcript, not a fresh child')
    assert.ok(msgCount(wireQ3) > msgCount(wireQ2), 'Q3 wire must accumulate Q2 transcript, not a fresh child')
    assert.equal(captures[1].body.messages[0].content, questions[0])
    assert.equal(captures[1].body.messages[1].content, answers[0])
    assert.equal(captures[1].body.messages[2].content, questions[1])

    assert.equal(
      sealHolds(wireQ1, captures[1].body),
      true,
      'sealHolds: ProviderWire(Q1) must be an append-only prefix of Q2 body',
    )
    assert.equal(
      sealHolds(wireQ2, captures[2].body),
      true,
      'sealHolds: ProviderWire(Q2) must be an append-only prefix of Q3 body',
    )
    assert.equal(
      isAppendOnlyPrefix(wireQ1, wireQ2),
      true,
      'provider-wire isAppendOnlyPrefix(Q1, Q2)',
    )
    assert.equal(
      providerProjection.isAppendOnlyPrefix(wireQ1, wireQ2),
      true,
      'ProviderProjection.isAppendOnlyPrefix(Q1, Q2) PREFIX LAW',
    )
    assert.equal(
      providerProjection.isAppendOnlyPrefix(wireQ2, wireQ3),
      true,
      'ProviderProjection.isAppendOnlyPrefix(Q2, Q3) PREFIX LAW',
    )
    assert.equal(
      providerProjection.isAppendOnlyPrefix(wireQ2, wireQ1),
      false,
      'prefix must be directional: Q2 is not a prefix of Q1',
    )
  })
})
