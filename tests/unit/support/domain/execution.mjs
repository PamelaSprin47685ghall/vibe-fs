// tests/unit/support/domain/execution.mjs — execution family.
// Fork child payload, TDD phase, executor summarize, blogger TOML/delta,
// terminal validity, process estimate/request, bounded parallelism, causal wait,
// task/token sources, process wait, fork runtime.

import {
  ForkChildPayloadModule,
  BloggerTomlModule,
  BloggerDeltaModule,
  Distillation,
  TerminalValidity,
  ProcessRequest,
  FlowModule,
  FableTask,
  AsyncBuilder,
  FableTypes,
  NodeProcessWaitModule,
  NodeProcessHostModule,
  ForkRuntimeModule,
  ForkTypesModule,
  ProviderProj,
  RolesModule,
  unionCase,
  bind,
  member,
  caseOf,
  resultOf,
  toList,
  listItems,
  unwrapOption,
  isNone,
  okResult,
  errorResult,
  fableInstanceMethod,
  prod,
} from './interop.mjs'
import { syntheticToml } from './context.mjs'

// ── failure-driven context recovery (docs/what/context.md) ────────────────────────────────

/**
 * ARCH-010 / REVIEW-002: what a newly forked child is told, as one payload.
 *
 * `render` takes named fields rather than positional arguments: the record's three fields are all
 * strings or string collections, so a positional call cannot be read for correctness.
 */
export const forkChildPayload = (() => {
  const m = bind(ForkChildPayloadModule, 'ForkChildPayload', [
    'BaseInstructions',
    'CommissionerRecordInstruction',
    'RequirementsInstruction',
    'render',
    'relay',
  ])

  return {
    baseInstructions: listItems(m.BaseInstructions),
    commissionerRecordInstruction: m.CommissionerRecordInstruction,
    /** @deprecated use commissionerRecordInstruction */
    parentWorkRecordInstruction: m.CommissionerRecordInstruction,
    requirementsInstruction: m.RequirementsInstruction,

    render: ({ assignment, commissionerRecord, parentWorkRecord, originalUserRequirements = [], rootRequirements, payload }) =>
      m.render(
        new ForkChildPayloadModule.ForkChildAssignment(
          assignment,
          commissionerRecord ?? parentWorkRecord ?? undefined,
          toList(rootRequirements ?? originalUserRequirements),
          payload,
        ),
      ),

    relay: (assignment, commissionerRecord, requirements = [], payload) =>
      m.relay(assignment, commissionerRecord, toList(requirements), payload),
  }
})()

/**
 * Distillation map/reduce: natural-language assignment prompts (no chunk index in wire).
 */
export const distillation = (() => {
  const m = bind(Distillation, 'Distillation', [
    'distillFragmentPrompt',
    'mergeDistillationsPrompt',
  ])

  return {
    distillFragmentPrompt: () => m.distillFragmentPrompt(),
    mergeDistillationsPrompt: () => m.mergeDistillationsPrompt(),
  }
})()

/**
 * CTX-013: the deterministic TOML wire form of a Blogger delta.
 *
 * `part()` builds a `BloggerDeltaPart` by case NAME. The union has six cases whose
 * payloads are structurally similar (`TextPart` and `ReasoningPart` are both one
 * string), so constructing by ordinal would silently relabel prose as reasoning and
 * every rendered document would still be valid TOML.
 */
export const bloggerToml = (() => {
  const m = bind(BloggerTomlModule, 'BloggerToml', [
    'TruncationMarker',
    'DoNotExecTable',
    'NewWorkTable',
    'renderItem',
    'renderHistoricFrame',
    'renderPreviousEnforcerTip',
    'renderWith',
    'render',
  ])
  const buildPart = unionCase(BloggerTomlModule.BloggerDeltaPart, 'BloggerDeltaPart')

  const part = (kind, ...fields) => buildPart(kind, fields)

  return {
    truncationMarker: m.TruncationMarker,
    doNotExecTable: m.DoNotExecTable,
    newWorkTable: m.NewWorkTable,
    renderItem: (item) => m.renderItem(item),
    renderHistoricFrame: (body) => m.renderHistoricFrame(body),
    /** ENFORCER-071: one previous_enforcer_tip do_not_exec block. */
    renderPreviousEnforcerTip: (tipField, cycleId) => m.renderPreviousEnforcerTip(tipField, cycleId),
    renderWith: (instructions, items) => m.renderWith(toList(instructions), toList(items)),
    render: (items) => m.render(toList(items)),

    text: (value) => part('TextPart', value),
    reasoning: (value) => part('ReasoningPart', value),
    toolCall: (tool, args) => part('ToolCallPart', tool, args),
    toolResult: (value) => part('ToolResultPart', value),
    imageOmitted: (mediaType) => part('ImageOmitted', mediaType),
    mediaOmitted: (mediaType) => part('MediaOmitted', mediaType),

    item: ({ role = 'user', part: p, truncated = false }) => ({
      Role: role,
      Part: p,
      Truncated: truncated,
    }),

    kindOf: (item) => caseOf(item.Part),
  }
})()

