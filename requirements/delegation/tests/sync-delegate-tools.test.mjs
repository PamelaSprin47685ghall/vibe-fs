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
} from '../../../dist/Foundation/Identity.js'
import {
  SessionMessage,
  SessionToolPart,
  SnapshotToolPartState,
} from '../../../dist/OpenCode/Host/SessionSnapshotPort.js'
import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/OpenCode/Host/SessionQuiescenceGate.js'
import { clearAllForTests as SessionPersona_clearAllForTests } from '../../../dist/Participant/Persona/SessionPersona.js'
import { TurnOutcome } from '../../../dist/Composition/Turn/Program.js'
import { ReconciledTurn } from '../../../dist/Composition/Turn/Observation.js'
import { SyncDelegateRole } from '../../../dist/Execution/Delegation/SyncDelegate/Model.js'
const attachedModule = await import('../../../dist/Execution/Session/Attachment/AttachedRuntime.js')
const createAttached = Object.entries(attachedModule).find(([k]) => k.startsWith('AttachedSessionRuntime_$ctor'))?.[1]
const syncDelegateRuntimeModule = await import('../../../dist/Execution/Delegation/SyncDelegate/Runtime.js')
const { SyncDelegateRuntime, SyncDelegateRuntime__Dispose: disposeRuntime } = syncDelegateRuntimeModule
const handleTurn = Object.entries(syncDelegateRuntimeModule).find(([k]) => k.startsWith('SyncDelegateRuntime__HandleTurn_'))?.[1]
const invokePrepared = Object.entries(syncDelegateRuntimeModule).find(([k]) => k.startsWith('SyncDelegateRuntime__InvokePrepared_'))?.[1]
const observeProviderToolCall = Object.entries(syncDelegateRuntimeModule).find(([k]) => k.startsWith('SyncDelegateRuntime__ObserveProviderToolCall_'))?.[1]
const tryFind = Object.entries(syncDelegateRuntimeModule).find(([k]) => k.startsWith('SyncDelegateRuntime__TryFind_'))?.[1]
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
} = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')
const { spec: inspectSpec } = await import('../../../dist/OpenCode/Tools/InspectorTool.js')
const { establishSpec, repairSpec } = await import('../../../dist/OpenCode/Tools/CoderTool.js')
const { ToolRuntimeScope } = await import('../../../dist/OpenCode/Tools/ToolRuntimeScope.js')
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

const failedTurn = (delegateKey, role, error = '503 Service Unavailable', runId = 'asst_fail') =>
  new ReconciledTurn(
    sessionId(delegateKey),
    physicalUser('msg_phys_fail'),
    authorityRoot('msg_root_fail'),
    providerRun(runId),
    role,
    undefined,
    [reconcileSupervisor.textPart(error)],
    'error',
    undefined,
    undefined,
    new TurnOutcome(4, [error]),
    undefined,
  )

const captureLastAssistant = async (journal, delegateKey, answer) => {
  await xTraceCapture.captureProjection(
    journal,
    sessionId(delegateKey),
    xTraceCapture.semantic({
      messages: [{ role: 'assistant', parts: [xTraceCapture.text(answer)] }],
    }),
  )
}

const settlePendingInvoke = async (runtime, journal, delegateKey, role, answer, runId = 'asst_complete') => {
  await captureLastAssistant(journal, delegateKey, answer)
  const handled = await handleTurn(runtime, completionTurn(delegateKey, role, answer, runId), undefined)
  assert.equal(handled, true)
}

let harnessSequence = 0

