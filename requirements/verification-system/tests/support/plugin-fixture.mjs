// tests/unit/Plugin/plugin-fixture.mjs — shared layer-2 plugin fixture (VERIFY-008).
//
// Deliberately NOT named `*.test.mjs`. `tests/unit/runner.mjs:98` discovers tests with
// `walk('tests/unit', ['.test.mjs'])`, so a helper carrying that suffix would be run as
// a test file (zero tests inside it, but its top-level import cost paid twice).
// `scripts/architecture-gate.mjs` scans `['.mjs']`, so this file is still gated.

import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

if (!process.env.WANXIANGSHU_PROVIDER_LANGUAGE || process.env.WANXIANGSHU_PROVIDER_LANGUAGE === 'undefined') {
  process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'
}

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

const { default: plugin } = await import('wanxiangshu')
const initSpikePlugin = plugin.server
const { requiredNames: managedAgentNames } = await import('../../../../dist/Participant/Persona/Surface.js')
const journalSurface = await import('../../../../dist/Persistence/Journal/Surface.js')
const eventsSurface = await import('../../../../dist/OpenCode/Host/EventsSurface.js')
const dispatchSurface = await import('../../../../dist/Interaction/Dispatch/DispatchSurface.js')
const obligationJournalSurface = await import('../../../../dist/Persistence/Journal/ObligationJournalSurface.js')
const sessionBindingSurface = await import('../../../../dist/OpenCode/Host/SessionBindingSurface.js')

const setupRoutingHome = (directory) => {
  const routingHome = join(directory, 'routing-home')
  const routingDir = join(routingHome, '.config', 'opencode')
  mkdirSync(routingDir, { recursive: true })
  writeFileSync(
    join(routingDir, 'wanxiangshu.mjs'),
    `export default function route(role, running) {
  if (!/^(fast|deep)-/.test(role)) throw new Error('unexpected managed role: ' + role)
  if (Array.isArray(globalThis.__wanxiangshu_test_routing_seen)) {
    globalThis.__wanxiangshu_test_routing_seen.push({ role, running: running.map((item) => ({ ...item })) })
  }
  return { model: 'provider/' + role + '-model', reasoning: 'none' }
}\n`,
    'utf8',
  )
  return routingHome
}

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
      get: async (args) => {
        const id = args?.path?.id ?? args?.sessionID ?? args?.sessionId
        const child = childSessions.find((candidate) => candidate.id === id)
        return { data: child ?? { id } }
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

/**
 * A plugin instance whose tools can actually execute (EXEC-002, layer 3).
 *
 * `runtime` passed to the body is { journal, runtimeId, terminalPort } — the
 * shared production instances, resolved AFTER `initSpikePlugin` so `acquire`
 * returns the already-registered entries rather than booting second owners.
 */
export const withExecutablePlugin = async (body, options = {}) => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-plugin-exec-'))
  const previousHome = process.env.HOME
  const previousUserProfile = process.env.USERPROFILE
  const routingHome = setupRoutingHome(directory)
  process.env.HOME = routingHome
  process.env.USERPROFILE = routingHome
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
    const hostConfig = {
      agent: Object.fromEntries(
        Array.from(managedAgentNames).map((name) => [name, { model: `fixture/${name}-model` }]),
      ),
    }
    hooks.config(hostConfig)
    let runtime
    try {
      const journalResult = await journalSurface.JournalSurface_acquireSharedForWorkspace(
        directory,
        process.pid,
        new Date().toISOString(),
      )
      if (!journalResult?.ok) throw new Error(`journal acquire rejected: ${journalResult?.error ?? 'unknown error'}`)
      const terminalPort = eventsSurface.acquireSharedForWorkspace(directory)
      if (terminalPort == null) {
        journalSurface.JournalSurface_dispose(journalResult.journal)
        throw new Error('shared terminal port is unavailable')
      }
      runtime = {
        journal: journalResult.journal,
        runtimeId: journalSurface.JournalSurface_runtimeId(journalResult.journal),
        terminalPort,
        prompts,
        messages,
        abortedIds,
        pushHostMessage: client.__pushHostMessage,
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
        try {
          eventsSurface.releaseSharedForWorkspace(directory, runtime.terminalPort)
        } finally {
          journalSurface.JournalSurface_dispose(runtime.journal)
        }
      }
    }
  } finally {
    if (previousHome === undefined) delete process.env.HOME
    else process.env.HOME = previousHome
    if (previousUserProfile === undefined) delete process.env.USERPROFILE
    else process.env.USERPROFILE = previousUserProfile
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
  const previousHome = process.env.HOME
  const previousUserProfile = process.env.USERPROFILE
  const routingHome = setupRoutingHome(directory)
  process.env.HOME = routingHome
  process.env.USERPROFILE = routingHome
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
    if (previousHome === undefined) delete process.env.HOME
    else process.env.HOME = previousHome
    if (previousUserProfile === undefined) delete process.env.USERPROFILE
    else process.env.USERPROFILE = previousUserProfile
    rmSync(directory, { recursive: true, force: true })
  }
}

