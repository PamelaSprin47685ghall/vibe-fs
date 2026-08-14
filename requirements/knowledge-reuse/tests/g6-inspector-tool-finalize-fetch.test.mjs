// inspector-tool → SyncDelegate → lifecycle → Bookkeeper child → fetch.
// Real InspectorTool.spec Execute + SyncDelegateRuntime; Q/A land via
// onInspectorPrompt/Answer = CasebookLifecycle.notePrompt/noteAnswer.
// NOT full live Host PromptDispatcher / tool.execute.before Long Stroke.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
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
  SyncDelegateRuntime__HandleTurn_Z7791586C as handleTurn,
  SyncDelegateRuntime__Dispose as disposeRuntime,
  SyncDelegateRuntime__TryFind_636E3F87 as tryFind,
} from '../../../dist/Session/SyncDelegateRuntime.js'
import {
  collector,
  setEnabled,
  notePrompt,
  noteAnswer,
  tryFinalizeInspector,
} from '../../../dist/Infrastructure/CasebookLifecycle.js'
import { CasebookWorkflow_fetchCase as fetchCase } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import {
  ObservationCollector__Collect_Z15AE2BE0 as collect,
} from '../../../dist/Infrastructure/ObservationCollector.js'
import { acquire } from '../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Journal/RuntimePath.js'
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
import {
  BookkeeperRuntime_setSessionPort as setSessionPort,
  BookkeeperRuntime_resetSessionPort as resetSessionPort,
  BookkeeperRuntime_txIdFor as txIdFor,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec: inspectorSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/InspectorTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { execute } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsBookkeeperTool.js')
const { TerminalOutcome } = await import('../../../dist/Infrastructure/OpenCode/Host/Events.js')
const { AgentRunResult } = await import('../../../dist/Kernel/Outcome.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')

const SYNC_RETURN_COMPLETION = 'Sync delegate answer returned to caller.'

const CANONICAL_Q = 'Canonical maintained question'
const CANONICAL_A = 'Summary of Inspector answers across turns.'

const QUESTIONS = [
  ['Who owns PromptAuthority?', 'Host owns PromptAuthority.'],
  ['Where do Case facts live?', 'Unified EventStore only.'],
  ['When does CaseFinalize run?', 'ReuseScope close, once.'],
]

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

const context = (session = 'ses_owner', providerRunId) =>
  new HostToolContext(session, undefined, undefined, providerRunId, undefined, () => () => {})

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

let activeJournal
const delegateMessages = new Map()

const settlePendingInvoke = async (runtime, delegateKey, role, answer, runId = 'asst_turn') => {
  const messages = delegateMessages.get(delegateKey) ?? []
  messages.push({ role: 'assistant', parts: [xTraceCapture.text(answer)] })
  delegateMessages.set(delegateKey, messages)
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

const completedTerminal = (child) =>
  new TerminalOutcome(0, [
    new AgentRunResult(child, undefined, undefined, Role.Inspector, undefined, 'wide', 'idle'),
  ])

// Duplicated from bookkeeper-session.test.mjs so importing this file does not
// register that suite's tests.
const scriptedBookkeeperPort = () => {
  const createCalls = []
  const prompts = []
  const programCalls = []
  const terminals = new Set()
  let seq = 0

  const port = {
    CreateChildSession: async (parentId, options) => {
      seq += 1
      const child = sessionId(`bk-child-${seq}`)
      createCalls.push({
        parent: idValue.session(parentId),
        title: options?.Title,
        agent: options?.Agent,
        child: idValue.session(child),
      })
      return { tag: 0, fields: [child] }
    },
    AbortSession: async () => ({ tag: 0, fields: [] }),
    SubscribeTerminal: (_childId, callback) => {
      terminals.add(callback)
      return {
        Dispose: () => {
          terminals.delete(callback)
        },
      }
    },
    SendPrompt: async (childSession, text, _options) => {
      prompts.push(text)
      const sid = idValue.session(childSession)
      const tx = txIdFor(sid)
      assert.equal(Boolean(tx), true, 'SendPrompt must run against a bound Bookkeeper tx')
      const out = await execute(
        makeArgs({
          program: `class Js extends JsProgram {
            async run() {
              this.setQuestion(${JSON.stringify(CANONICAL_Q)});
              this.setAnswer(${JSON.stringify(CANONICAL_A)});
              return { changed: true };
            }
          }`,
        }),
        context(sid),
      )
      assert.equal(String(out).includes('changed = true'), true, out)
      programCalls.push(tx)
      for (const callback of terminals) callback(childSession, completedTerminal(childSession))
      return { tag: 0, fields: [] }
    },
  }

  return { port, createCalls, prompts, programCalls }
}

const withHarness = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-g6-inspector-tool-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')

  const opened = await agentJournal.create({ directory: dir })
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
      const child = sessionId(`g6-insp-tool-${createCalls.length + 1}`)
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
    (_owner) => roles.tier('Fast'),
    (_delegateSession, _agent) => {},
    createQuiescenceGate(),
    dir,
    notePrompt,
    noteAnswer,
    undefined,
    undefined,
    // EXEC-031: bounded WorkRecord via the real journal projector.
    (_sid, range) => lifecycleWorkRecordProjection.lifecycleWorkRecordBounded(opened.journal, _sid, range),
  )

  const scope = new ToolRuntimeScope(
    sessions,
    opened.journal,
    undefined,
    dir,
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

  setEnabled(dir)
  const bookkeeper = scriptedBookkeeperPort()
  setSessionPort(bookkeeper.port)
  try {
    await fn({
      dir,
      runtime,
      scope,
      createCalls,
      prompts,
      bookkeeper,
    })
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    disposeRuntime(runtime)
    opened.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

test('G6_inspector_tool_sync_delegate_lifecycle_bookkeeper_fetch', async () => {
  await withHarness(async ({ dir, runtime, scope, createCalls, prompts, bookkeeper }) => {
    const owner = 'ses_meditator_inspector_tool'
    const inspectorRole = roles.of('Inspector')
    const tool = inspectorSpec(factory, scope, runtime)
    let delegateId

    for (let i = 0; i < QUESTIONS.length; i += 1) {
      const [q, a] = QUESTIONS[i]
      const pending = tool.Execute(makeArgs({ charge: q }), context(owner))

      await waitFor(
        () => prompts.length === i + 1 && createCalls.length === 1,
        `InspectorTool Q${i + 1} did not reuse a single child`,
      )

      if (i === 0) {
        delegateId = createCalls[0].child
        assert.equal(createCalls[0].agent, 'fast-inspector')
        const found = tryFind(runtime, sessionId(owner), SyncDelegateRole.Inspector)
        assert.ok(found != null, 'TryFind must return Some while delegate is attached')
        assert.equal(idValue.session(found), delegateId)
      } else {
        assert.equal(createCalls[0].child, delegateId, 'GetOrCreate must reuse Inspector session')
        assert.equal(prompts[i].session, delegateId)
      }

      assert.equal(prompts[i].text, q)
      await settlePendingInvoke(runtime, delegateId, inspectorRole, a, `asst_q${i + 1}`)
      const text = await pending
      assert.match(text, new RegExp(a.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')))
      assert.equal(parseToml(text).error, undefined)
    }

    assert.equal(createCalls.length, 1, 'Inspector CreateChildSession once across Q1/Q2/Q3')
    assert.equal(prompts.length, 3)
    assert.equal(prompts.every((p) => p.session === delegateId), true)

    collect(collector, delegateId, 'read', { path: 'a.txt' }, 'hello')

    const first = resultOf(await tryFinalizeInspector(dir, delegateId))
    assert.equal(first.ok, true, `tryFinalizeInspector ok: ${JSON.stringify(first.error)}`)
    assert.equal(bookkeeper.createCalls.length, 1, 'Bookkeeper CreateChildSession once')
    assert.equal(bookkeeper.programCalls.length >= 1, true, 'js-bookkeeper must reshape Q and A in one program')
    assert.equal(bookkeeper.prompts.some((text) => String(text).includes('CaseFinalize')), true)
    assert.equal(bookkeeper.prompts.some((text) => String(text).includes('Q1')), true)
    assert.equal(bookkeeper.prompts.some((text) => String(text).includes('Q3')), true)

    const common = gitCommonDir(dir)
    const store = acquire(common)
    const fetched = resultOf(await fetchCase(store, 10, delegateId))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value !== undefined && fetched.value !== null, true, 'Case exists after finalize')
    assert.equal(fetched.value.SessionId, delegateId)
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.Q, QUESTIONS[2][0], 'fetch must not return last Inspector Q')
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(String(fetched.value.A).includes('evidence:'), false)
    assert.equal(String(fetched.value.A).includes('digest'), false)
  })
})