/**
 * CTX-003 / CTX-011 / CTX-013: the three-level chunker.
 *
 * `messages` takes plain JS objects and converts the nested lists once. A raw array
 * where F# expects a `list` reports itself EMPTY rather than throwing, so a test
 * that skipped this would chunk nothing and assert successfully.
 */
export const bloggerDelta = (() => {
  const m = bind(BloggerDeltaModule, 'BloggerDelta', ['DeltaLimitBytes', 'nextChunk'])
  const semanticPart = unionCase(ProviderProj.SemanticPart, 'SemanticPart')

  const part = (kind, ...fields) => semanticPart(kind, fields)

  return {
    limitBytes: m.DeltaLimitBytes,

    text: (value) => part('SemanticText', value),
    reasoning: (value) => part('SemanticReasoning', value),
    toolCall: (name, args) => part('SemanticToolCall', name, args),
    toolResult: (value) => part('SemanticToolResult', value),
    media: (mediaType, digest) => part('SemanticMedia', mediaType, digest),

    /** `[{ role, parts: [...] }]` → the F# list-of-lists shape. */
    messages: (turns) => toList(turns.map((turn) => ({ Role: turn.role, Parts: toList(turn.parts) }))),

    cursor: (turn, part) => ({ TurnIndex: turn, PartIndex: part }),

    /** `undefined` when nothing is left to consume. */
    nextChunk: ({ limit, cursor, previousCutoff = 0, messages }) => {
      const chunk = unwrapOption(m.nextChunk(limit, cursor, previousCutoff, messages))
      if (isNone(chunk)) return undefined

      return {
        toml: chunk.Toml,
        bytes: syntheticToml.byteCount(chunk.Toml),
        itemCount: listItems(chunk.Items).length,
        kinds: listItems(chunk.Items).map((item) => caseOf(item.Part)),
        truncatedFlags: listItems(chunk.Items).map((item) => item.Truncated),
        nextCursor: { turn: chunk.NextCursor.TurnIndex, part: chunk.NextCursor.PartIndex },
        nextCutoff: chunk.NextCoverableTurnCutoffExclusive,
      }
    },
  }
})()

/** CTX-004: the one content-level validity check. */
export const terminalValidity = {
  isValid: (text) => TerminalValidity.isValid(text),

  /**
   * `{ ok: true }` or `{ ok: false, error: 'Empty' | 'XmlOnly' }`.
   *
   * The success case carries no value on purpose: F# returns `Result<unit, _>`, and
   * Fable erases `unit` to `undefined`. Exposing it would invite assertions on a
   * meaningless payload that happens to compare equal to a missing field.
   */
  check: (text) => {
    const result = resultOf(TerminalValidity.check(text))
    return result.ok ? { ok: true } : { ok: false, error: caseOf(result.error) }
  },

  describe: (rejectionName) => TerminalValidity.describe(unionCase(TerminalValidity.Rejection, 'Rejection')(rejectionName, [])),
}