/** AGENT-007 layer one: a durable HumanRoot names the session's managed agent. */
export const acceptAuthorityRoot = async (runtime, sessionId, agent) => {
  const result = await dispatchSurface.acceptHumanRoot(runtime.journal, sessionId, `root-${sessionId}`, agent)
  if (!result?.ok) {
    throw new Error(`AcceptHumanRoot(${sessionId}, ${agent}) rejected: ${result?.error ?? 'unknown error'}`)
  }
}

/** Bind a managed child session to a parent session in local execution binding. */
export const bindManagedChild = (parentId, childId, agent) => {
  return sessionBindingSurface.bindChild(parentId, childId, agent)
}

/**
 * Accept a pending AgentOwnerRoot claim on a child session so BusyAgentNudge
 * (reuse while run active) can resolve ActiveLogicalRun (PROMPT-005).
 * Call after fork create once promptAsync has recorded the PromptKey in metadata.
 * The agent comes from the pending claim (PromptDispatcher.fs:147-151), not from
 * a separate argument.
 */
export const acceptChildAgentOwnerRoot = async (runtime, childSessionId, promptKey) => {
  const result = await dispatchSurface.acceptAgentOwnerRoot(
    runtime.journal,
    childSessionId,
    promptKey,
    `physical-${childSessionId}`,
  )
  if (!result?.ok) {
    throw new Error(
      `AcceptAgentOwnerRoot(${childSessionId}, ${promptKey}) rejected: ${result?.error ?? 'unknown error'}`,
    )
  }
}

export const activateLife = async (runtime, sessionId) => {
  const lifeId = `life-${sessionId}`
  const opened = await obligationJournalSurface.appendManagerLifecycle(
    runtime.journal,
    sessionId,
    'LifeOpened',
    {
      sessionId,
      lifeId,
      openingCursorSequence: 0,
      openingTextDigest: 'digest-opening',
      openingTextRef: 'blob-ref',
      openingUserMessageId: `root-${sessionId}`,
    },
  )
  if (!opened?.ok) throw new Error(`LifeOpened(${sessionId}) rejected: ${opened?.error ?? 'unknown error'}`)
  const activated = await obligationJournalSurface.appendManagerLifecycle(
    runtime.journal,
    sessionId,
    'WorkActivated',
    {
      sessionId,
      lifeId,
      activationPromptKey: '',
      protectedPrefixEndSequence: 1,
    },
  )
  if (!activated?.ok) {
    throw new Error(`WorkActivated(${sessionId}) rejected: ${activated?.error ?? 'unknown error'}`)
  }
}

/**
 * GLORY-037 / TODO-010: first unblessed suicide requires ≥1 TodoWriteAccepted.
 * `activateLife` only opens the Life; T1 is a separate Magic Todo fact pair.
 */
