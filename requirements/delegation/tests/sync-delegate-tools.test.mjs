// tests/unit/tools/sync-delegate-tools.test.mjs — inspect + establish/repair-behavior SyncDelegate surface.
// EXEC-026/031: ordinary assistant completion → bounded WorkRecord; no return tool.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import {
  HostToolPartIdModule_create as hostToolPartId,
  ToolCallIdModule_create as toolCallId,
} from '../../../dist/Kernel/Identity.js'
import {
  SessionMessage,
  SessionToolPart,
  SnapshotToolPartState,
} from '../../../dist/Infrastructure/OpenCode/Host/SessionSnapshotPort.js'
import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'
import { clearAllForTests as SessionPersona_clearAllForTests } from '../../../dist/Session/SessionPersona.js'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import { ReconciledTurn } from '../../../dist/Composition/Turn/Observation.js'
import { SyncDelegateRole } from '../../../dist/Kernel/SyncDelegate.js'
import {
  AttachedSessionRuntime_$ctor_Z5DA00426 as createAttached,
} from '../../../dist/Session/AttachedSessionRuntime.js'
import {
  SyncDelegateRuntime,
  SyncDelegateRuntime__HandleTurn_7C364186 as handleTurn,
  SyncDelegateRuntime__InvokePrepared_Z13135FAE as invokePrepared,
  SyncDelegateRuntime__Dispose as disposeRuntime,
  SyncDelegateRuntime__TryFind_636E3F87 as tryFind,
} from '../../../dist/Session/SyncDelegateRuntime.js'
import {
  agentJournal,
  authorityRoot,
  idValue,
  lifecycleWorkRecordProjection,
  listItems,
  okResult,
  physicalUser,
  promptDispatcher,
  providerRun,
  toList,
  reconcileSupervisor,
  resultOf,
  roles,
  sessionId,
  xTraceCapture,
} from '../../verification-system/tests/support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec: inspectSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/InspectorTool.js')
const { establishSpec, repairSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/CoderTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { DelegatedToolEstimateProjection_remaining: estimateRemaining } = await import(
  '../../../dist/Execution/Delegation/DelegatedToolEstimateProjection.js'
)

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  int: () => chain(`${kind}-int`, extra),
  nonnegative: () => chain(`${kind}-nonnegative`, extra),
  describe: (description) => chain(`${kind}-described`, { ...extra, description }),
  optional: () => chain(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => chain('string'),
  number: () => chain('number'),
  enum: (values) => chain('enum', { values }),
  array: (inner) => chain('array', { inner }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const sentinelRuntime = { kind: 'sync-delegate-sentinel' }

const context = (session = 'ses_owner', providerRunId, callId) =>
  new HostToolContext(session, undefined, callId, providerRunId, undefined, () => () => {})

const batchMessage = (id, calls) =>
  new SessionMessage(
    id,
    'assistant',
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    true,
    false,
    undefined,
    [],
    calls.map(
      ({ id: callId, tool }, index) =>
        new SessionToolPart(
          hostToolPartId(`part_${index + 1}`),
          callId,
          tool,
          '{}',
          SnapshotToolPartState.Pending,
        ),
    ),
  )

const bareScope = () =>
  new ToolRuntimeScope(
    {},
    undefined,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )

const argNames = (tool) => listItems(tool.Arguments).map(([name]) => name)

const runToml = async (tool, args, ctx) => {
  const text = await tool.Execute(makeArgs(args), ctx)
  return { text, fields: parseToml(text) }
}

const waitFor = async (predicate, message, ms = 2000) => {
  const deadline = Date.now() + ms
  while (!predicate()) {
    if (Date.now() >= deadline) throw new Error(message)
    await new Promise((resolve) => setImmediate(resolve))
  }
}

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

const captureLastAssistant = (delegateKey, answer) => {
  xTraceCapture.captureProjection(
    activeJournal,
    sessionId(delegateKey),
    xTraceCapture.semantic({
      messages: [{ role: 'assistant', parts: [xTraceCapture.text(answer)] }],
    }),
  )
}

const settlePendingInvoke = async (runtime, delegateKey, role, answer, runId = 'asst_complete') => {
  captureLastAssistant(delegateKey, answer)
  const handled = await handleTurn(runtime, completionTurn(delegateKey, role, answer, runId), undefined)
  assert.equal(handled, true)
}

const withHarness = async (fn, { tier = 'Fast', snapshotMessages } = {}) => {
  SessionPersona_clearAllForTests()
  const base = mkdtempSync(join(tmpdir(), 'wxs-sync-delegate-tools-'))
  const opened = await agentJournal.create({ directory: base })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  activeJournal = opened.journal

  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const createCalls = []
  const prompts = []
  const inspectorPrompts = []
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
  const runtime = new SyncDelegateRuntime(
    sessions,
    dispatcher,
    opened.journal,
    attached,
    (_owner) => roles.tier(tier),
    () => {},
    createQuiescenceGate(),
    undefined,
    (_session, charge) => inspectorPrompts.push(charge),
    undefined,
    undefined,
    undefined,
    // EXEC-031: bounded WorkRecord via the real journal projector.
    (_sid, range) => lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(opened.journal, _sid, range),
  )

  const snapshot = snapshotMessages
    ? { GetMessages: async () => okResult(toList(snapshotMessages)) }
    : undefined

  const scope = new ToolRuntimeScope(
    sessions,
    opened.journal,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    snapshot,
    undefined,
  )

  const delegatedRemaining = (childKey) => {
    const state = agentJournal.snapshot(opened.journal).AgentProjections.Sessions.get(sessionId(childKey)).DelegatedToolEstimate
    return estimateRemaining(state)
  }

  try {
    await fn({ runtime, createCalls, prompts, inspectorPrompts, scope, delegatedRemaining })
  } finally {
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(base, { recursive: true, force: true })
  }
}

test('INSPECT_spec_exposes_charge_plus_optional_keywords_no_agent', () => {
  const tool = inspectSpec(factory, bareScope(), undefined)
  assert.equal(tool.Name, 'inspect')
  assert.match(tool.Description, /WorkRecord/)
  assert.deepEqual(argNames(tool), ['charge', 'keywords', 'expected_tool_calls'])
})

test('ESTABLISH_AND_REPAIR_specs_expose_charge_plus_optional_keywords', () => {
  const establish = establishSpec(factory, bareScope(), undefined)
  const repair = repairSpec(factory, bareScope(), undefined)
  assert.equal(establish.Name, 'establish-behavior')
  assert.equal(repair.Name, 'repair-behavior')
  assert.deepEqual(argNames(establish), ['charge', 'keywords', 'expected_tool_calls'])
  assert.deepEqual(argNames(repair), ['charge', 'keywords', 'expected_tool_calls'])
})

test('INSPECT_missing_sync_delegate_runtime_is_a_natural_consequence', async () => {
  const { text, fields } = await runToml(inspectSpec(factory, bareScope(), undefined), { charge: 'look' }, context())
  assert.match(text, /No Inspector is available from this execution context|当前执行语境中没有可用的 Inspector/)
  assert.equal(fields.error, undefined)
})

test('ESTABLISH_missing_sync_delegate_runtime_is_a_natural_consequence', async () => {
  const { text, fields } = await runToml(
    establishSpec(factory, bareScope(), undefined),
    { charge: 'establish failing test' },
    context(),
  )
  assert.match(text, /No Coder is available from this execution context|当前执行语境中没有可用的 Coder/)
  assert.equal(fields.error, undefined)
})

test('INSPECT_missing_charge_is_refused_as_a_natural_consequence', async () => {
  const { text, fields } = await runToml(inspectSpec(factory, bareScope(), sentinelRuntime), {}, context())
  assert.match(text, /inspect needs a charge|inspect 需要一项 charge/)
  assert.equal(fields.error, undefined)
})

test('INSPECT_blank_charge_is_refused_as_a_natural_consequence', async () => {
  const { text, fields } = await runToml(
    inspectSpec(factory, bareScope(), sentinelRuntime),
    { charge: '   ' },
    context(),
  )
  assert.match(text, /inspect needs a charge|inspect 需要一项 charge/)
  assert.equal(fields.error, undefined)
})

test('ESTABLISH_missing_charge_is_refused_as_a_natural_consequence', async () => {
  const { text, fields } = await runToml(establishSpec(factory, bareScope(), sentinelRuntime), {}, context())
  assert.match(text, /establish-behavior needs a charge|establish-behavior 需要一项 charge/)
  assert.equal(fields.error, undefined)
})

test('EXEC_032_prepared_provider_prompt_does_not_replace_semantic_inspector_charge', async () => {
  await withHarness(async ({ runtime, createCalls, prompts, inspectorPrompts }) => {
    const owner = 'ses_owner_prepared'
    const charge = 'raw semantic question'
    const providerPrompt = '# enriched provider envelope\n\n[[repository_hint]]\nfile_path = "src/a.fs"\n'
    const pending = invokePrepared(
      runtime,
      owner,
      SyncDelegateRole.Inspector,
      charge,
      async () => providerPrompt,
    )

    await waitFor(
      () => prompts.length === 1 && createCalls.length === 1 && inspectorPrompts.length === 1,
      'prepared Inspector Invoke did not finish its post-send semantic hook',
    )
    assert.equal(prompts[0].text, providerPrompt, 'provider must receive prepared bytes')
    assert.deepEqual(inspectorPrompts, [charge], 'Casebook Q hook must receive raw semantic Charge')

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'bounded answer', 'asst_prepared')
    const result = resultOf(await pending)
    assert.equal(result.ok, true, result.error)
  })
})

test('DELEG_022_sync_delegate_batch_sums_explicit_estimates_once', async () => {
  const runId = 'asst_inspect_estimate_batch'
  const firstCall = toolCallId('inspect_estimate_1')
  const secondCall = toolCallId('inspect_estimate_2')
  const message = batchMessage(runId, [
    { id: firstCall, tool: 'inspect' },
    { id: secondCall, tool: 'inspect' },
  ])

  await withHarness(async ({ runtime, createCalls, prompts, scope, delegatedRemaining }) => {
    const owner = 'ses_owner_inspect_estimate_batch'
    const tool = inspectSpec(factory, scope, runtime)

    const second = tool.Execute(
      makeArgs({ charge: 'inspect second', expected_tool_calls: 5 }),
      context(owner, providerRun(runId), secondCall),
    )
    const first = tool.Execute(
      makeArgs({ charge: 'inspect first', expected_tool_calls: 2 }),
      context(owner, providerRun(runId), firstCall),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'estimated inspect batch did not send once')
    assert.equal(delegatedRemaining(createCalls[0].child), 7)

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'estimated batch answer')
    await first
    await second
  }, { snapshotMessages: [message] })
})

test('DELEG_022_sync_delegate_omission_retains_reused_delegate_remaining', async () => {
  await withHarness(async ({ runtime, createCalls, prompts, scope, delegatedRemaining }) => {
    const owner = 'ses_owner_inspect_estimate_reuse'
    const tool = inspectSpec(factory, scope, runtime)

    const first = tool.Execute(makeArgs({ charge: 'first inquiry', expected_tool_calls: 4 }), context(owner))
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'first estimated inspect did not send')
    const child = createCalls[0].child
    assert.equal(delegatedRemaining(child), 4)
    await settlePendingInvoke(runtime, child, roles.of('Inspector'), 'first answer', 'asst_estimate_first')
    await first

    const second = tool.Execute(makeArgs({ charge: 'second inquiry' }), context(owner))
    await waitFor(() => prompts.length === 2, 'reused inspect did not send')
    assert.equal(createCalls.length, 1, 'Inspector session must be reused')
    assert.equal(delegatedRemaining(child), 4, 'omitted expected_tool_calls must preserve remaining')
    await settlePendingInvoke(runtime, child, roles.of('Inspector'), 'second answer', 'asst_estimate_second')
    await second
  })
})

