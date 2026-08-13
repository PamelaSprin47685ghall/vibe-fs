// tests/unit/Plugin/plugin-fixture.mjs — shared layer-2 plugin fixture (VERIFY-008).
//
// Deliberately NOT named `*.test.mjs`. `tests/unit/runner.mjs:98` discovers tests with
// `walk('tests/unit', ['.test.mjs'])`, so a helper carrying that suffix would be run as
// a test file (zero tests inside it, but its top-level import cost paid twice).
// `scripts/architecture-gate.mjs` scans `['.mjs']`, so this file is still gated.

import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

if (!process.env.WANXIANGSHU_PROVIDER_LANGUAGE || process.env.WANXIANGSHU_PROVIDER_LANGUAGE === 'undefined') {
  process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
}

const { initSpikePlugin } = await import('../../../dist/Infrastructure/OpenCode/Plugin/SpikePlugin.js')

// ── the layer-3 executable fixture (EXEC-002, AGENT-007 layer two) ───────────
//
// `hooks.tool.*.execute` needs two things the layer-2 fixture deliberately does
// not have: a session transport under `client.session` (without one
// `Sessions.fs` SendPrompt fails closed with "No Host transport" — the old
// fabricated `"test output"` completion was removed), and an accepted Authority
// Root per calling session (PROMPT-002: without one the role is unresolved and
// every tool returns `{"error":"...no Authority Root..."}`).
//
// HOST-009 keeps both internal ports OFF the hooks object by design, so this
// layer reaches them the way production itself does — through the two
// process-local shared owners keyed by the same workspace runtime path
// (`RuntimePath.forWorkspace`): `SharedAgentJournal.acquire` and
// `SharedTerminalBus.acquire` return the exact journal instance and exact
// HostEventPort `initSpikePlugin` wired. No production export or visibility was
// widened for this (VERIFY-008); the same-import precedent is this file's own
// `initSpikePlugin` line.
const { forWorkspace, gitCommonDir } = await import('../../../dist/Journal/RuntimePath.js')
const { acquire: acquireJournal, release: releaseJournal } = await import('../../../dist/Journal/SharedAgentJournal.js')
const { acquire: acquireTerminalBus } = await import('../../../dist/Infrastructure/OpenCode/Host/SharedTerminalBus.js')
const { bootPort } = await import('../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js')
const { AgentJournalModule_runtimeId, AgentJournalModule_createFromProjection } = await import(
  '../../../dist/Journal/AgentJournal.js'
)
const { forJournal, Runtime__AcceptHumanRoot, Runtime__AcceptAgentOwnerRoot } = await import(
  '../../../dist/Application/Prompting/PromptDispatcher.js'
)
const { SessionIdModule_create, PhysicalUserMessageIdModule_create, PromptKeyModule_create, ManagerLifeIdModule_create, BlobDigestModule_create, BlobRefModule_create, ToolCallIdModule_create } = await import(
  '../../../dist/Kernel/Identity.js'
)
const { TerminalOutcome } = await import('../../../dist/Infrastructure/OpenCode/Host/Events.js')
const { AgentRunResult } = await import('../../../dist/Kernel/Outcome.js')
const {
  HandleController_recordCompletion: recordCompletion,
  HandleController_agentHandle: agentHandle,
} = await import('../../../dist/Session/HandleController.js')
const ChildRecovery = await import('../../../dist/Domain/ChildRecovery.js')
const { ManagerLifecycleFact } = await import('../../../dist/Kernel/Fact.js')
const { StreamId } = await import('../../../dist/Journal/Envelope.js')
const {
  MagicTodoFact,
  PhysicalSuccessEvidence,
  TodoWriteAccepted,
  TodoWritePrepared,
} = await import('../../../dist/Domain/MagicTodoFacts.js')
const { TodoWriteIdModule_create } = await import('../../../dist/Domain/MagicTodo.js')
const { XTraceCursor } = await import('../../../dist/Domain/XTrace.js')
const { AgentJournalModule_appendManagerLifecycle, AgentJournalModule_appendMagicTodo } = await import('../../../dist/Journal/AgentJournal.js')
const terminalEvidenceCompleted =
  ChildRecovery.TerminalEvidenceModule_completed ?? ChildRecovery.TerminalEvidence_completed