export const acceptFirstTodoWrite = async (runtime, sessionId) => {
  const lifeId = `life-${sessionId}`
  const callId = `todo-t1-${sessionId}`
  const writeId = `todo-write-t1-${sessionId}`
  const inputDigest = 'digest:t1-input'
  const prepared = await obligationJournalSurface.appendMagicTodo(
    runtime.journal,
    sessionId,
    null,
    JSON.stringify({
      case: 'TodoWritePrepared',
      ManagerSessionId: sessionId,
      ManagerLifeId: lifeId,
      TodoWriteId: writeId,
      ToolCallId: callId,
      ToolPartOrdinal: 0,
      BaseTodoRef: 'blob-todo-base',
      BaseTodoDigest: 'digest-todo-base',
      ProposedTodoRef: 'blob-todo-proposed',
      ProposedTodoDigest: 'digest-todo-proposed',
      PlanCompleteDeclared: true,
      ProviderInputDigest: inputDigest,
      ReviewFrontier: { Sequence: 0 },
      SemanticVersion: 'magic-todo.v1',
    }),
  )
  if (!prepared?.ok) {
    throw new Error(`TodoWritePrepared(${sessionId}) rejected: ${prepared?.error ?? 'unknown error'}`)
  }
  const accepted = await obligationJournalSurface.appendMagicTodo(
    runtime.journal,
    sessionId,
    null,
    JSON.stringify({
      case: 'TodoWriteAccepted',
      ManagerLifeId: lifeId,
      TodoWriteId: writeId,
      ToolCallId: callId,
      PreparedFactRef: prepared.eventId,
      InputDigest: inputDigest,
      OutputDigest: 'digest:t1-output',
      PhysicalSuccessEvidence: 'LiveAfterSuccess',
      SemanticVersion: 'magic-todo.v1',
    }),
  )
  if (!accepted?.ok) {
    throw new Error(`TodoWriteAccepted(${sessionId}) rejected: ${accepted?.error ?? 'unknown error'}`)
  }
}

/**
 * Deliver a real terminal completion for one child session, through the same
 * opaque Host event capability the plugin subscribed to.
 *
 * `sessionWideText` maps to AgentRunResult.TerminalText (EXEC-006 IsValid).
 * Join LLM wire (EXEC-004 rev.2) renders LWR as entry-local `#` comments before
 * each [[result]], not as a `work_record =` field. The durable blob still carries
 * work_record JSON for HandleCompletionCodec. Fixture completions without an
 * Opening capture yield an empty LWR → no comment block before [[result]].
 * `turnFormalText` is what a one-shot tool reports as output (COMPANION-005).
 */
export const notifyCompleted = async (runtime, childSessionId, sessionWideText, turnFormalText, roleCaseTag = 2) => {
  const roleLabels = ['manager', 'orchestrator', 'coder', 'inspector', 'browser', 'inquiry', 'reviewer', 'devops', 'distiller', 'blogger']
  const role = roleLabels[roleCaseTag] ?? 'coder'
  await eventsSurface.notifyCompleted(runtime.terminalPort, childSessionId, sessionWideText, turnFormalText, role)
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
  const previousHome = process.env.HOME
  const previousUserProfile = process.env.USERPROFILE
  const routingHome = setupRoutingHome(directory)
  process.env.HOME = routingHome
  process.env.USERPROFILE = routingHome

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
    if (previousHome === undefined) delete process.env.HOME
    else process.env.HOME = previousHome
    if (previousUserProfile === undefined) delete process.env.USERPROFILE
    else process.env.USERPROFILE = previousUserProfile
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
      status: obligation.name === args.workingOn ? 'in_progress' : 'pending',
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
  planComplete: true,
  workingOn: 'membrane',
  obligations: [
    { name: 'membrane', horizon: 'near', work: 'implement the production membrane' },
    { name: 'canaries', horizon: 'far', work: 'write permanent contract canaries' },
  ],
})

/** Provider advertisement installed by tool.definition. */
export const sampleObligationTodoWriteAdvertisement = () => ({
  description: 'Replace the current owed-work account and declare whether the plan is complete.',
  parameters: {
    type: 'object',
    additionalProperties: false,
    properties: {
      planComplete: { type: 'boolean' },
      workingOn: { type: 'string' },
      obligations: {
        type: 'array',
        items: {
          type: 'object',
          additionalProperties: false,
          properties: {
            name: { type: 'string', minLength: 1 },
            horizon: { type: 'string', enum: ['near', 'mid', 'far'] },
            work: { type: 'string' },
          },
          required: ['name', 'horizon', 'work'],
        },
      },
    },
    required: ['planComplete', 'workingOn', 'obligations'],
  },
  jsonSchema: {
    $schema: 'https://json-schema.org/draft/2020-12/schema',
    type: 'object',
    additionalProperties: false,
    properties: {
      planComplete: { type: 'boolean' },
      workingOn: { type: 'string' },
      obligations: {
        type: 'array',
        items: {
          type: 'object',
          additionalProperties: false,
          properties: {
            name: { type: 'string', minLength: 1 },
            horizon: { type: 'string', enum: ['near', 'mid', 'far'] },
            work: { type: 'string' },
          },
          required: ['name', 'horizon', 'work'],
        },
      },
    },
    required: ['planComplete', 'workingOn', 'obligations'],
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