const withHarness = async (fn, { tier = 'Fast', snapshotMessages, snapshotGetMessages } = {}) => {
  SessionPersona_clearAllForTests()
  harnessSequence += 1
  const harnessId = harnessSequence
  const base = mkdtempSync(join(tmpdir(), 'wxs-sync-delegate-tools-'))
  const opened = await agentJournal.create({ directory: base })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const createCalls = []
  const prompts = []
  const inspectorPrompts = []
  let physicalSeq = 0
  const terminalListeners = new Map()

  const sessions = {
    SubscribeTerminal: (session, listener) => {
      const key = idValue.session(session)
      const listeners = terminalListeners.get(key) ?? []
      listeners.push(listener)
      terminalListeners.set(key, listeners)
      return {
        Dispose: () => {
          const current = terminalListeners.get(key) ?? []
          terminalListeners.set(key, current.filter((l) => l !== listener))
        },
      }
    },
    SendPrompt: async (session, text, options) => {
      physicalSeq += 1
      prompts.push({
        session: idValue.session(session),
        text,
        agent: options?.Agent,
      })
      return promptDispatcher.admittedWithPhysicalMessage(`msg_phys_${harnessId}_${physicalSeq}`)
    },
    CreateChildSession: async (parentId, options) => {
      const child = sessionId(`delegate-${harnessId}-${createCalls.length + 1}`)
      createCalls.push({
        parent: idValue.session(parentId),
        agent: options?.Agent,
        title: options?.Title,
        child: idValue.session(child),
      })
      return okResult(child)
    },
  }

  const notifyTerminal = (delegateKey, outcome) => {
    const key = String(delegateKey)
    const listeners = terminalListeners.get(key) ?? []
    for (const listener of listeners) {
      listener(sessionId(key), outcome)
    }
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
    // EXEC-031: bounded WorkRecord via the real journal projector.
    (_sid, range) => lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(opened.journal, _sid, range),
  )

  const snapshot = snapshotGetMessages
    ? { GetMessages: snapshotGetMessages }
    : snapshotMessages
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
    await fn({ runtime, journal: opened.journal, createCalls, prompts, inspectorPrompts, scope, delegatedRemaining, notifyTerminal })
  } finally {
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(base, { recursive: true, force: true })
  }
}

test('WHAT[DELEG-017] INSPECT_spec_exposes_charge_plus_optional_keywords_no_agent', () => {
  const tool = inspectSpec(factory, bareScope(), undefined)
  assert.equal(tool.Name, 'inspect')
  assert.match(tool.Description, /WorkRecord/)
  assert.deepEqual(argNames(tool), ['charge', 'keywords', 'expected_tool_calls'])
})

test('WHAT[DELEG-017] ESTABLISH_AND_REPAIR_specs_expose_charge_plus_optional_keywords', () => {
  const establish = establishSpec(factory, bareScope(), undefined)
  const repair = repairSpec(factory, bareScope(), undefined)
  assert.equal(establish.Name, 'establish-behavior')
  assert.equal(repair.Name, 'repair-behavior')
  assert.deepEqual(argNames(establish), ['charge', 'keywords', 'expected_tool_calls'])
  assert.deepEqual(argNames(repair), ['charge', 'keywords', 'expected_tool_calls'])
})

test('WHAT[DELEG-017] INSPECT_missing_sync_delegate_runtime_is_a_natural_consequence', async () => {
  const { text, fields } = await runToml(inspectSpec(factory, bareScope(), undefined), { charge: 'look' }, context())
  assert.match(text, /No Inspector is available from this execution context|当前执行语境中没有可用的 Inspector/)
  assert.equal(fields.error, undefined)
})

test('WHAT[DELEG-017] ESTABLISH_missing_sync_delegate_runtime_is_a_natural_consequence', async () => {
  const { text, fields } = await runToml(
    establishSpec(factory, bareScope(), undefined),
    { charge: 'establish failing test' },
    context(),
  )
  assert.match(text, /No Coder is available from this execution context|当前执行语境中没有可用的 Coder/)
  assert.equal(fields.error, undefined)
})

test('WHAT[DELEG-017] INSPECT_missing_charge_is_refused_as_a_natural_consequence', async () => {
  const { text, fields } = await runToml(inspectSpec(factory, bareScope(), sentinelRuntime), {}, context())
  assert.match(text, /inspect needs a charge|inspect 需要一项 charge/)
  assert.equal(fields.error, undefined)
})