/** ForkRuntime: AwaitAgent deadline + CancelAgent surface. */
export const forkRuntime = (() => {
  const Runtime = ForkRuntimeModule.ForkRuntime
  if (Runtime === undefined) {
    throw new Error('Session/ForkRuntime did not export ForkRuntime')
  }
  const AgentRole = ForkTypesModule.AgentRole ?? RolesModule.Role
  if (AgentRole === undefined) {
    throw new Error('Session/ForkTypes.AgentRole or Kernel/Roles.Role missing')
  }

  const forkFn = fableInstanceMethod(ForkRuntimeModule, 'ForkRuntime', 'Fork')
  const awaitFn = fableInstanceMethod(ForkRuntimeModule, 'ForkRuntime', 'AwaitAgent')
  const cancelFn = fableInstanceMethod(ForkRuntimeModule, 'ForkRuntime', 'CancelAgent')
  const joinFn = fableInstanceMethod(ForkRuntimeModule, 'ForkRuntime', 'Join')

  const roleOf = (name) => {
    const value = AgentRole[name]
    if (value === undefined) throw new Error(`unknown Role '${name}'`)
    return value
  }

  return {
    role: roleOf,
    /**
     * `runner` is uncurried `(agentId, role, prompt) => Promise<AgentCompletionOutcome>`.
     * Omit for default instant-ok runner.
     */
    // GREEN-5: ForkRuntime(runner, listener, cleanup) — no publishToMailbox flag.
    create: (runner) => new Runtime(runner, undefined, undefined),
    fork: (rt, agentId, role, agentName, prompt) => forkFn(rt, agentId, role, agentName, prompt, undefined),
    awaitAgent: (rt, agentId, timeoutMs) => awaitFn(rt, agentId, timeoutMs),
    cancelAgent: (rt, agentId) => cancelFn(rt, agentId),
    join: (rt, timeoutMs) => joinFn(rt, timeoutMs),
  }
})()

/**
 * Distillation map/reduce: distillSpool cancels owned children on failure.
 * Fake IDistillationRuntime: Fork / JoinWithPermit / AwaitAgentWithPermit / CancelAgent.
 * Permit-gated in production (requirePermit → HostForkRuntime).
 */
export const distillationRuntime = (() => {
  const distillSpool = member(Distillation, 'Distillation', 'distillSpool')
  const ForkResult = ForkTypesModule.ForkResult
  const ForkError = ForkTypesModule.ForkError
  if (ForkResult === undefined || ForkError === undefined) {
    throw new Error('ForkTypes ForkResult/ForkError missing')
  }

  return {
    distillSpool: (runtime, spoolPath) => distillSpool(runtime, spoolPath),
    /** Ok(ForkResult.Created agentId) */
    forkOk: (agentId) => okResult(new ForkResult(0, [agentId])),
    timedOut: () => errorResult(ForkError.TimedOut),
    /** Hard fail: FamilyBlocked / real join timeout → ForkError.NotFound (no Waiting retry). */
    notFound: (agentId = 'missing') => errorResult(new ForkError(4, [agentId])),
    /**
     * Fake IDistillationRuntime. JoinWithPermit / AwaitAgentWithPermit return Promise of Result.
     * Default → TimedOut so await fails after fork.
     */
    fake: ({ fork, join, awaitAgent, awaitRecoveryReadiness, cancel } = {}) => {
      const cancelled = []
      let lastAwaitedAgent
      const joinOrAwait = (timeoutMs, agentId) => {
        if (typeof awaitAgent === 'function' && agentId !== undefined) {
          return awaitAgent(agentId, timeoutMs)
        }
        if (typeof join === 'function') {
          return join(timeoutMs, agentId)
        }
        return errorResult(ForkError.TimedOut)
      }
      const runtime = {
        Fork: (agentId, _role, _prompt, _payload) =>
          Promise.resolve(typeof fork === 'function' ? fork(agentId) : okResult(new ForkResult(0, [agentId]))),
        JoinWithPermit: (timeoutMs) => Promise.resolve(joinOrAwait(timeoutMs)),
        AwaitAgentWithPermit: (agentId, timeoutMs) => {
          lastAwaitedAgent = agentId
          return Promise.resolve(joinOrAwait(timeoutMs, agentId))
        },
        CurrentJournalRevision: () => 0,
        AwaitJournalChangeFrom: (_fromRevision) =>
          Promise.resolve(
            typeof awaitRecoveryReadiness === 'function' ? awaitRecoveryReadiness(lastAwaitedAgent) : undefined,
          ),
        CancelAgent: (agentId) => {
          cancelled.push(agentId)
          if (typeof cancel === 'function') cancel(agentId)
        },
      }
      return { runtime, cancelled }
    },
  }
})()

/** EXEC-011: `min(3 × estimate, administrator ceiling)`. */
export const processEstimate = (() => {
  const m = bind(ProcessRequest, 'ProcessEstimate', ['DefaultHardLimit', 'effectiveDeadline', 'outputThreshold'])
  const runtimeSecondsOf = unionCase(ProcessRequest.EstimatedRuntime, 'EstimatedRuntime')
  const outputBytesOf = unionCase(ProcessRequest.EstimatedOutput, 'EstimatedOutput')

  return {
    defaultHardLimitMs: m.DefaultHardLimit,
    effectiveDeadlineMs: (runtimeSeconds, hardLimitMs) =>
      m.effectiveDeadline(runtimeSecondsOf('RuntimeSeconds', [runtimeSeconds]), hardLimitMs),
    outputThreshold: (bytes) => m.outputThreshold(outputBytesOf('OutputBytes', [BigInt(bytes)])),
  }
})()

