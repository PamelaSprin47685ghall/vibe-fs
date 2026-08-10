// tests/unit/tools/sync-delegate-tools.test.mjs — InspectorTool + CoderTool
// SyncDelegate cutover + SyncDelegateTools.returnSpec surface.
//
// Fable compiles SyncDelegateRuntime.Invoke / TryFind / Return as free functions
// (`SyncDelegateRuntime__Invoke_…(sd, …)`), so a plain `{ Invoke, TryFind }`
// object cannot drive happy-path Execute. Validation paths never reach Invoke
// and accept any truthy runtime sentinel; happy path reuses the real
// SyncDelegateRuntime harness pattern from session/sync-delegate-runtime.test.mjs.
//
// EXEC-026: no agent enum on inspector/coder; coder tdd required.
// EXEC-028: dual-await Returned → Completion; completion literal is fixed.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import { SessionQuiescenceGate_$ctor as createQuiescenceGate } from '../../../dist/Infrastructure/OpenCode/Host/SessionQuiescenceGate.js'
import { TurnOutcome } from '../../../dist/Domain/ReconcileProgram.js'
import { ReconciledTurn } from '../../../dist/Application/Reconciliation/ReconciledTurn.js'
import { SyncDelegateRole } from '../../../dist/Kernel/SyncDelegate.js'
import {
  AttachedSessionRuntime_$ctor_Z5DA00426 as createAttached,
} from '../../../dist/Session/AttachedSessionRuntime.js'
import {
  SyncDelegateRuntime,
  SyncDelegateRuntime__Return_Z65460A0C as returnAnswer,
  SyncDelegateRuntime__HandleTurn_Z7791586C as handleTurn,
  SyncDelegateRuntime__Dispose as disposeRuntime,
  SyncDelegateRuntime__TryFind_636E3F87 as tryFind,
} from '../../../dist/Session/SyncDelegateRuntime.js'
import {
  agentJournal,
  authorityRoot,
  idValue,
  listItems,
  okResult,
  physicalUser,
  promptDispatcher,
  providerRun,
  reconcileSupervisor,
  resultOf,
  roles,
  sessionId,
  tddPhase,
} from '../support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec: inspectorSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/InspectorTool.js')
const { spec: coderSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/CoderTool.js')
const { returnSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/SyncDelegateTools.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')

const SYNC_RETURN_COMPLETION = 'Sync delegate answer returned to caller.'

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: (description) => chain(`${kind}-described`, { ...extra, description }),
  optional: () => chain(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => chain('string'),
  enum: (values) => chain('enum', { values }),
  array: (inner) => chain('array', { inner }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

/** Truthy sentinel — validation returns before Fable free-function Invoke. */
const sentinelRuntime = { kind: 'sync-delegate-sentinel' }

const context = (session = 'ses_owner', providerRunId) =>
  new HostToolContext(session, undefined, undefined, providerRunId, undefined, () => () => {})

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

const settlePendingInvoke = async (runtime, delegateKey, role, answer, runId = 'asst_return') => {
  const returned = resultOf(await returnAnswer(runtime, delegateKey, providerRun(runId), answer))
  assert.equal(returned.ok, true, returned.ok ? '' : returned.error)

  const handled = await handleTurn(runtime, completionTurn(delegateKey, role), undefined)
  assert.equal(handled, true)
}

/** Real SyncDelegateRuntime + fake ISessionHostPort (copied pattern, not shared helpers). */
const withHarness = async (fn, { tier = 'Fast' } = {}) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-sync-delegate-tools-'))
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
  const runtime = new SyncDelegateRuntime(
    sessions,
    dispatcher,
    opened.journal,
    attached,
    (_owner) => roles.tier(tier),
    () => {},
    createQuiescenceGate(),
    undefined,
  )

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
    undefined,
    undefined,
  )

  try {
    await fn({ runtime, createCalls, prompts, scope })
  } finally {
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(base, { recursive: true, force: true })
  }
}

// ── spec surface (EXEC-026: no agent; SyncDelegate Inspector/Coder only) ─────

test('INSPECTOR_spec_exposes_prompt_prompts_only_no_agent', () => {
  const tool = inspectorSpec(factory, bareScope(), undefined)
  assert.equal(tool.Name, 'inspector')
  assert.match(tool.Description, /Reusable/)
  assert.match(tool.Description, /Returned→Completion|Returned->Completion/)
  assert.match(tool.Description, /not dispose-after/)
  assert.deepEqual(argNames(tool), ['prompt', 'prompts'])
})

test('CODER_spec_exposes_tdd_prompt_prompts_no_agent', () => {
  const tool = coderSpec(factory, bareScope(), undefined)
  assert.equal(tool.Name, 'coder')
  assert.match(tool.Description, /Reusable/)
  assert.match(tool.Description, /Returned→Completion|Returned->Completion/)
  assert.match(tool.Description, /not dispose-after/)
  assert.match(tool.Description, /tdd=red\|green/)
  assert.deepEqual(argNames(tool), ['tdd', 'prompt', 'prompts'])
})

test('RETURN_spec_exposes_message_argument', () => {
  const tool = returnSpec(factory, undefined)
  assert.equal(tool.Name, 'return')
  assert.match(tool.Description, /SyncDelegate/)
  assert.deepEqual(argNames(tool), ['message'])
})

// ── missing SyncDelegate runtime ─────────────────────────────────────────────

test('INSPECTOR_missing_sync_delegate_runtime_errors', async () => {
  const { fields } = await runToml(inspectorSpec(factory, bareScope(), undefined), { prompt: 'look' }, context())
  assert.equal(fields.error, 'SyncDelegate runtime unavailable')
})

test('CODER_missing_sync_delegate_runtime_errors', async () => {
  const { fields } = await runToml(
    coderSpec(factory, bareScope(), undefined),
    { tdd: 'red', prompt: 'implement' },
    context(),
  )
  assert.equal(fields.error, 'SyncDelegate runtime unavailable')
})

test('RETURN_unavailable_when_sync_delegate_runtime_missing', async () => {
  const { fields } = await runToml(returnSpec(factory, undefined), { message: 'done' }, context())
  assert.equal(fields.error, 'return unavailable')
})

// ── validation before Invoke (sentinel runtime never invoked) ────────────────

test('INSPECTOR_missing_prompt_is_refused', async () => {
  const { fields } = await runToml(
    inspectorSpec(factory, bareScope(), sentinelRuntime),
    {},
    context(),
  )
  assert.equal(fields.error, 'inspector prompt required')
})

test('INSPECTOR_blank_prompt_is_refused', async () => {
  const { fields } = await runToml(
    inspectorSpec(factory, bareScope(), sentinelRuntime),
    { prompt: '   ' },
    context(),
  )
  assert.equal(fields.error, 'inspector prompt required')
})

test('CODER_missing_tdd_rejected_before_invoke', async () => {
  const { fields } = await runToml(
    coderSpec(factory, bareScope(), sentinelRuntime),
    { prompt: 'work' },
    context(),
  )
  assert.equal(fields.error, 'missing required argument: tdd')
})

test('CODER_invalid_tdd_rejected_before_invoke', async () => {
  const { fields } = await runToml(
    coderSpec(factory, bareScope(), sentinelRuntime),
    { tdd: 'blue', prompt: 'work' },
    context(),
  )
  assert.equal(fields.error, 'UnknownTddPhase blue')
})

test('CODER_missing_prompt_refused_after_tdd_ok', async () => {
  const { fields } = await runToml(
    coderSpec(factory, bareScope(), sentinelRuntime),
    { tdd: 'green' },
    context(),
  )
  assert.equal(fields.error, 'coder prompt required')
})

// ── happy path via real SyncDelegateRuntime harness ──────────────────────────

test('INSPECTOR_happy_path_invokes_inspector_and_encodes_inspector_id', async () => {
  await withHarness(async ({ runtime, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_insp'
    const answer = 'inspector formal answer'
    const pending = inspectorSpec(factory, scope, runtime).Execute(
      makeArgs({ prompt: 'inspect the module' }),
      context(owner),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Inspector Invoke did not send')
    assert.equal(createCalls[0].agent, 'fast-inspector')
    assert.equal(prompts[0].text, 'inspect the module')
    assert.equal(prompts[0].agent, 'fast-inspector')

    const found = tryFind(runtime, sessionId(owner), SyncDelegateRole.Inspector)
    assert.ok(found != null, 'TryFind must return Some while delegate is attached')
    assert.equal(idValue.session(found), createCalls[0].child)

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Inspector'), answer, 'asst_insp')
    const text = await pending
    const parsed = parseToml(text)
    assert.match(text, /inspector formal answer/)
    assert.equal(parsed.inspector_id, createCalls[0].child)
    assert.equal(parsed.error, undefined)
  })
})

test('CODER_happy_path_composes_tdd_red_and_encodes_coder_id', async () => {
  await withHarness(async ({ runtime, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_coder'
    const body = 'add failing test for X'
    const answer = 'coder red answer'
    const pending = coderSpec(factory, scope, runtime).Execute(
      makeArgs({ tdd: 'red', prompt: body }),
      context(owner),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Coder Invoke did not send')
    assert.equal(createCalls[0].agent, 'fast-coder')
    assert.equal(prompts[0].text, tddPhase.composeAssignment(tddPhase.parse('red').value, body))
    assert.match(prompts[0].text, /TDD phase: RED/)
    assert.match(prompts[0].text, /add failing test for X/)

    const found = tryFind(runtime, sessionId(owner), SyncDelegateRole.Coder)
    assert.ok(found != null, 'TryFind must return Some for Coder')
    assert.equal(idValue.session(found), createCalls[0].child)

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Coder'), answer, 'asst_coder')
    const text = await pending
    const parsed = parseToml(text)
    assert.match(text, /coder red answer/)
    assert.equal(parsed.coder_id, createCalls[0].child)
    assert.equal(parsed.tdd, 'red')
  })
})

test('CODER_happy_path_composes_tdd_green', async () => {
  await withHarness(async ({ runtime, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_green'
    const body = 'smallest fix'
    const pending = coderSpec(factory, scope, runtime).Execute(
      makeArgs({ tdd: 'green', prompt: body }),
      context(owner),
    )

    await waitFor(() => prompts.length === 1, 'Coder green Invoke did not send')
    assert.equal(prompts[0].text, tddPhase.composeAssignment(tddPhase.parse('green').value, body))
    assert.match(prompts[0].text, /TDD phase: GREEN/)

    await settlePendingInvoke(runtime, createCalls[0].child, roles.of('Coder'), 'green done', 'asst_green')
    const parsed = parseToml(await pending)
    assert.equal(parsed.tdd, 'green')
    assert.equal(parsed.coder_id, createCalls[0].child)
  })
})

// ── returnSpec SyncDelegate-only (no StudentTeacher fallthrough) ─────────────
// Empty journal: SyncDelegate.Return → "no active SyncDelegate Authority Root".
// ToolRegistry yields SyncDelegateTools.returnSpec only — that error surfaces.

test('RETURN_surfaces_sync_delegate_error_when_no_authority_root', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-return-sd-only-'))
  const opened = agentJournal.create({ directory: base })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))

  const dispatcher = promptDispatcher.forJournal(opened.journal)
  const sessions = {
    SubscribeTerminal: () => ({ Dispose: () => {} }),
    SendPrompt: async () => promptDispatcher.admittedWithPhysicalMessage('msg_phys_sd'),
    CreateChildSession: async () => okResult(sessionId('child-sd')),
  }

  const syncDelegate = new SyncDelegateRuntime(
    sessions,
    dispatcher,
    opened.journal,
    createAttached(),
    (_owner) => roles.tier('Fast'),
    () => {},
    createQuiescenceGate(),
    undefined,
  )

  try {
    const { fields } = await runToml(
      returnSpec(factory, syncDelegate),
      { message: 'answer' },
      context('ses_neither'),
    )
    assert.match(fields.error, /no active SyncDelegate Authority Root/)
  } finally {
    disposeRuntime(syncDelegate)
    opened.dispose()
    rmSync(base, { recursive: true, force: true })
  }
})

// EXEC-028 via returnSpec: Return settles answer side; Completion literal finishes Invoke.

test('RETURN_happy_path_dual_awaits_completion_literal', async () => {
  await withHarness(async ({ runtime, createCalls, prompts, scope }) => {
    const owner = 'ses_owner_return'
    const answer = 'delegate answer for caller'
    const pending = inspectorSpec(factory, scope, runtime).Execute(
      makeArgs({ prompt: 'please answer' }),
      context(owner),
    )

    await waitFor(() => prompts.length === 1 && createCalls.length === 1, 'Inspector Invoke did not send')

    let invokeSettled = false
    let invokeText
    pending.then((text) => {
      invokeSettled = true
      invokeText = text
    })

    const { text: returnText, fields } = await runToml(
      returnSpec(factory, runtime),
      { message: answer },
      context(createCalls[0].child, providerRun('asst_dual')),
    )
    assert.equal(fields.error, undefined)
    assert.equal(fields.completion_text, SYNC_RETURN_COMPLETION)
    assert.match(returnText, /durably recorded/)

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
    assert.match(invokeText, /delegate answer for caller/)
    assert.equal(parseToml(invokeText).inspector_id, createCalls[0].child)
  })
})