test('EXEC_026_inspect_tool_uses_host_provider_batch_and_returns_body_once', async () => {
  const runId = 'asst_inspect_batch'
  const firstCall = toolCallId('inspect_call_1')
  const secondCall = toolCallId('inspect_call_2')
  const message = batchMessage(runId, [
    { id: firstCall, tool: 'inspect' },
    { id: secondCall, tool: 'inspect' },
  ])

  await withHarness(async ({ runtime, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_inspect_batch'
    const tool = inspectSpec(factory, scope, runtime)

    const second = tool.Execute(
      makeArgs({ charge: 'inspect second' }),
      context(owner, providerRun(runId), secondCall),
    )
    const first = tool.Execute(
      makeArgs({ charge: 'inspect first' }),
      context(owner, providerRun(runId), firstCall),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'batched inspect did not send once')
    assert.equal(prompts[0].text, 'inspect first\n\ninspect second')

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), 'batched inspector answer')
    const firstText = await first
    const secondText = await second

    assert.match(firstText, /batched inspector answer/)
    assert.doesNotMatch(secondText, /batched inspector answer/)
    assert.match(secondText, /inspect_call_1/)
  }, { snapshotMessages: [message] })
})

test('EXEC_026_coder_sync_surfaces_share_one_semantic_batch', async () => {
  const runId = 'asst_coder_batch'
  const establishCall = toolCallId('coder_call_1')
  const repairCall = toolCallId('coder_call_2')
  const message = batchMessage(runId, [
    { id: establishCall, tool: 'establish-behavior' },
    { id: repairCall, tool: 'repair-behavior' },
  ])

  await withHarness(async ({ runtime, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_coder_batch'
    const establish = establishSpec(factory, scope, runtime)
    const repair = repairSpec(factory, scope, runtime)

    const repairPending = repair.Execute(
      makeArgs({ charge: 'repair behavior' }),
      context(owner, providerRun(runId), repairCall),
    )
    const establishPending = establish.Execute(
      makeArgs({ charge: 'establish behavior' }),
      context(owner, providerRun(runId), establishCall),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'batched Coder did not send once')
    assert.equal(createCalls[0].agent, 'fast-coder')
    assert.equal(prompts[0].text, 'establish behavior\n\nrepair behavior')

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Coder'), 'batched coder answer')
    const establishText = await establishPending
    const repairText = await repairPending

    assert.match(establishText, /batched coder answer/)
    assert.doesNotMatch(repairText, /batched coder answer/)
    assert.match(repairText, /coder_call_1/)
  }, { snapshotMessages: [message] })
})

test('INSPECT_happy_path_invokes_inspector_and_returns_work_record', async () => {
  await withHarness(async ({ runtime, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_insp'
    const answer = 'inspector formal answer'
    const pending = inspectSpec(factory, scope, runtime).Execute(
      makeArgs({ charge: 'inspect the module' }),
      context(owner),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Inspector Invoke did not send')
    assert.equal(createCalls[0].agent, 'fast-inspector')
    assert.equal(prompts[0].text, 'inspect the module')

    const found = tryFind(runtime, sessionId(owner), SyncDelegateRole.Inspector)
    assert.ok(found != null, 'TryFind must return Some while delegate is attached')
    assert.equal(idValue.session(found), createCalls[0].child)

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), answer, 'asst_insp')
    const text = await pending
    // EXEC-031: the tool payload is the bounded WorkRecord — last assistant
    // text in Recent work (not the raw last message as the whole payload),
    // and Opening is not echoed back (includeOpening=false).
    assert.match(text, /Recent work/)
    assert.match(text, /inspector formal answer/)
    assert.doesNotMatch(text, /Closing report/)
    assert.doesNotMatch(text, /^Opening\n/m)
    assert.equal(parseToml(text).error, undefined)
  })
})