/** EXEC-010: the stable fields of one process request. */
export const processRequest = {
  command: ({ fileName, args = [], workingDirectory, stdin }) =>
    new ProcessRequest.Command(fileName, toList(args), workingDirectory, undefined, stdin, undefined, undefined),
  estimate: ({ runtimeSeconds, outputBytes, memory }) =>
    new ProcessRequest.ProcessEstimate(
      new ProcessRequest.EstimatedRuntime(runtimeSeconds),
      new ProcessRequest.EstimatedOutput(BigInt(outputBytes)),
      (memory === 'Large') ? ProcessRequest.EstimatedMemory.Large : ProcessRequest.EstimatedMemory.Medium,
    ),
}

// ── bounded parallelism (ARCH-008, VERIFY-004) ───────────────────────────────

/**
 * `Parallel.mapBounded`, the ONE concurrency primitive production uses.
 *
 * `action` is passed as an UNCURRIED `(item, ct) => Promise`. Fable compiled the
 * two-parameter F# function to a two-argument JS one, so the curried spelling
 * `(item) => (ct) => ...` fails with `computation(...).finally is not a function`
 * — the builder receives a function where it expects a task. That error surfaces
 * only at await time, which is why the shape is fixed here rather than at each
 * call site.
 */
export const parallel = {
  mapBounded: async (maxConcurrency, action, items, cancellation = liveToken()) =>
    listItems(await FlowModule.Parallel_mapBounded(maxConcurrency, cancellation, action, items)),
}

// ── causal wait diagnostics (DSL-012) ────────────────────────────────────────
// JoinAttemptRegistry is re-exported as the class from dist; CausalWaitRegistry
// follows the same shape. Construction helpers absorb Fable list/union spellings.

const CausalWaitModule = await prod('Kernel/CausalWait')
const CausalWaitRegistryModule = await prod('Session/CausalWaitRegistry')
const CausalAwaitModule = await prod('Session/CausalAwait')

const buildCausalProducer = unionCase(CausalWaitModule.CausalProducerRef, 'CausalProducerRef')
const buildWaitEscape = unionCase(CausalWaitModule.WaitEscape, 'WaitEscape')

/** DSL-012 descriptor builders + pure frontier algorithm. */
export const causalWait = {
  owner: (kind, identity = []) => CausalWaitModule.CausalOwner_create(kind, toList(identity)),
  ownerKey: (owner) => CausalWaitModule.CausalOwner_key(owner),
  workflowProducer: (owner) => buildCausalProducer('WorkflowProducer', [owner]),
  externalProducer: (kind, identity = []) =>
    buildCausalProducer('ExternalProducer', [kind, toList(identity)]),
  producerKey: (producer) => CausalWaitModule.CausalProducer_key(producer),
  escape: {
    processLifetime: () => CausalWaitModule.WaitEscape.ProcessLifetime,
    sessionLifetime: () => CausalWaitModule.WaitEscape.SessionLifetime,
    openEndedExternal: () => CausalWaitModule.WaitEscape.OpenEndedExternal,
    cancelledBy: (owner) => buildWaitEscape('CancelledBy', [owner]),
  },
  exit: {
    resolved: () => CausalWaitModule.DiagnosticWaitExit.WaitResolved,
    failed: () => CausalWaitModule.DiagnosticWaitExit.WaitFailed,
    cancelled: () => CausalWaitModule.DiagnosticWaitExit.WaitCancelled,
    timedOut: () => CausalWaitModule.DiagnosticWaitExit.WaitTimedOut,
    disposed: () => CausalWaitModule.DiagnosticWaitExit.WaitDisposed,
  },
  create: ({
    waitKind,
    owner,
    subject = [],
    producer,
    escapes = [CausalWaitModule.WaitEscape.OpenEndedExternal],
    source = 'test',
  }) =>
    CausalWaitModule.DiagnosticWaitModule_create(
      waitKind,
      owner,
      toList(subject),
      producer,
      toList(escapes),
      source,
    ),
  snapshotOf: ({ active = [], history = [], sequence = 0n } = {}) =>
    new CausalWaitModule.DiagnosticWaitSnapshot(toList(active), toList(history), sequence),
  frontiersOf: (snapshot) => listItems(CausalWaitModule.CausalFrontierModule_ofSnapshot(snapshot)),
}