const terminalEvidenceFailed =
  ChildRecovery.TerminalEvidenceModule_failed ?? ChildRecovery.TerminalEvidence_failed
const tryFromProvenTerminal =
  ChildRecovery.JoinableCompletionModule_tryFromProvenTerminal ??
  ChildRecovery.JoinableCompletion_tryFromProvenTerminal

/**
 * The smallest SDK client double: mint child ids, accept prompts. The id list is
 * handed back so tests can address the child a fork actually created.
 */
const promptedWaiters = new Map()

const stubClient = (createdIds, prompts, messages, abortedIds) => {
  let counter = 0
  const childSessions = []
  // PROMPT-011 physical-message store: family recovery（`PromptRecovery.reconcile`
  // → `findPhysical`）读子会话消息来证明 PromptClaim。旧 fixture 对所有 session 返回
  // 同一个空数组，子会话的认领永远 StillPending → join RECOVERY_BLOCKED
  // （`pending claim unknown ...`）。
  const messagesBySession = new Map() // sessionId -> message[]
  return {
    __pushHostMessage: (sessionId, message) => {
      const perSession = messagesBySession.get(sessionId) ?? []
      perSession.push(message)
      messagesBySession.set(sessionId, perSession)
      messages.push(message)
    },
    session: {
      create: async (args) => {
        counter += 1
        const id = `host-child-${counter}-${Math.random().toString(16).slice(2)}`
        createdIds.push(id)
        childSessions.push({
          id,
          parentID: args?.body?.parentID,
          agent: args?.body?.agent,
          title: args?.body?.title,
        })
        return { data: { id } }
      },
      children: async (args) => ({
        data: childSessions.filter((child) => child.parentID === args?.path?.id),
      }),
      messages: async (args) => {
        // SessionSnapshotPort.GetMessages payload: { path: { id }, query, headers }。
        const id = args?.path?.id ?? args?.sessionID ?? args?.sessionId
        const perSession = (id && messagesBySession.get(id)) || []
        // Legacy 数组：测试直接向 `runtime.messages` push（REVIEW_007 compaction
        // fixture）。只合并无 session 归属的消息——parent 仍能读到它们，子会话的
        // prompt 不会跨 session 泄漏。
        const orphaned = messages.filter(
          (m) => ![...messagesBySession.values()].some((list) => list.includes(m)),
        )
        return { data: [...perSession, ...orphaned] }
      },
      promptAsync: async (args) => {
        prompts.push(args)
        // 生产在 terminal 订阅安装之后才发 prompt（OneShotAgentTool.fs:115 → send），
        // 故此调用即「可以安全 NotifyTerminal」的就绪信号。
        // OpenCodePort payload: { path: { id }, body: { parts, agent?, model?, metadata? }, headers }。
        const sessionId = args?.path?.id ?? args?.sessionID ?? args?.sessionId ?? createdIds[createdIds.length - 1]
        // 合成 Host 物理消息（SessionSnapshotPort.projectMessage 可投影的形状），让
        // findPhysical 能证明认领。key 在 body.metadata 与 text part metadata 两侧
        // 都写（OpenCodePort 两处都落，PromptMetadataCodec.PromptKeyField）。
        const textPart = args?.body?.parts?.find((part) => part?.type === 'text')
        const key =
          args?.body?.metadata?.wanxiangshu_prompt_key ?? textPart?.metadata?.wanxiangshu_prompt_key
        const message = {
          id: `msg-${sessionId}-${messagesBySession.get(sessionId)?.length ?? 0}`,
          role: 'user',
          parts: [
            {
              type: 'text',
              text: textPart?.text ?? '',
              metadata: key ? { wanxiangshu_prompt_key: key } : undefined,
            },
          ],
          metadata: key ? { wanxiangshu_prompt_key: key } : undefined,
        }
        const perSession = messagesBySession.get(sessionId) ?? []
        perSession.push(message)
        messagesBySession.set(sessionId, perSession)
        messages.push(message)
        const waiter = promptedWaiters.get(sessionId)
        if (waiter) {
          promptedWaiters.delete(sessionId)
          waiter()
        } else {
          promptedWaiters.set(sessionId, null) // 已就绪：后来的 awaitPrompted 立即返回
        }
        return {}
      },
      delete: async () => ({}),
      abort: async (args) => {
        abortedIds.push(args?.path?.id)
        return {}
      },
    },
  }
}