test('WHAT[DELEG-017] INSPECT_blank_charge_is_refused_as_a_natural_consequence', async () => {
  const { text, fields } = await runToml(
    inspectSpec(factory, bareScope(), sentinelRuntime),
    { charge: '   ' },
    context(),
  )
  assert.match(text, /inspect needs a charge|inspect 需要一项 charge/)
  assert.equal(fields.error, undefined)
})

test('WHAT[DELEG-017] ESTABLISH_missing_charge_is_refused_as_a_natural_consequence', async () => {
  const { text, fields } = await runToml(establishSpec(factory, bareScope(), sentinelRuntime), {}, context())
  assert.match(text, /establish-behavior needs a charge|establish-behavior 需要一项 charge/)
  assert.equal(fields.error, undefined)
})

test('WHAT[DELEG-019] EXEC_032_prepared_provider_prompt_does_not_replace_semantic_inspector_charge', async () => {
  await withHarness(async ({ runtime, journal, createCalls, prompts, inspectorPrompts }) => {
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

    await settlePendingInvoke(runtime, journal, createCalls[0].child, roles.of('Inspector'), 'bounded answer', 'asst_prepared')
    const result = resultOf(await pending)
    assert.equal(result.ok, true, result.error)
  })
})

test('WHAT[DELEG-022] DELEG_022_sync_delegate_batch_sums_explicit_estimates_once', async () => {
  const runId = 'asst_inspect_estimate_batch'
  const firstCall = toolCallId('inspect_estimate_1')
  const secondCall = toolCallId('inspect_estimate_2')
  const message = batchMessage(runId, [
    { id: firstCall, tool: 'inspect' },
    { id: secondCall, tool: 'inspect' },
  ])

  await withHarness(async ({ runtime, journal, createCalls, prompts, scope, delegatedRemaining }) => {
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

    await settlePendingInvoke(runtime, journal, createCalls[0].child, roles.of('Inspector'), 'estimated batch answer')
    await first
    await second
  }, { snapshotMessages: [message] })
})

test('WHAT[DELEG-022] DELEG_022_sync_delegate_omission_retains_reused_delegate_remaining', async () => {
  await withHarness(async ({ runtime, journal, createCalls, prompts, scope, delegatedRemaining }) => {
    const owner = 'ses_owner_inspect_estimate_reuse'
    const tool = inspectSpec(factory, scope, runtime)

    const first = tool.Execute(makeArgs({ charge: 'first inquiry', expected_tool_calls: 4 }), context(owner))
    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'first estimated inspect did not send')
    const child = createCalls[0].child
    assert.equal(delegatedRemaining(child), 4)
    await settlePendingInvoke(runtime, journal, child, roles.of('Inspector'), 'first answer', 'asst_estimate_first')
    await first

    const second = tool.Execute(makeArgs({ charge: 'second inquiry' }), context(owner))
    await waitFor(() => prompts.length === 2, 'reused inspect did not send')
    assert.equal(createCalls.length, 1, 'Inspector session must be reused')
    assert.equal(delegatedRemaining(child), 4, 'omitted expected_tool_calls must preserve remaining')
    await settlePendingInvoke(runtime, journal, child, roles.of('Inspector'), 'second answer', 'asst_estimate_second')
    await second
  })
})

test('WHAT[DELEG-008] EXEC_026_inspect_tool_uses_host_provider_batch_and_returns_body_once', async () => {
  const runId = 'asst_inspect_batch'
  const firstCall = toolCallId('inspect_call_1')
  const secondCall = toolCallId('inspect_call_2')
  const message = batchMessage(runId, [
    { id: firstCall, tool: 'inspect' },
    { id: secondCall, tool: 'inspect' },
  ])

  await withHarness(async ({ runtime, journal, createCalls, prompts, scope }) => {
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

    await settlePendingInvoke(runtime, journal, createCalls[0].child, roles.of('Inspector'), 'batched inspector answer')
    const firstText = await first
    const secondText = await second

    assert.match(firstText, /batched inspector answer/)
    assert.doesNotMatch(secondText, /batched inspector answer/)
    assert.match(secondText, /inspect_call_1/)
  }, { snapshotMessages: [message] })
})