/** Process-local registry class (mirror of JoinAttemptRegistry dist re-export). */
export const CausalWaitRegistry = CausalWaitRegistryModule.CausalWaitRegistry

/**
 * Process-local hub. Application code holds `observer` (Enter only);
 * diagnostics read via `reader` / `snapshot` / `frontiers`.
 */
export const causalWaitHub = {
  observer: CausalWaitRegistryModule.CausalWaitHub_observer,
  reader: CausalWaitRegistryModule.CausalWaitHub_reader,
  snapshot: () => CausalWaitRegistryModule.CausalWaitHub_snapshot(),
  frontiers: () => listItems(CausalWaitRegistryModule.CausalWaitHub_frontiers()),
}

/** Bracket helpers: register a diagnostic wait around a real Task. */
export const causalAwait = {
  awaitTask: (observer, descriptor, pending) =>
    CausalAwaitModule.awaitTask(observer, descriptor, pending),
  awaitUnit: (observer, descriptor, pending) =>
    CausalAwaitModule.awaitUnit(observer, descriptor, pending),
  race: (observer, descriptor, primary, escape) =>
    CausalAwaitModule.race(observer, descriptor, primary, escape),
  /** G4R-CE S1: tryRead → race signal OR one IDeadlineHandle (no slice poll). */
  untilSignalOrDeadline: (observer, descriptor, deadline, tryRead, awaitSignal) =>
    CausalAwaitModule.untilSignalOrDeadline(observer, descriptor, deadline, tryRead, awaitSignal),
}

/**
 * Pending Fable Task via TaskCompletionSource (same surface as processWait /
 * join completion tests). `task()` is Promise-shaped under Fable.
 */
export const taskSource = () => {
  const tcs = new FableTask.TaskCompletionSource()
  return {
    task: () => tcs.get_Task(),
    resolve: (value) => tcs.SetResult(value),
    reject: (error) => tcs.SetException(error),
    cancel: () => tcs.SetCancelled(),
  }
}

/** A `CancellationToken`. `cancelled()` is already-cancelled at construction. */
export const liveToken = () => new AsyncBuilder.CancellationToken(false)
export const cancelledToken = () => new AsyncBuilder.CancellationToken(true)

/**
 * EXEC-011 process wait surface. Mock ChildProcess only — never touches real OS spawn.
 *
 * Fable shapes absorbed here: ChildProcess record fields, FSharpRef.contents for Exited,
 * TaskCompletionSource.get_Task / SetResult, OnExited as a JS array (ResizeArray).
 */
export const processWait = (() => {
  const waitForExitFn = NodeProcessWaitModule.waitForExit
  if (typeof waitForExitFn !== 'function') {
    throw new Error('NodeProcessWait.waitForExit missing from dist — run npm run build')
  }
  const notifyExitedFn = NodeProcessHostModule.notifyExited
  if (typeof notifyExitedFn !== 'function') {
    throw new Error('NodeProcessHost.notifyExited missing from dist — run npm run build')
  }
  const ChildProcess = NodeProcessHostModule.ChildProcess
  if (typeof ChildProcess !== 'function') {
    throw new Error('NodeProcessHost.ChildProcess missing from dist — run npm run build')
  }

  return {
    killAckGraceMs: NodeProcessWaitModule.KillAckGraceMs,
    /** Business wait entry: ChildProcess → Deadline → CancellationToken → Promise<WaitOutcome>. */
    waitForExit: (child, dl, ct) => waitForExitFn(child, dl, ct),
    /**
     * In-memory child: Kill is a counter; exit is explicit via `exit(code)`.
     * Optional `onKill` runs after each Kill (e.g. schedule a delayed real exit).
     */
    mockChild: ({ onKill } = {}) => {
      const exitTcs = new FableTask.TaskCompletionSource()
      const exited = new FableTypes.FSharpRef(false)
      const onExited = []
      let killCount = 0
      const child = new ChildProcess(
        null,
        exitTcs,
        () => {
          killCount += 1
          if (typeof onKill === 'function') onKill()
        },
        exited,
        onExited,
      )
      return {
        child,
        killCount: () => killCount,
        /** Mark exited + complete Exit.Task + fire OnExited waiters (same order as Host). */
        exit: (code) => {
          exited.contents = true
          exitTcs.SetResult(code | 0)
          notifyExitedFn(child)
        },
      }
    },
  }
})()