/** 等待生产对某个子会话发出首个 prompt——即其 terminal 订阅已安装的就绪信号。 */
export const awaitPrompted = (sessionId) => {
  const state = promptedWaiters.get(sessionId)
  if (state === null) {
    promptedWaiters.delete(sessionId)
    return Promise.resolve()
  }
  return new Promise((resolve) => promptedWaiters.set(sessionId, resolve))
}

/** Observe real HostEventPort terminal subscriptions without a timer. */
export const observeTerminalSubscriptions = (runtime) => {
  const listeners = runtime?.terminalPort?.listeners
  if (!Array.isArray(listeners)) throw new Error('HostEventPort listener collection is unavailable')

  const observed = []
  let resolveNext
  const notify = (entry) => {
    if (resolveNext !== undefined) {
      const resolve = resolveNext
      resolveNext = undefined
      resolve(entry)
    } else {
      observed.push(entry)
    }
  }
  const proxy = new Proxy(listeners, {
    get(target, property, receiver) {
      if (property === 'push') {
        return (...entries) => {
          const length = Array.prototype.push.apply(target, entries)
          entries.forEach(notify)
          return length
        }
      }
      return Reflect.get(target, property, receiver)
    },
  })
  runtime.terminalPort.listeners = proxy

  return {
    next: () => {
      if (observed.length > 0) return Promise.resolve(observed.shift())
      if (resolveNext !== undefined) throw new Error('only one terminal subscription observation may be pending')
      return new Promise((resolve) => {
        resolveNext = resolve
      })
    },
    restore: () => {
      if (runtime.terminalPort.listeners === proxy) runtime.terminalPort.listeners = listeners
    },
  }
}

/**
 * A plugin instance whose tools can actually execute (EXEC-002, layer 3).
 *
 * `runtime` passed to the body is { journal, runtimeId, terminalPort,
 * recordFork } — the shared production instances, resolved AFTER
 * `initSpikePlugin` so `acquire` returns the already-registered entries rather
 * than booting second owners. `recordFork(parentSessionId, agentId,
 * childSessionId)` registers a forked handle so its terminal completion also
 * claims the EXEC-004 durable cell (see below).
 */