test('WHAT[DELEG-008] DELEG_008_inspect_batch_waits_for_complete_host_tool_call_set_before_dispatch', async () => {
  const runId = 'asst_inspect_batch_late_snapshot'
  const firstCall = toolCallId('inspect_late_1')
  const secondCall = toolCallId('inspect_late_2')
  const thirdCall = toolCallId('inspect_late_3')
  const partial = batchMessage(runId, [{ id: firstCall, tool: 'inspect' }])
  const complete = batchMessage(runId, [
    { id: firstCall, tool: 'inspect' },
    { id: secondCall, tool: 'inspect' },
    { id: thirdCall, tool: 'inspect' },
  ])
  let snapshotReads = 0

  await withHarness(async ({ runtime, journal, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_inspect_late_snapshot'
    const tool = inspectSpec(factory, scope, runtime)

    observeProviderToolCall(runtime, sessionId(owner), providerRun(runId), SyncDelegateRole.Inspector, firstCall)
    observeProviderToolCall(runtime, sessionId(owner), providerRun(runId), SyncDelegateRole.Inspector, secondCall)
    observeProviderToolCall(runtime, sessionId(owner), providerRun(runId), SyncDelegateRole.Inspector, thirdCall)
    const first = tool.Execute(
      makeArgs({ charge: 'inspect first late snapshot' }),
      context(owner, providerRun(runId), firstCall),
    )

    await waitFor(() => snapshotReads === 1, 'first inspect did not observe the deliberately stale snapshot')
    assert.equal(prompts.length, 0, 'complete Host event projection must not dispatch before all sibling invokes arrive')

    const second = tool.Execute(
      makeArgs({ charge: 'inspect second late snapshot' }),
      context(owner, providerRun(runId), secondCall),
    )
    const third = tool.Execute(
      makeArgs({ charge: 'inspect third late snapshot' }),
      context(owner, providerRun(runId), thirdCall),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'complete three-call inspect batch did not send once')
    assert.equal(
      prompts[0].text,
      'inspect first late snapshot\n\ninspect second late snapshot\n\ninspect third late snapshot',
    )

    await settlePendingInvoke(runtime, journal, createCalls[0].child, roles.of('Inspector'), 'late snapshot combined answer')
    const firstText = await first
    const secondText = await second
    const thirdText = await third

    assert.match(firstText, /late snapshot combined answer/)
    assert.match(secondText, /inspect_late_1/)
    assert.match(thirdText, /inspect_late_1/)
    assert.doesNotMatch(secondText, /could not complete|未能完成/i)
    assert.doesNotMatch(thirdText, /could not complete|未能完成/i)
    assert.equal(snapshotReads, 3, 'each sibling reconciles the independent Host event + snapshot views')
  }, {
    snapshotGetMessages: async () => {
      snapshotReads += 1
      return okResult(toList([snapshotReads === 1 ? partial : complete]))
    },
  })
})

test('WHAT[DELEG-008] EXEC_026_coder_sync_surfaces_share_one_semantic_batch', async () => {
  const runId = 'asst_coder_batch'
  const establishCall = toolCallId('coder_call_1')
  const repairCall = toolCallId('coder_call_2')
  const message = batchMessage(runId, [
    { id: establishCall, tool: 'establish-behavior' },
    { id: repairCall, tool: 'repair-behavior' },
  ])

  await withHarness(async ({ runtime, journal, createCalls, prompts, scope }) => {
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

    await settlePendingInvoke(runtime, journal, createCalls[0].child, roles.of('Coder'), 'batched coder answer')
    const establishText = await establishPending
    const repairText = await repairPending

    assert.match(establishText, /batched coder answer/)
    assert.doesNotMatch(repairText, /batched coder answer/)
    assert.match(repairText, /coder_call_1/)
  }, { snapshotMessages: [message] })
})

test('WHAT[DELEG-012] INSPECT_happy_path_invokes_inspector_and_returns_work_record', async () => {
  await withHarness(async ({ runtime, journal, createCalls, prompts, scope }) => {
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

    await settlePendingInvoke(runtime, journal, createCalls[0].child, roles.of('Inspector'), answer, 'asst_insp')
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

test('WHAT[DELEG-023] INSPECT_transient_failure_retries_and_returns_successful_work_record', async () => {
  await withHarness(async ({ runtime, journal, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_insp_retry'
    const tool = inspectSpec(factory, scope, runtime)
    const pending = tool.Execute(
      makeArgs({ charge: 'inspect with transient retry' }),
      context(owner),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Inspector Invoke did not send')
    const delegateId = createCalls[0].child

    // Turn 1 fails with transient provider error
    const failed = await handleTurn(
      runtime,
      failedTurn(delegateId, roles.of('Inspector'), '503 Service Unavailable', 'asst_fail_1'),
      undefined,
    )
    assert.equal(failed, false, 'transient failure must NOT complete tool prematurely')

    // Tool execution must still be in flight
    let settledEarly = false
    pending.then(() => { settledEarly = true }).catch(() => { settledEarly = true })
    await new Promise((resolve) => setTimeout(resolve, 50))
    assert.equal(settledEarly, false, 'inspect tool execution must remain pending during retry')

    // Turn 2 succeeds after retry
    await settlePendingInvoke(
      runtime,
      journal,
      delegateId,
      roles.of('Inspector'),
      'findings verified after retry',
      'asst_retry_2',
    )

    const text = await pending
    assert.match(text, /findings verified after retry/)
    assert.match(text, /Recent work/)
    assert.equal(parseToml(text).error, undefined)
  })
})

test('WHAT[DELEG-023] CODER_establish_behavior_transient_failure_retries_and_returns_successful_work_record', async () => {
  await withHarness(async ({ runtime, journal, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_coder_retry'
    const tool = establishSpec(factory, scope, runtime)
    const pending = tool.Execute(
      makeArgs({ charge: 'establish behavior with retry' }),
      context(owner),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Coder Invoke did not send')
    const delegateId = createCalls[0].child

    // Turn 1 fails
    const failed = await handleTurn(
      runtime,
      failedTurn(delegateId, roles.of('Coder'), 'rate_limit_exceeded', 'asst_coder_fail_1'),
      undefined,
    )
    assert.equal(failed, false)

    // Turn 2 succeeds
    await settlePendingInvoke(
      runtime,
      journal,
      delegateId,
      roles.of('Coder'),
      'behavior established after retry',
      'asst_coder_retry_2',
    )

    const text = await pending
    assert.match(text, /behavior established after retry/)
    assert.match(text, /Recent work/)
    assert.equal(parseToml(text).error, undefined)
  })
})

test('WHAT[DELEG-023] INSPECT_exhausted_failure_via_terminal_event_returns_incomplete_error', async () => {
  await withHarness(async ({ runtime, createCalls, prompts, scope, notifyTerminal }) => {
    const owner = 'ses_owner_insp_exhausted'
    const tool = inspectSpec(factory, scope, runtime)
    const pending = tool.Execute(
      makeArgs({ charge: 'inspect with exhausted failure' }),
      context(owner),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Inspector Invoke did not send')
    const delegateId = createCalls[0].child

    const { TerminalOutcome } = await import('../../../dist/OpenCode/Host/Events.js')
    notifyTerminal(delegateId, new TerminalOutcome(2, ['provider budget exhausted']))

    const text = await pending
    assert.match(text, /could not complete|未能完成|incomplete|failed/i)
  })
})