export const withExecutablePlugin = async (body, options = {}) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-plugin-exec-'))
  try {
    execFileSync('git', ['init', '--quiet', directory])
    const createdIds = []
    const abortedIds = []
    const prompts = []
    const messages = []
    const client = stubClient(createdIds, prompts, messages, abortedIds)
    const hooks = await initSpikePlugin({
      ...options,
      client,
      directory,
      events: { listen: () => () => {} },
    })
    let runtime
    try {
      const runtimePath = forWorkspace(directory)
      const port = bootPort(gitCommonDir(directory))
      // Fable uncurries openJournal to (runtimeId, processId, startedAt) => Result.
      const openJournal = async (runtimeId, processId, startedAt) => {
        const resumed = await port.ResumeOrCreate(runtimeId, processId, startedAt)
        if (resumed.tag !== 0) return resumed
        const [writer, , projection] = resumed.fields[0]
        return AgentJournalModule_createFromProjection(writer, projection)
      }
      const journalResult = await acquireJournal(runtimePath, process.pid, new Date(), openJournal)
      // Fable Result: tag 0 is Ok, and `fields` is the fields ARRAY — the
      // journal itself is fields[0].
      if (journalResult.tag !== 0) throw new Error(`journal acquire rejected: ${journalResult.fields?.[0]?.Reason}`)
      runtime = {
        journal: journalResult.fields[0],
        runtimeId: AgentJournalModule_runtimeId(journalResult.fields[0]).fields[0],
        terminalPort: acquireTerminalBus(runtimePath),
        // Filled in by forkHandle below; the recorder needs the parent session's
        // handle ids, which only a fork return value reveals.
        handles: new Map(),
        prompts,
        messages,
        abortedIds,
        pushHostMessage: client.__pushHostMessage,
        pendingTerminalRecords: new Set(),
      }
      // EXEC-004's completion cell is claimed by `HostForkRuntime.Complete`
      // (Session/HostForkRuntime.fs:121-145), which lives behind the private
      // ToolRuntimeScope — NOT on the SharedTerminalBus fan-out a fixture can
      // reach. Delivering a bare NotifyTerminal without it leaves the handle
      // Active, so join's retire is rejected (NotCompleted) and poisons the
      // journal. The fixture claims the cell through the same single writer
      // (HandleController.recordCompletion) on the same shared journal, from a
      // terminal listener keyed by the parent session id — byte-identical to
      // the fact production appends, ahead of join's retire.
      const recorderByParent = new Map()
      runtime.terminalPort.SubscribeTerminalListener((sessionIdUnion, outcome) => {
        const sessionId = sessionIdUnion.fields[0]
        for (const [parentSessionId, byChild] of recorderByParent) {
          const agentId = byChild.get(sessionId)
          if (agentId !== undefined) {
            // P0-RECOVERY-JOIN-001: mint JoinableCompletion via Domain proof only.
            // EXEC-009: body is the durable join payload; fixture writes a minimal
            // completed/failed blob so projection-first join can consume after restart.
            // outcome.tag 0 = Completed; other tags are proven Failed (not raw Aborted).
            if (typeof terminalEvidenceCompleted !== 'function' || typeof tryFromProvenTerminal !== 'function') {
              throw new Error('ChildRecovery TerminalEvidence / JoinableCompletion not exported')
            }
            const body =
              outcome.tag === 0
                ? JSON.stringify({
                    status: 'completed',
                    run_id: `run-${agentId}`,
                    work_record: 'fixture-work-record',
                    child_session_id: sessionId,
                    authority_root: '',
                    provider_run: '',
                    directory: '',
                  })
                : JSON.stringify({
                    status: 'failed',
                    run_id: `run-${agentId}`,
                    code: 'ERROR',
                    message: 'fixture terminal failure',
                    child_session_id: sessionId,
                  })
            const handle = agentHandle(agentId)
            const child = SessionIdModule_create(sessionId)
            const evidence =
              outcome.tag === 0
                ? terminalEvidenceCompleted(agentId, handle, child, body)
                : terminalEvidenceFailed(agentId, handle, child, body)
            const proof = tryFromProvenTerminal(evidence)
            if (proof.tag !== 0) {
              throw new Error(`JoinableCompletion(${agentId}) rejected: ${proof.fields?.[0]}`)
            }
            const recording = recordCompletion(runtime.journal, SessionIdModule_create(parentSessionId), proof.fields[0]).then((recorded) => {
              if (recorded.tag !== 0) {
                throw new Error(`HandleCompleted(${agentId}) rejected: ${recorded.fields?.[0]}`)
              }
              byChild.delete(sessionId)
            })
            runtime.pendingTerminalRecords.add(recording)
            recording.finally(() => runtime.pendingTerminalRecords.delete(recording))
          }
        }
      })
      runtime.recordFork = (parentSessionId, agentId, childSessionId) => {
        if (!recorderByParent.has(parentSessionId)) recorderByParent.set(parentSessionId, new Map())
        recorderByParent.get(parentSessionId).set(childSessionId, agentId)
      }
      await body(hooks, directory, createdIds, runtime)
    } finally {
      // Keep the fixture's extra journal reference alive while plugin disposal
      // drains detached HostFork cancellation. AbortSession is sequenced after
      // durable handle abandonment, so observing every created child aborted is
      // a deterministic teardown barrier before releasing the last writer ref.
      await hooks.dispose()
      if (runtime !== undefined) {
        for (let attempt = 0; attempt < 100 && !createdIds.every((id) => abortedIds.includes(id)); attempt += 1) {
          await new Promise((resolve) => setTimeout(resolve, 0))
        }
        releaseJournal(runtime.journal)
      }
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}

/**
 * Two or more real plugin incarnations over one Git-private journal and one
 * persistent Host double. Used for restart recovery contracts that cannot be
 * proved by rebuilding only an in-memory runtime object.
 */
export const withRestartablePlugin = async (body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-plugin-restart-'))
  const liveHooks = []
  try {
    execFileSync('git', ['init', '--quiet', directory])
    const createdIds = []
    const abortedIds = []
    const prompts = []
    const messages = []
    const client = stubClient(createdIds, prompts, messages, abortedIds)
    const start = async () => {
      const hooks = await initSpikePlugin({
        client,
        directory,
        events: { listen: () => () => {} },
      })
      liveHooks.push(hooks)
      return hooks
    }

    await body(start, directory, {
      createdIds,
      abortedIds,
      prompts,
      messages,
      pushHostMessage: client.__pushHostMessage,
    })
  } finally {
    for (const hooks of liveHooks.reverse()) await hooks.dispose()
    rmSync(directory, { recursive: true, force: true })
  }
}

/** AGENT-007 layer one: a durable HumanRoot names the session's managed agent. */
export const acceptAuthorityRoot = async (runtime, sessionId, agent) => {
  const result = await Runtime__AcceptHumanRoot(
    forJournal(runtime.journal),
    SessionIdModule_create(sessionId),
    PhysicalUserMessageIdModule_create(`root-${sessionId}`),
    agent,
  )
  if (result.tag !== 0) {
    throw new Error(`AcceptHumanRoot(${sessionId}, ${agent}) rejected: ${result.fields?.[0]}`)
  }
}

/**
 * Accept a pending AgentOwnerRoot claim on a child session so BusyAgentNudge
 * (reuse while run active) can resolve ActiveLogicalRun (PROMPT-005).
 * Call after fork create once promptAsync has recorded the PromptKey in metadata.
 * The agent comes from the pending claim (PromptDispatcher.fs:147-151), not from
 * a separate argument.
 */
export const acceptChildAgentOwnerRoot = async (runtime, childSessionId, promptKey) => {
  const result = await Runtime__AcceptAgentOwnerRoot(
    forJournal(runtime.journal),
    PromptKeyModule_create(promptKey),
    SessionIdModule_create(childSessionId),
    PhysicalUserMessageIdModule_create(`physical-${childSessionId}`),
  )
  if (result.tag !== 0) {
    throw new Error(
      `AcceptAgentOwnerRoot(${childSessionId}, ${promptKey}) rejected: ${result.fields?.[0]}`,
    )
  }
}

export const activateLife = async (runtime, sessionId) => {
  const sid = SessionIdModule_create(sessionId)
  const lifeId = ManagerLifeIdModule_create(`life-${sessionId}`)
  const stream = new StreamId(1, [sid])
  await AgentJournalModule_appendManagerLifecycle(
    stream,
    new ManagerLifecycleFact(0, [{
      LifeId: lifeId,
      OpeningCursorSequence: 0n,
      OpeningTextDigest: BlobDigestModule_create('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'),
      OpeningTextRef: BlobRefModule_create('blob-ref'),
      OpeningUserMessageId: PhysicalUserMessageIdModule_create(`root-${sessionId}`),
      SessionId: sid,
    }]),
    runtime.journal,
  )
  await AgentJournalModule_appendManagerLifecycle(
    stream,
    new ManagerLifecycleFact(1, [{
      ActivationPromptKey: PromptKeyModule_create(''),
      LifeId: lifeId,
      ProtectedPrefixEndSequence: 1n,
      SessionId: sid,
    }]),
    runtime.journal,
  )
}

/**
 * GLORY-037 / TODO-010: first unblessed suicide requires ≥1 TodoWriteAccepted.
 * `activateLife` only opens the Life; T1 is a separate Magic Todo fact pair.
 */
export const acceptFirstTodoWrite = async (runtime, sessionId) => {
  const sid = SessionIdModule_create(sessionId)
  const lifeId = ManagerLifeIdModule_create(`life-${sessionId}`)
  const stream = new StreamId(1, [sid])
  const call = ToolCallIdModule_create(`todo-t1-${sessionId}`)
  const write = TodoWriteIdModule_create(`todo-write-t1-${sessionId}`)
  const digest = 'digest:t1-input'
  const prepared = new TodoWritePrepared(
    sid,
    lifeId,
    write,
    call,
    0,
    BlobRefModule_create('blob-todo-base'),
    BlobDigestModule_create('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'),
    BlobRefModule_create('blob-todo-proposed'),
    BlobDigestModule_create('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'),
    digest,
    new XTraceCursor(0n),
    'magic-todo.v1',
  )
  const preparedResult = await AgentJournalModule_appendMagicTodo(
    stream,
    undefined,
    new MagicTodoFact(0, [prepared]),
    runtime.journal,
  )
  if (preparedResult.tag !== 0) {
    throw new Error(`TodoWritePrepared(${sessionId}) rejected: ${JSON.stringify(preparedResult.fields?.[0])}`)
  }
  const accepted = new TodoWriteAccepted(
    lifeId,
    write,
    call,
    preparedResult.fields[0].EventId,
    digest,
    'digest:t1-output',
    PhysicalSuccessEvidence.LiveAfterSuccess,
    'magic-todo.v1',
  )
  const acceptedResult = await AgentJournalModule_appendMagicTodo(
    stream,
    undefined,
    new MagicTodoFact(1, [accepted]),
    runtime.journal,
  )
  if (acceptedResult.tag !== 0) {
    throw new Error(`TodoWriteAccepted(${sessionId}) rejected: ${JSON.stringify(acceptedResult.fields?.[0])}`)
  }
}

/**
 * Deliver a real terminal completion for one child session, through the same
 * HostEventPort the plugin subscribed to.
 *
 * `sessionWideText` maps to AgentRunResult.TerminalText (EXEC-006 IsValid).
 * Join LLM wire (EXEC-004 rev.2) renders LWR as entry-local `#` comments before
 * each [[result]], not as a `work_record =` field. The durable blob still carries
 * work_record JSON for HandleCompletionCodec. Fixture completions without an
 * Opening capture yield an empty LWR → no comment block before [[result]].
 * `turnFormalText` is what a one-shot tool reports as output (COMPANION-005).
 *
 * AgentRunResult field order (Kernel/Outcome.fs): SessionId,
 * AuthorityRootUserMessageId, ProviderRun, Role, Directory, TerminalText,
 * TurnFormalText. `Role.Coder` is Fable union case tag 2 (Kernel/Roles.fs);
 * the runner pins it so a Role-order edit fails loudly instead of mislabeling.
 */
export const notifyCompleted = async (runtime, childSessionId, sessionWideText, turnFormalText, roleCaseTag = 2) => {
  runtime.terminalPort.NotifyTerminal(
    SessionIdModule_create(childSessionId),
    // Fable union: case tag 0 is `Completed`, and `fields` is the fields ARRAY —
    // `new TerminalOutcome(0, result)` would wrap the record as fields[0..n]=undefined.
    new TerminalOutcome(
      0,
      [
        new AgentRunResult(
          SessionIdModule_create(childSessionId),
          null,
          null,
          { tag: roleCaseTag, fields: [] },
          undefined,
          sessionWideText,
          turnFormalText,
        ),
      ],
    ),
  )
  await Promise.all([...runtime.pendingTerminalRecords])
}

/**
 * A plugin instance over a throwaway Git repo.
 *
 * The journal lives under the Git common directory (PERSIST-006), so `git init` is
 * what makes the runtime addressable at all. `events.listen` is the smallest port
 * that satisfies the signal source; no scenario, no HTTP, no mock provider.
 *
 * Measured cost per call: 275-331ms wall, almost entirely `git init` plus
 * `initSpikePlugin`. Callers under the 1000ms per-test bound (`runner.mjs:27`) must
 * count calls, not assertions.
 */
export const withPluginClient = async (client, body) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-plugin-'))
  try {
    execFileSync('git', ['init', '--quiet', directory])
    const hooks = await initSpikePlugin({
      client,
      directory,
      events: { listen: () => () => {} },
    })
    try {
      await body(hooks, directory)
    } finally {
      await hooks.dispose()
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}

export const withPlugin = async (body) => withPluginClient({}, body)

// ── Magic Todo Phase 0 Host V1 contract helpers (test-only) ──────────────────
//
// Mirror the OpenCode V1 tool-hook call shape used by:
//   packages/opencode/src/tool/registry.ts  (tool.definition)
//   packages/opencode/src/session/tools.ts  (execute.before → execute → after)
//   packages/opencode/src/tool/tool.ts      (original parameters decoder)
//   packages/opencode/src/tool/todo.ts      (V1 todowrite Parameters)
//
// These helpers do not import production membrane code. They only freeze the
// Host call contract that membrane implementations must satisfy.

import { Schema, Result } from 'effect'

/** V1 Todo.Info — content/status/priority only (no id/kind). */
export const V1_TODO_INFO = Schema.Struct({
  content: Schema.String,
  status: Schema.String,
  priority: Schema.String,
})

/** Original V1 todowrite parameters decoder (Host Tool.define wraps this). */
export const V1_TODOWRITE_PARAMETERS = Schema.Struct({
  todos: Schema.mutable(Schema.Array(V1_TODO_INFO)),
})

const decodeV1TodoWriteUnknown = Schema.decodeUnknownResult(V1_TODOWRITE_PARAMETERS)

/**
 * Run the original V1 todowrite decoder the way Host `Tool.define` does after
 * `tool.execute.before` returns. Returns `{ ok, value?, error? }`.
 */
export const decodeV1TodoWriteArgs = (args) => {
  const result = decodeV1TodoWriteUnknown(args)
  if (Result.isSuccess(result)) return { ok: true, value: result.success }
  return { ok: false, error: result.failure }
}

/**
 * Host `tool.definition` registry wrap (registry.ts):
 * builds `{ description, parameters, jsonSchema }`, triggers the hook with
 * positional `(input, output)`, then applies the ternary that drops jsonSchema
 * when only `parameters` identity changed.
 */
export const applyToolDefinitionHook = async (hook, tool) => {
  const output = {
    description: tool.description,
    parameters: tool.parameters,
    jsonSchema: tool.jsonSchema,
  }
  if (typeof hook === 'function') {
    await hook({ toolID: tool.id }, output)
  }
  const jsonSchema =
    output.parameters === tool.parameters || output.jsonSchema !== tool.jsonSchema
      ? output.jsonSchema
      : undefined
  return {
    id: tool.id,
    description: output.description,
    parameters: output.parameters,
    jsonSchema,
    // Host keeps the original execute wrapper — definition never replaces it.
    execute: tool.execute,
    originalParameters: tool.parameters,
  }
}

/**
 * Host V1 execute path (session/tools.ts):
 *   before(input, { args })  — mutates local args in place
 *   item.execute(args, ctx)  — original decoder+body; throw skips after
 *   after(input, output)     — only on success
 *
 * Returns observations needed by canaries A-precondition / B / C / F.
 */
export const runHostV1ToolExecutePath = async ({
  toolID,
  sessionID,
  callID,
  args,
  before,
  after,
  execute,
  decode = decodeV1TodoWriteArgs,
}) => {
  const beforeInput = { tool: toolID, sessionID, callID }
  const beforeOutput = { args }
  const argsIdentityBefore = args
  const argsSnapshotBefore = structuredClone(args)

  if (typeof before === 'function') {
    await before(beforeInput, beforeOutput)
  }

  // Host never rebinds the local `args` binding from `output.args = …`.
  // Only in-place field mutation on the original object reaches execute.
  const executorArgs = args
  const replacedArgsObject = beforeOutput.args !== argsIdentityBefore
  const decodeResult = decode(executorArgs)

  const observation = {
    argsIdentityUnchanged: executorArgs === argsIdentityBefore,
    replacedArgsObject,
    argsSnapshotBefore,
    argsAfterBefore: structuredClone(executorArgs),
    beforeOutputArgs: beforeOutput.args,
    decode: decodeResult,
    executorSawArgs: executorArgs,
    afterRan: false,
    afterOutput: undefined,
    executeThrew: false,
    executeError: undefined,
    executeResult: undefined,
  }

  if (!decodeResult.ok) {
    observation.executeThrew = true
    observation.executeError = decodeResult.error
    return observation
  }

  try {
    const result = await execute(decodeResult.value, {
      sessionID,
      callID,
      abort: new AbortController().signal,
    })
    observation.executeResult = result
    const afterOutput = {
      title: result?.title ?? '',
      output: result?.output ?? '',
      metadata: result?.metadata,
    }
    if (typeof after === 'function') {
      await after(
        { tool: toolID, sessionID, callID, args: executorArgs },
        afterOutput,
      )
      observation.afterRan = true
      observation.afterOutput = afterOutput
    }
  } catch (error) {
    observation.executeThrew = true
    observation.executeError = error
    // Host: after is only reached after item.execute succeeds.
  }

  return observation
}

/** Project clean-break obligations into the original Host V1 compatibility sink. */
export const projectObligationsToV1TodoRows = (args) => {
  if (!args || typeof args !== 'object' || !Array.isArray(args.obligations)) return args
  Object.defineProperty(args, 'todos', {
    value: args.obligations.map((obligation) => ({
      content: `${obligation.name}: ${obligation.work}`,
      status: 'in_progress',
      priority: 'medium',
    })),
    enumerable: false,
    configurable: true,
    writable: true,
  })
  return args
}

/** Baseline V1 todowrite tool record as registry seeds definition output. */
export const v1TodoWriteToolSeed = (overrides = {}) => ({
  id: 'todowrite',
  description:
    'Create and maintain a structured task list for the current coding session.',
  parameters: V1_TODOWRITE_PARAMETERS,
  jsonSchema: {
    type: 'object',
    properties: {
      todos: {
        type: 'array',
        items: {
          type: 'object',
          properties: {
            content: { type: 'string' },
            status: { type: 'string' },
            priority: { type: 'string' },
          },
          required: ['content', 'status', 'priority'],
        },
      },
    },
    required: ['todos'],
  },
  execute: async (params) => ({
    title: `${params.todos.filter((t) => t.status !== 'completed').length} todos`,
    output: JSON.stringify(params.todos, null, 2),
    metadata: { todos: params.todos },
  }),
  ...overrides,
})

/**
 * Positional Host trigger double — Effect.promise(() => fn(input, output)).
 * Used to prove registration call shape for definition / before / after.
 */
export const hostTrigger = async (fn, input, output) => {
  if (typeof fn !== 'function') return output
  await fn(input, output)
  return output
}

/** Sample clean-break provider obligation account before sink projection. */
export const sampleObligationTodoWriteArgs = () => ({
  obligations: [
    { name: 'membrane', work: 'implement the production membrane' },
    { name: 'canaries', work: 'write permanent contract canaries' },
  ],
})

/** Provider advertisement installed by tool.definition. */
export const sampleObligationTodoWriteAdvertisement = () => ({
  description: 'Replace the mission living obligation account with stable name/work pairs.',
  parameters: {
    type: 'object',
    additionalProperties: false,
    properties: {
      obligations: {
        type: 'array',
        items: {
          type: 'object',
          additionalProperties: false,
          properties: {
            name: { type: 'string', minLength: 1 },
            work: { type: 'string' },
          },
          required: ['name', 'work'],
        },
      },
    },
    required: ['obligations'],
  },
  jsonSchema: {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    type: 'object',
    additionalProperties: false,
    properties: {
      obligations: {
        type: 'array',
        items: {
          type: 'object',
          additionalProperties: false,
          properties: {
            name: { type: 'string', minLength: 1 },
            work: { type: 'string' },
          },
          required: ['name', 'work'],
        },
      },
    },
    required: ['obligations'],
  },
})

/**
 * Fixture-level Host trigger model used by isolated unit canaries.
 * Production SpikePlugin now wires the equivalent three-hook membrane.
 */
export const createMagicTodoContractHooks = () => {
  const advertisement = sampleObligationTodoWriteAdvertisement()
  return {
    'tool.definition': async (input, output) => {
      if (input.toolID !== 'todowrite') return
      output.description = advertisement.description
      output.parameters = advertisement.parameters
      output.jsonSchema = advertisement.jsonSchema
    },
    'tool.execute.before': async (input, output) => {
      if (input.tool !== 'todowrite') return
      // Host only honors in-place mutation of the original args object.
      projectObligationsToV1TodoRows(output.args)
    },
    'tool.execute.after': async (_input, _output) => {
      // Observation-only in Phase 0 unit canaries.
    },
  }
}
