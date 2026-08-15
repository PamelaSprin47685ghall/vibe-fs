// tests/unit/support/domain/orchestrator.mjs — orchestrator + join family.
// Join drain, orchestrator projection/runtime, completion mailbox, host event
// port, reconcile supervisor, orchestrator/join/reconcile programs, verdict
// mailbox, agent completion, join result renderer.

import {
  JoinDrainModule,
  OrchestratorProj,
  OrchestratorRuntime,
  OrchestratorTypes,
  CompletionMailboxModule,
  AgentCompletionModuleEarly,
  EventsModule,
  ReconcileSupervisorModule,
  TurnBindingModule,
  SessionSnapshotPortModule,
  HostMessageCodecModule,
  ForkTypesModule,
  RolesModule,
  unionCase,
  bind,
  member,
  caseOf,
  payloadOf,
  resultOf,
  okResult,
  errorResult,
  toList,
  listItems,
  unwrapOption,
  utcOffset,
  fableInstanceMethod,
  prod,
} from './interop.mjs'
import { managerJobId, commitHash, worktreePath, targetRef, idValue } from './identity.mjs'
import { fold } from './journal.mjs'
import { providerLanguage } from './prompt.mjs'

/**
 * Canonical agentId of a RunCompletion: read from Outcome payload
 * (AgentCompleted/Failed payload.AgentId; AgentAbandoned tuple head).
 */
export const agentIdOf = (completion) => {
  const outcome = caseOf(completion.Outcome)
  const payload = payloadOf(completion.Outcome)
  if (outcome === 'AgentAbandoned') return payload[0]
  return payload.AgentId
}

/**
 * EXEC-009 + EXEC-018 pure durable join drain (JoinDrain.fs).
 * HostForkRuntime.tryDrainAvailable → JoinDrain.drainFromJournal.
 * Tests drive this path — never re-implement sort or hand-build batches around drain.
 */
export const joinDrain = (() => {
  const m = bind(JoinDrainModule, 'JoinDrain', [
    'stableJoinKey',
    'orderedCandidates',
    'drainFromJournal',
    'reconcileFalseAborts',
    'tryMigrateRetiredFalseAbort',
  ])

  const completionView = (c) => {
    const outcome = caseOf(c.Outcome)
    const payload = payloadOf(c.Outcome)
    return {
      runId: c.RunId,
      agentId: agentIdOf(c),
      agentName: c.AgentName,
      status:
        outcome === 'AgentCompleted'
          ? 'completed'
          : outcome === 'AgentFailed'
            ? 'failed'
            : outcome === 'AgentAbandoned'
              ? 'abandoned'
              : outcome,
      // AgentAbandoned of agentId * reason → fields [agentId, reason]
      reason: outcome === 'AgentAbandoned' ? payload[1] : undefined,
      workRecord: outcome === 'AgentCompleted' ? payload.WorkRecord : undefined,
    }
  }

  const tuple2 = (key) => {
    if (Array.isArray(key)) return { creationOrder: key[0], targetAgent: key[1] }
    if (key && typeof key === 'object' && Array.isArray(key.fields)) {
      return { creationOrder: key.fields[0], targetAgent: key.fields[1] }
    }
    if (key && typeof key === 'object') {
      const a = key[0] ?? key.Item1
      const b = key[1] ?? key.Item2
      if (a !== undefined) return { creationOrder: a, targetAgent: b }
    }
    throw new Error(`stableJoinKey unexpected shape: ${JSON.stringify(key)}`)
  }

  return {
    /** EXEC-018 production key: (CreationOrder, TargetAgent). */
    stableJoinKey: (record) => tuple2(m.stableJoinKey(record)),

    /** Merge reportableAbandoned + joinable, sort by stableJoinKey. */
    orderedCandidates: (projection) => listItems(m.orderedCandidates(projection)),

    /**
     * Journal-backed production drain (merge → sort → CAS consume).
     * Returns { ok:true, items } or { ok:false, error }.
     */
    drainFromJournal: async (journal, parentId, maxCount, completedAt = utcOffset('2024-01-01T00:00:00.000Z')) => {
      const value = resultOf(await m.drainFromJournal(journal, parentId, maxCount, completedAt))
      if (!value.ok) {
        return {
          ok: false,
          error: typeof value.error === 'string' ? value.error : caseOf(value.error),
        }
      }
      return { ok: true, items: listItems(value.value).map(completionView) }
    },
  }
})()

// ── orchestrator (docs/what/orchestrator.md) ───────────────────────────────────────────────────

export const orchestratorProjection = (() => {
  const m = bind(OrchestratorProj, 'OrchestratorProjection', [
    'empty',
    'tryFind',
    'tryFindByManagerSession',
    'tryWorktreeEffect',
    'activeJobs',
    'createJob',
    'recordProgress',
    'requestWorktree',
    'acceptWorktree',
    'recoveryAction',
  ])

  return {
    empty: m.empty,
    tryFind: (jobId, current) => unwrapOption(m.tryFind(jobId, current)),
    tryFindByManagerSession: (session, current) => unwrapOption(m.tryFindByManagerSession(session, current)),
    tryWorktreeEffect: (identity, current) => unwrapOption(m.tryWorktreeEffect(identity, current)),
    activeJobs: (current) => listItems(m.activeJobs(current)),
    createJob: (job, current) => m.createJob(job, current),
    recordProgress: (jobId, progress, current) => m.recordProgress(jobId, progress, current),
    requestWorktree: (identity, path, jobId, current) => m.requestWorktree(identity, path, jobId, current),
    acceptWorktree: (identity, path, jobId, current) => m.acceptWorktree(identity, path, jobId, current),

    /** ORCH-007: the single recovery action, by case name. */
    recoveryAction: (currentHead, job) => caseOf(m.recoveryAction(currentHead, job)),
    recoveryActionPayload: (currentHead, job) => payloadOf(m.recoveryAction(currentHead, job)),
    progressOf: (job) => caseOf(job.Progress),
    /** PERSIST-009 worktree claim status case name, or undefined if absent. */
    worktreeEffectOf: (identity, current) => {
      const status = unwrapOption(m.tryWorktreeEffect(identity, current))
      return status === undefined ? undefined : caseOf(status)
    },
  }
})()

export const jobProgress = (() => {
  const build = unionCase(OrchestratorProj.JobProgress, 'JobProgress')
  return { of: (name, payload) => build(name, payload === undefined ? [] : [payload]) }
})()

const orchestratorVerdictOf = (verdict) => {
  const name = caseOf(verdict)
  const fields = verdict.fields ?? []

  switch (name) {
    case 'Published':
      return { case: name, jobId: idValue.managerJob(fields[0]), head: idValue.commit(fields[1]) }
    case 'RejectedDirty':
      return { case: name, reason: fields[0] }
    case 'NeedsReview':
    case 'IntegrationFailed':
      return { case: name, jobId: idValue.managerJob(fields[0]), details: fields[1] }
    case 'Empty':
      return { case: name }
    default:
      throw new Error(`unknown OrchestratorVerdict case: ${name}`)
  }
}

export const orchestratorRuntime = {
  ok: okResult,
  error: errorResult,
  create: ({ git, manager, repoPath, target = 'refs/heads/main', journal }) =>
    OrchestratorRuntime[Object.keys(OrchestratorRuntime).find((k) => k.startsWith('Orchestrator_$ctor_'))](
      new OrchestratorTypes.GitPort(
        git.isDirty,
        (jobId, path) => git.createWorktree(jobId, path),
        git.freezeTargetBranch,
        (path, targetRef) => git.rebase(path, targetRef),
        (path, targetRef, expectedHead) => git.ffMerge(path, targetRef, expectedHead),
        (path) => git.conflictedFiles(path).then((result) =>
          resultOf(result).ok ? okResult(toList(resultOf(result).value)) : result),
        git.removeWorktree,
        git.hasRebaseHead,
        () => git.listWorktrees(),
        () => git.listManagerBranches(),
        git.deleteBranch,
        git.readHead,
        git.getTargetHead,
      ),
      new OrchestratorTypes.ManagerPort(
        manager.startManager,
        manager.awaitManager,
        (jobId, managerSessionId, worktree, barrierId) =>
          manager.reverify(jobId, managerSessionId, worktree, barrierId),
        (jobId, worktree, prompt) => manager.resumeManager(jobId, worktree, prompt),
      ),
      repoPath,
      targetRef(target),
      journal
        ? new OrchestratorTypes.OrchestratorJournalPort(
            journal.append,
            journal.snapshot ?? (() => fold.empty),
          )
        : undefined,
      undefined,
    ),
  forkManager: async (runtime, { job, managerAgent, prompt, worktree }) => {
    const result = resultOf(await OrchestratorRuntime[Object.keys(OrchestratorRuntime).find((k) => k.startsWith('Orchestrator__ForkManager'))](
      runtime,
      managerJobId(job),
      managerAgent,
      prompt,
      worktreePath(worktree),
    ))

    return result.ok
      ? { ok: true, value: { jobId: idValue.managerJob(result.value.JobId), worktreePath: idValue.worktreePath(result.value.WorktreePath) } }
      : { ok: false, error: orchestratorVerdictOf(result.error) }
  },
  join: async (runtime) => orchestratorVerdictOf(await OrchestratorRuntime.Orchestrator__JoinPublished(runtime)),
}

/** CompletionMailbox: dual-channel wake + PTY drain (EXEC-018 / GREEN-5). */
export const completionMailbox = (() => {
  // Class lives on the module as `CompletionMailbox` (type name = file root).
  // Methods compile as non-curried module statics: `CompletionMailbox__Join_*(_, timeoutMs)`.
  const Mailbox = CompletionMailboxModule.CompletionMailbox
  if (Mailbox === undefined) {
    throw new Error('Session/CompletionMailbox did not export CompletionMailbox')
  }

  const joinFn = fableInstanceMethod(CompletionMailboxModule, 'CompletionMailbox', 'Join')
  const publishPtyFn = fableInstanceMethod(
    CompletionMailboxModule,
    'CompletionMailbox',
    'PublishPtyCompletion',
  )
  const pulseAgentFn = fableInstanceMethod(
    CompletionMailboxModule,
    'CompletionMailbox',
    'PulseAgentHandle',
  )
  const cancelFn = fableInstanceMethod(CompletionMailboxModule, 'CompletionMailbox', 'Cancel')
  const pendingCountFn = fableInstanceMethod(
    CompletionMailboxModule,
    'CompletionMailbox',
    'get_PendingCount',
  )
  const pendingPtyCountFn = fableInstanceMethod(
    CompletionMailboxModule,
    'CompletionMailbox',
    'get_PendingPtyCount',
  )
  const isCancelledFn = fableInstanceMethod(
    CompletionMailboxModule,
    'CompletionMailbox',
    'get_IsCancelled',
  )
  const drainPtyFn = fableInstanceMethod(
    CompletionMailboxModule,
    'CompletionMailbox',
    'DrainPtyCompletions',
  )
  const drainAgentWakesFn = fableInstanceMethod(
    CompletionMailboxModule,
    'CompletionMailbox',
    'DrainAgentWakes',
  )
  const waitForSignalFn = fableInstanceMethod(
    CompletionMailboxModule,
    'CompletionMailbox',
    'WaitForSignal',
  )
  const waitForWakeFn = fableInstanceMethod(
    CompletionMailboxModule,
    'CompletionMailbox',
    'WaitForWake',
  )
  const pulseWakeFn = fableInstanceMethod(CompletionMailboxModule, 'CompletionMailbox', 'PulseWake')
  // PtyJoinItem lives in AgentCompletion; toRunCompletion projects for Join wire.
  const toRunCompletionFn = member(
    AgentCompletionModuleEarly,
    'PtyJoinItem',
    'toRunCompletion',
  )
  // Type name collides with module name, so Fable does NOT export named case
  // constructors: dist emits `PtyJoinItem` Union + `PtyExit` record class +
  // `PtyJoinItemModule_*` module functions. Construct by tag at the facade
  // boundary; case index resolved from cases() so a reorder fails loudly.
  const PtyJoinItemUnion = AgentCompletionModuleEarly.PtyJoinItem
  const PtyExitRecord = AgentCompletionModuleEarly.PtyExit
  const PtyFailureRecord = AgentCompletionModuleEarly.PtyFailure
  const PtyAbortRecord = AgentCompletionModuleEarly.PtyAbort
  if (typeof PtyJoinItemUnion !== 'function' || typeof PtyExitRecord !== 'function') {
    throw new Error(
      `PtyJoinItem/PtyExit missing; keys=${Object.keys(AgentCompletionModuleEarly).filter((k) => k.includes('Pty')).join(',')}`,
    )
  }
  const buildPtyJoinItem = unionCase(PtyJoinItemUnion, 'PtyJoinItem')
  const ptyExitedOfPayload = (payload) =>
    buildPtyJoinItem('PtyExited', [
      new PtyExitRecord(
        payload.PtyId,
        payload.Outcome,
        payload.Closed === undefined ? true : !!payload.Closed,
      ),
    ])
  const ptyFailedOfPayload = (payload) => {
    if (typeof PtyFailureRecord !== 'function') {
      throw new Error('PtyFailure record missing from AgentCompletion module')
    }
    return buildPtyJoinItem('PtyFailed', [
      new PtyFailureRecord(
        payload.PtyId,
        payload.Outcome ?? payload.Message ?? 'failed',
        payload.Closed === undefined ? true : !!payload.Closed,
        payload.Code ?? 'ERROR',
        payload.Message ?? payload.Outcome ?? 'failed',
      ),
    ])
  }
  const ptyAbortedOfPayload = (payload) => {
    if (typeof PtyAbortRecord !== 'function') {
      throw new Error('PtyAbort record missing from AgentCompletion module')
    }
    return buildPtyJoinItem('PtyAborted', [
      new PtyAbortRecord(
        payload.PtyId,
        payload.Outcome ?? payload.Message ?? 'PTY aborted',
        payload.Closed === undefined ? true : !!payload.Closed,
        payload.Code ?? 'PTY_ABORTED',
        payload.Message ?? payload.Outcome ?? 'PTY aborted',
      ),
    ])
  }

  const maxJoinBatch =
    CompletionMailboxModule.JoinBatch_MaxJoinBatch ??
    CompletionMailboxModule.JoinBatch_Max ??
    CompletionMailboxModule.MaxJoinBatch ??
    CompletionMailboxModule.maxJoinBatch ??
    (() => {
      throw new Error('CompletionMailbox JoinBatch.Max / MaxJoinBatch missing')
    })()

  /** Build PtyExited item from agent-shaped test fixture fields or id string. */
  const ptyExitedOf = (completionOrId) => {
    if (typeof completionOrId === 'string') {
      return ptyExitedOfPayload({
        PtyId: completionOrId,
        Outcome: `wr-${completionOrId}`,
        Closed: true,
      })
    }
    const id = agentIdOf(completionOrId)
    const outcomePayload = completionOrId.Outcome
    let outcome = `wr-${id}`
    if (outcomePayload && typeof outcomePayload === 'object') {
      const fields = outcomePayload.fields
      if (Array.isArray(fields) && fields[0]?.WorkRecord !== undefined) {
        outcome = fields[0].WorkRecord
      } else if (outcomePayload.WorkRecord !== undefined) {
        outcome = outcomePayload.WorkRecord
      }
    }
    return ptyExitedOfPayload({ PtyId: id, Outcome: String(outcome), Closed: true })
  }

  return {
    create: (hasActive = () => true) => new Mailbox({}, hasActive),
    /** GREEN-5: PTY fact publish (replaces publish(RunCompletion)). */
    publishPty: (box, item) => publishPtyFn(box, item),
    /**
     * Test helper: publish a PTY exit derived from completedRun fixture or id string.
     * Keeps join-v2-mailbox tests readable under dual-channel semantics.
     */
    publish: (box, completionOrId) => publishPtyFn(box, ptyExitedOf(completionOrId)),
    ptyExited: ptyExitedOf,
    ptyFailed: ptyFailedOfPayload,
    ptyAborted: ptyAbortedOfPayload,
    /** EXEC-020: Code used when PtyAborted is projected through toRunCompletion. */
    ptyAbortedCode: 'PTY_ABORTED',
    toRunCompletion: (item) => toRunCompletionFn(item),
    pulseAgentHandle: (box, handle) => pulseAgentFn(box, handle),
    // timeoutMs === undefined → no deadline (Fable optional is nullish).
    join: (box, timeoutMs) => joinFn(box, timeoutMs),
    cancel: (box) => cancelFn(box),
    pendingCount: (box) => pendingCountFn(box),
    pendingPtyCount: (box) => pendingPtyCountFn(box),
    isCancelled: (box) => isCancelledFn(box),
    drainPtyCompletions: (box, maxCount) => listItems(drainPtyFn(box, maxCount)),
    drainAgentWakes: (box, maxCount) => listItems(drainAgentWakesFn(box, maxCount)),
    /**
     * Drain PTY channel and project to RunCompletion (Join wire shape).
     * Tests assert AgentId / publish order on this projection.
     */
    drainAvailable: (box, maxCount) =>
      listItems(drainPtyFn(box, maxCount)).map((item) => toRunCompletionFn(item)),
    waitForSignal: (box, interrupt) => waitForSignalFn(box, interrupt),
    waitForWake: (box) => waitForWakeFn(box),
    pulseWake: (box) => pulseWakeFn(box),
    maxJoinBatch,
  }
})()/** EXEC-018 batch ceiling — single export for wire/runtime tests. */
export const maxJoinBatch = completionMailbox.maxJoinBatch

/** EXEC-017 local join interrupt (tool abort → Signal only). */
export const joinInterrupt = (() => {
  const createFn = member(CompletionMailboxModule, 'JoinInterrupt', 'create')
  return {
    create: () => createFn(),
    wait: (interrupt) => interrupt.Wait,
    signal: (interrupt) => interrupt.Signal(),
  }
})()

/** EXEC-004 / EXEC-018 NonEmptyBatch constructors. */
export const nonEmptyBatch = (() => {
  const ofHeadTailFn = member(CompletionMailboxModule, 'NonEmptyBatch', 'ofHeadTail')
  const tryOfListFn = member(CompletionMailboxModule, 'NonEmptyBatch', 'tryOfList')
  const toListFn = member(CompletionMailboxModule, 'NonEmptyBatch', 'toList')
  const lengthFn = member(CompletionMailboxModule, 'NonEmptyBatch', 'length')
  return {
    ofHeadTail: (head, tail = []) => ofHeadTailFn(head, toList(tail)),
    tryOfList: (items) => unwrapOption(tryOfListFn(toList(items))),
    toList: (batch) => listItems(toListFn(batch)),
    length: (batch) => lengthFn(batch),
  }
})()

/** JoinWaitOutcome DU helpers (case names, never ordinals). */
export const joinWaitOutcome = {
  nameOf: (outcome) => caseOf(outcome),
  isInterrupted: (outcome) => caseOf(outcome) === 'InterruptedByUserMessage',
  results: (outcome) => {
    if (caseOf(outcome) !== 'ResultsAvailable') {
      throw new Error(`expected ResultsAvailable, got ${caseOf(outcome)}`)
    }
    return payloadOf(outcome)
  },
}

/** MailboxWakeReason case name. */
export const mailboxWakeReason = {
  nameOf: (reason) => caseOf(reason),
}

/** HostEventPort sticky terminal + late-subscriber replay. */
export const hostEventPort = (() => {
  const Port =
    EventsModule.HostEventPort ??
    EventsModule.Events_HostEventPort ??
    EventsModule.Events$HostEventPort
  if (Port === undefined) {
    const keys = Object.keys(EventsModule).filter((k) => k.includes('Host') || k.includes('Event') || k.includes('Port'))
    throw new Error(`Events.HostEventPort missing. Near: ${keys.join(', ') || '(none)'}`)
  }

  const TerminalOutcome = EventsModule.TerminalOutcome
  if (TerminalOutcome === undefined) {
    throw new Error('Events.TerminalOutcome missing')
  }

  // Prototype methods exist on Events_HostEventPort; also accept static exports if present.
  const callPort = (port, name, args) => {
    const method =
      port[name] ??
      port[`IEventObservationPort_${name}`] ??
      port[`IEventObservationPort__${name}`]
    if (typeof method === 'function') {
      return method.apply(port, args)
    }
    const staticFn = fableInstanceMethod(EventsModule, 'Events_HostEventPort', name)
    return staticFn(port, ...args)
  }

  return {
    create: () => new Port(),
    subscribe: (port, listener) => callPort(port, 'SubscribeTerminalListener', [listener]),
    notify: (port, sessionId, outcome) => callPort(port, 'NotifyTerminal', [sessionId, outcome]),
    /** Failed outcome — enough for sticky/replay tests (no ProviderRun dual-instance dedupe). */
    failed: (error = 'test-fail') => new TerminalOutcome(2, [error]),
    aborted: (reason = 'test-abort') => new TerminalOutcome(1, [reason]),
    /** Production sticky terminal capacity (Events.fs stickyCap). */
    stickyCap: 256,
  }
})()

/**
 * ReconcileSupervisor: per-session single-flight reconcile with bounded causal
 * rereads (maxCausalRereads) until terminal, reread budget exhausted, or session
 * clear. Continuous Snapshot Errors stop the pass at maxConsecutiveErrors
 * (no wall-clock budget / timer backoff).
 */
export const reconcileSupervisor = (() => {
  const Supervisor = ReconcileSupervisorModule.Supervisor
  if (Supervisor === undefined) {
    throw new Error('ReconcileSupervisor.Supervisor missing')
  }
  const Store = TurnBindingModule.Store
  if (Store === undefined) {
    throw new Error('TurnBinding.Store missing')
  }
  const SessionMessage = SessionSnapshotPortModule.SessionMessage
  if (SessionMessage === undefined) {
    throw new Error('SessionSnapshotPort.SessionMessage missing')
  }
  const MessagePart = HostMessageCodecModule.MessagePart
  if (MessagePart === undefined) {
    throw new Error('HostMessageCodec.MessagePart missing')
  }

  const kickFn = fableInstanceMethod(ReconcileSupervisorModule, 'Supervisor', 'Kick')
  const bindUserFn = fableInstanceMethod(ReconcileSupervisorModule, 'Supervisor', 'BindUserMessage')
  const clearSessionFn = fableInstanceMethod(ReconcileSupervisorModule, 'Supervisor', 'ClearSession')

  const textPart = (text) => new MessagePart(0, [text])

  const message = ({
    id,
    role,
    finish = undefined,
    errorName = undefined,
    completed = false,
    parts = [],
    agent = undefined,
    parentId = undefined,
  }) =>
    new SessionMessage(
      id,
      role,
      agent,
      finish,
      errorName,
      undefined,
      parentId,
      completed,
      false,
      undefined,
      parts,
    )

  return {
    createStore: () => new Store(),
    /**
     * `reads` is a queue of Result shapes: `{ ok: true, messages }` or `{ ok: false, error }`.
     * Each GetMessages call consumes one entry (last entry repeats if exhausted).
     * Optional `onRead` fires once per GetMessages (for budget tests).
     */
    createSnapshot: (reads, onRead) => {
      const queue = [...reads]
      let last = queue[queue.length - 1]
      let readCount = 0
      return {
        get readCount() {
          return readCount
        },
        GetMessages(_sessionId) {
          readCount += 1
          if (typeof onRead === 'function') onRead(readCount)
          const next = queue.length > 0 ? queue.shift() : last
          last = next
          if (next.ok) {
            return Promise.resolve(okResult(toList(next.messages)))
          }
          return Promise.resolve(errorResult(next.error ?? 'snapshot-error'))
        },
      }
    },
    message,
    textPart,
    /** Terminal assistant turn: finish=stop + formal text (TurnCompleted). */
    terminalTranscript: (userId = 'user-1', assistantId = 'asst-1') => [
      message({ id: userId, role: 'user', completed: true, parts: [textPart('assignment')] }),
      message({
        id: assistantId,
        role: 'assistant',
        finish: 'stop',
        completed: true,
        parentId: userId,
        parts: [textPart('done')],
      }),
    ],
    /** In-progress assistant: finish=tool-calls → TurnInProgress (incomplete material). */
    inProgressTranscript: (userId = 'user-1', assistantId = 'asst-ip') => [
      message({ id: userId, role: 'user', completed: true, parts: [textPart('assignment')] }),
      message({
        id: assistantId,
        role: 'assistant',
        finish: 'tool-calls',
        completed: false,
        parentId: userId,
        parts: [textPart('working')],
      }),
    ],
    create: ({
      snapshot,
      binding,
      onTurn,
      onDeleted,
      projection,
      onSnapshot,
      maxCausalRereads,
      maxConsecutiveErrors,
    } = {}) => {
      if (snapshot === undefined || binding === undefined || onTurn === undefined) {
        throw new Error('reconcileSupervisor.create requires snapshot, binding, onTurn')
      }
      // Fable optional ctor args: undefined → None → production defaults.
      return new Supervisor(
        snapshot,
        binding,
        onTurn,
        onDeleted,
        projection,
        onSnapshot,
        maxCausalRereads,
        maxConsecutiveErrors,
      )
    },
    bindUserMessage: (supervisor, session, physical, agentRole) =>
      bindUserFn(supervisor, session, physical, agentRole),
    kick: (supervisor, session) => kickFn(supervisor, session),
    clearSession: (supervisor, session) => clearSessionFn(supervisor, session),
  }
})()

// ── Orchestrator direct-CE workflow (FLOW-001 / PR3) ─────────────────────────
// Application/Orchestration/Program.fs is the sole production entrypoint.
// Domain AST + OrchestratorInterpreter were deleted; tests use fake ports via
// orchestratorRuntime, not reply-bearing Program trees.
export const orchestratorProgram = (() => {
  let cached
  const load = async () => {
    if (cached) return cached
    cached = await prod('Change/Program')
    return cached
  }
  return {
    /** Sole production entrypoint Runtime calls. */
    run: async (deps, job) => {
      const mod = await load()
      const fn = mod.run ?? mod.OrchestratorProgram_run
      if (typeof fn !== 'function') {
        throw new Error(
          `OrchestratorProgram.run missing; exports: ${Object.keys(mod).join(', ')}`,
        )
      }
      if (fn.length >= 2) return fn(deps, job)
      const partial = fn(deps)
      return typeof partial === 'function' ? partial(job) : partial
    },
  }
})()

// ── Join direct CE (P0-RECOVERY-JOIN-001 + EXEC-018 / PR5) ───────────────────
// Domain JoinProgram AST deleted. Application/Reconciliation/Join.fs is the sole
// permit-gated entry. Tests assert production surface, not AST case names.
const JoinModule = await prod('Execution/Delegation/Join')
// AgentCompletion loaded early (AgentCompletionModuleEarly) for mailbox dual-channel.
const AgentCompletionModule = AgentCompletionModuleEarly
const JoinResultRendererModule = await prod('Execution/Delegation/Fork/OpenCode/JoinResultRenderer')
const ManagerJobModule = await prod('Change/Job')
// EXEC-017 cases live on CompletionMailboxModule (already loaded above).
const JoinInterruptReason = CompletionMailboxModule.JoinInterruptReason

export const joinProgram = (() => {
  const joinAnyFn = JoinModule.joinAny ?? JoinModule.Join_joinAny
  const joinAvailableFn = JoinModule.joinAvailable ?? JoinModule.Join_joinAvailable
  if (typeof joinAnyFn !== 'function' || typeof joinAvailableFn !== 'function') {
    throw new Error(
      `Join.joinAny/joinAvailable missing; exports: ${Object.keys(JoinModule).join(', ')}`,
    )
  }
  return {
    /** Direct CE: FamilyRecoveryPermit → runtime.JoinWithPermit. */
    joinAny: joinAnyFn,
    /** EXEC-018 batch: permit + maxCount + interrupt.Wait. */
    joinAvailable: joinAvailableFn,
  }
})()

/**
 * AgentCompletion / RunCompletion constructors for mailbox + renderer tests.
 * Role is Session.AgentRole (ForkTypes), not Kernel.Role.
 */
export const agentCompletion = (() => {
  const ofSimpleTextFn = member(AgentCompletionModule, 'AgentCompletion', 'ofSimpleText')
  const ofSimpleErrorFn = member(AgentCompletionModule, 'AgentCompletion', 'ofSimpleError')
  const failedFn = member(AgentCompletionModule, 'AgentCompletion', 'failed')
  const abandonedFn = member(AgentCompletionModule, 'AgentCompletion', 'abandoned')
  const statusFn = member(AgentCompletionModule, 'AgentCompletion', 'status')
  const textFn = member(AgentCompletionModule, 'AgentCompletion', 'text')
  // GREEN-3: JoinItem.ofRunCompletion — agent vs PTY projection surface.
  const joinItemOfRunCompletionFn = member(AgentCompletionModule, 'JoinItem', 'ofRunCompletion')

  const roleOf = (name) => {
    const value = (ForkTypesModule.AgentRole ?? RolesModule.Role)?.[name]
    if (value === undefined) throw new Error(`unknown AgentRole '${name}'`)
    return value
  }

  /** Build a RunCompletion record for mailbox publish / renderer input. */
  const run = ({
    runId,
    agentName = '',
    role = 'Coder',
    outcome,
    completedAt = new Date(),
  }) => ({
    RunId: runId,
    AgentName: agentName,
    Role: typeof role === 'string' ? roleOf(role) : role,
    Outcome: outcome,
    CompletedAt: completedAt,
  })

  return {
    role: roleOf,
    ofSimpleText: (agentId, runId, role, text) =>
      ofSimpleTextFn(agentId, runId, typeof role === 'string' ? roleOf(role) : role, text),
    ofSimpleError: (agentId, runId, role, message) =>
      ofSimpleErrorFn(agentId, runId, typeof role === 'string' ? roleOf(role) : role, message),
    failed: (agentId, runId, role, code, message) =>
      failedFn(
        agentId,
        runId,
        role === undefined || role === null ? undefined : typeof role === 'string' ? roleOf(role) : role,
        undefined,
        code,
        message,
      ),
    status: (outcome) => statusFn(outcome),
    text: (outcome) => textFn(outcome),
    run,
    /** Convenience: completed agent RunCompletion with work record text. */
    completedRun: ({ runId, agentId, agentName, role = 'Coder', workRecord = '' }) =>
      run({
        runId,
        agentId,
        agentName: agentName ?? agentId,
        role,
        outcome: ofSimpleTextFn(agentId, runId, roleOf(role), workRecord),
      }),
    failedRun: ({ runId, agentId, agentName, role = 'Coder', code = 'ERROR', message = 'failed' }) =>
      run({
        runId,
        agentId,
        agentName: agentName ?? agentId,
        role,
        outcome: failedFn(agentId, runId, roleOf(role), undefined, code, message),
      }),
    /**
     * GREEN-3: AgentAborted deleted. No abortedRun factory.
     * Legacy abort blobs use handleCompletionCodec.legacyAbortedBody only.
     */
    abandoned: (agentId, reason) => abandonedFn(agentId, reason),
    abandonedRun: ({ runId, agentId, agentName, role = 'Coder', reason = 'ParentCancelled' }) =>
      run({
        runId: runId ?? `abandoned-${agentId}`,
        agentId,
        agentName: agentName ?? agentId,
        role,
        outcome: abandonedFn(agentId, reason),
      }),
    /** Project RunCompletion → JoinItem (AgentItem | PtyItem). */
    joinItemOfRunCompletion: (isPtyRun, completion) => joinItemOfRunCompletionFn(isPtyRun, completion),
  }
})()

/**
 * EXEC-004 rev.2 / docs/how/synthetic-toml.md ### Join / fork JoinResultRenderer — LLM-facing join wire only.
 * `runtime` for agent/pty batch is a minimal { IsPtyCompletion, TryFindAgent } surface.
 */
export const joinResultRenderer = (() => {
  const english = providerLanguage.english
  const renderInterruptedFn = member(JoinResultRendererModule, 'JoinResultRenderer', 'renderInterrupted')
  const renderCompletedBatchFn = member(JoinResultRendererModule, 'JoinResultRenderer', 'renderCompletedBatch')
  const renderJoinItemBatchFn = member(JoinResultRendererModule, 'JoinResultRenderer', 'renderJoinItemBatch')
  const renderOrchestratorBatchFn = member(JoinResultRendererModule, 'JoinResultRenderer', 'renderOrchestratorBatch')
  const renderForkErrorFn = member(JoinResultRendererModule, 'JoinResultRenderer', 'renderForkError')

  /**
   * Minimal HostForkRuntime surface for JoinResultRenderer.
   * IsPtyCompletion is a type extension (HostForkPty) that Fable may emit as a
   * module function reading `.Gate` / `.PtyRuns` — supply both property and getter.
   */
  const stubRuntime = ({ ptyRunIds = new Set(), agents = new Map() } = {}) => {
    const ids = ptyRunIds instanceof Set ? ptyRunIds : new Set(ptyRunIds)
    const gate = {}
    // Fable HashSet-like: Contains + has + .has for either emission.
    const ptyRuns = {
      Contains: (runId) => ids.has(runId),
      contains: (runId) => ids.has(runId),
      has: (runId) => ids.has(runId),
    }
    return {
      Gate: gate,
      get_Gate: () => gate,
      PtyRuns: ptyRuns,
      get_PtyRuns: () => ptyRuns,
      IsPtyCompletion: (runId) => ids.has(runId),
      TryFindAgent: (agentId) => agents.get(agentId),
    }
  }

  return {
    /** @param reason JoinInterruptReason (default OperatorAbort for legacy callers). */
    renderInterrupted: (reason = JoinInterruptReason.OperatorAbort) => renderInterruptedFn(english, reason),
    renderCompletedBatch: (runtime, batch, resolveTerminalLabel) => {
      const isPty = (runId) =>
        typeof runtime?.IsPtyCompletion === 'function' ? !!runtime.IsPtyCompletion(runId) : false
      const resolve = (agentId) => {
        if (typeof runtime?.TryFindAgent !== 'function') return ''
        const rec = runtime.TryFindAgent(agentId)
        if (!rec) return ''
        return rec.Agent ?? rec.agent ?? ''
      }
      if (typeof resolveTerminalLabel === 'function') {
        return renderCompletedBatchFn(english, isPty, resolve, batch, resolveTerminalLabel)
      }
      return renderCompletedBatchFn(english, isPty, resolve, batch, () => 'Terminal')
    },
    /** Production JoinTool path: NonEmptyBatch<JoinItem> with PtyAborted intact. */
    renderJoinItemBatch: (resolveAgentName, batch, resolveTerminalLabel = () => 'Terminal') => {
      const resolve =
        typeof resolveAgentName === 'function'
          ? resolveAgentName
          : (agentId) => {
              if (typeof resolveAgentName?.TryFindAgent !== 'function') return ''
              const rec = resolveAgentName.TryFindAgent(agentId)
              if (!rec) return ''
              return rec.Agent ?? rec.agent ?? ''
            }
      return renderJoinItemBatchFn(english, resolve, batch, resolveTerminalLabel)
    },
    renderOrchestratorBatch: (batch) => renderOrchestratorBatchFn(english, batch),
    renderForkError: (error, resolveAgentName = () => '') => renderForkErrorFn(english, error, resolveAgentName),
    stubRuntime,
  }
})()

/** EXEC-019 VerdictMailbox: FIFO batch drain + JoinAvailable interrupt race. */
export const verdictMailbox = (() => {
  const Mailbox = ManagerJobModule.VerdictMailbox
  if (Mailbox === undefined) {
    throw new Error('Application/Orchestration/ManagerJob did not export VerdictMailbox')
  }

  const publishFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'Publish')
  const startJobFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'StartJob')
  const drainFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'DrainAvailable')
  const tryJoinBatchFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'TryJoinBatch')
  const tryJoinFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'TryJoin')
  const joinAvailableFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'JoinAvailable')
  const pendingCountFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'get_PendingCount')
  const hasActiveFn = fableInstanceMethod(ManagerJobModule, 'VerdictMailbox', 'get_HasActive')

  const buildVerdict = unionCase(OrchestratorTypes.OrchestratorVerdict, 'OrchestratorVerdict')

  return {
    create: () => new Mailbox(),
    startJob: (box) => startJobFn(box),
    publish: (box, verdict) => publishFn(box, verdict),
    drainAvailable: (box, maxCount) => listItems(drainFn(box, maxCount)),
    tryJoinBatch: (box, maxCount) => tryJoinBatchFn(box, maxCount).then((list) => listItems(list)),
    tryJoin: (box) => tryJoinFn(box),
    joinAvailable: (box, maxCount, interrupt) => joinAvailableFn(box, maxCount, interrupt),
    pendingCount: (box) => pendingCountFn(box),
    hasActive: (box) => hasActiveFn(box),
    /** Construct OrchestratorVerdict by case name (fields as raw Fable values when needed). */
    verdict: (name, fields = []) => buildVerdict(name, fields),
    empty: () => buildVerdict('Empty', []),
    rejectedDirty: (reason) => buildVerdict('RejectedDirty', [reason]),
    published: (jobId, head) => buildVerdict('Published', [managerJobId(jobId), commitHash(head)]),
    needsReview: (jobId, details) => buildVerdict('NeedsReview', [managerJobId(jobId), details]),
    integrationFailed: (jobId, details) => buildVerdict('IntegrationFailed', [managerJobId(jobId), details]),
    nameOf: (verdict) => caseOf(verdict),
  }
})()

// ── Reconcile pure Domain (FLOW-001 / PR4) ───────────────────────────────────
// Domain/ReconcileProgram keeps Evidence → Decision + publish seals only.
// Command/Reply/Step AST + TraceInterpreter deleted; workflow is Reconciler.fs.
const ReconcileProgramModule = await prod('Composition/Turn/Program')

export const reconcileProgram = (() => {
  const mod = ReconcileProgramModule

  const applyArgs = (fn, args) => {
    if (typeof fn !== 'function') {
      throw new TypeError('reconcileProgram: expected function')
    }
    if (args.length === 0) return fn()
    if (fn.length === 0 || fn.length >= args.length) return fn(...args)
    let cur = fn
    for (const arg of args) {
      if (typeof cur !== 'function') {
        throw new TypeError('reconcileProgram: curried application exhausted early')
      }
      cur = cur(arg)
    }
    return cur
  }

  const requireFn = (candidates, label) => {
    const found = candidates.find((fn) => typeof fn === 'function')
    if (typeof found !== 'function') {
      throw new Error(
        `${label} missing on Domain/ReconcileProgram. Near: ${Object.keys(mod)
          .filter((k) => /Reconcile|decide|publish|isTerminal|pickDelay|evidence|turn/i.test(k))
          .slice(0, 40)
          .join(', ')}`,
      )
    }
    return found
  }

  const call = (candidates, label, args = []) => applyArgs(requireFn(candidates, label), args)
  const resolve = (candidates, label) => requireFn(candidates, label)

  const isWrappedMaps = (value) => value !== null && typeof value === 'object' && value.__reconcileRawMaps !== undefined
  const unwrapMaps = (value) => (isWrappedMaps(value) ? value.__reconcileRawMaps : value)

  const mapsMember = (baseName) => {
    const keys = Object.keys(mod ?? {})
    const suffixed =
      keys.find((key) => key.startsWith(`ReconcileProgram_PublishMaps__${baseName}_`)) ??
      keys.find((key) => key.startsWith(`PublishMaps__${baseName}_`)) ??
      undefined
    return requireFn(
      [
        mod?.[`ReconcileProgram_PublishMaps__${baseName}`],
        mod?.[`PublishMaps__${baseName}`],
        mod?.[baseName],
        suffixed !== undefined ? mod?.[suffixed] : undefined,
      ],
      `PublishMaps.${baseName}`,
    )
  }

  const wrapMaps = (raw) => {
    if (isWrappedMaps(raw)) return raw
    if (!raw || typeof raw !== 'object') return raw
    const provisionalHas = mapsMember('provisionalHas')
    const consumedHas = mapsMember('consumedHas')
    return {
      __reconcileRawMaps: raw,
      Consumed: raw.Consumed,
      Provisional: raw.Provisional,
      provisionalHas: (turn) => applyArgs(provisionalHas, [raw, turn]),
      consumedHas: (turn) => applyArgs(consumedHas, [raw, turn]),
    }
  }

  const outcomeOf = (name) =>
    call(
      [
        mod?.outcomeOf,
        mod?.ReconcileProgram_outcomeOf,
        mod?.TurnOutcomeModule_ofName,
        mod?.TurnOutcome_ofName,
      ],
      'outcomeOf',
      [name],
    )

  return {
    get isTerminalOutcome() {
      const fn = resolve(
        [
          mod?.isTerminalOutcome,
          mod?.ReconcileProgram_isTerminalOutcome,
          mod?.Reconcile_isTerminalOutcome,
        ],
        'isTerminalOutcome',
      )
      return (outcomeName) => {
        const outcome = typeof outcomeName === 'string' ? outcomeOf(outcomeName) : outcomeName
        return applyArgs(fn, [outcome])
      }
    },

    get decideStep() {
      const fn = resolve(
        [mod?.decideStep, mod?.ReconcileProgram_decideStep, mod?.Reconcile_decideStep],
        'decideStep',
      )
      return (wake, rereadsRemaining, evidence) => applyArgs(fn, [wake, rereadsRemaining, evidence])
    },

    get decisionName() {
      const fn = resolve(
        [mod?.decisionName, mod?.ReconcileProgram_decisionName, mod?.ReconcileDecision_name],
        'decisionName',
      )
      return (decision) => applyArgs(fn, [decision])
    },

    get clearsContinuationCandidate() {
      const fn = resolve(
        [
          mod?.clearsContinuationCandidate,
          mod?.ReconcileProgram_clearsContinuationCandidate,
          mod?.ReconcileDecision_clearsContinuationCandidate,
        ],
        'clearsContinuationCandidate',
      )
      return (decision) => applyArgs(fn, [decision])
    },

    get publishDecision() {
      const fn = resolve(
        [mod?.publishDecision, mod?.ReconcileProgram_publishDecision],
        'publishDecision',
      )
      return (maps, turn) => {
        const raw = applyArgs(fn, [unwrapMaps(maps), turn])
        let normalized
        if (raw && typeof raw === 'object' && 'shouldPublish' in raw) {
          normalized = raw
        } else if (Array.isArray(raw) && raw.length >= 2) {
          normalized = { shouldPublish: raw[0], maps: raw[1] }
        } else {
          return raw
        }
        if (normalized && typeof normalized === 'object' && 'maps' in normalized) {
          return { shouldPublish: normalized.shouldPublish, maps: wrapMaps(normalized.maps) }
        }
        return normalized
      }
    },

    get clearProvisional() {
      const fn = resolve(
        [mod?.clearProvisional, mod?.ReconcileProgram_clearProvisional],
        'clearProvisional',
      )
      return (maps, sessionKey) => wrapMaps(applyArgs(fn, [unwrapMaps(maps), sessionKey]))
    },

    get consumeKey() {
      const fn = resolve([mod?.consumeKey, mod?.ReconcileProgram_consumeKey], 'consumeKey')
      return (turn) => applyArgs(fn, [turn])
    },

    evidence: {
      snapshotError: (reason) =>
        call(
          [
            mod?.ReconcileProgram_evidenceSnapshotError,
            mod?.evidenceSnapshotError,
            mod?.ReconcileEvidence_SnapshotError,
            mod?.SnapshotError,
          ],
          'ReconcileEvidence.SnapshotError',
          [reason],
        ),
      noTurn: () =>
        call(
          [mod?.ReconcileProgram_evidenceNoTurn, mod?.evidenceNoTurn, mod?.ReconcileEvidence_NoTurn, mod?.NoTurn],
          'ReconcileEvidence.NoTurn',
          [],
        ),
      provisional: (outcomeName) =>
        call(
          [
            mod?.ReconcileProgram_evidenceProvisional,
            mod?.evidenceProvisional,
            mod?.ReconcileEvidence_Provisional,
            mod?.Provisional,
          ],
          'ReconcileEvidence.Provisional',
          [typeof outcomeName === 'string' ? outcomeOf(outcomeName) : outcomeName],
        ),
      unknown: () =>
        call(
          [mod?.ReconcileProgram_evidenceUnknown, mod?.evidenceUnknown, mod?.ReconcileEvidence_Unknown, mod?.Unknown],
          'ReconcileEvidence.Unknown',
          [],
        ),
      terminal: (outcomeName) =>
        call(
          [
            mod?.ReconcileProgram_evidenceTerminal,
            mod?.evidenceTerminal,
            mod?.ReconcileEvidence_Terminal,
            mod?.Terminal,
          ],
          'ReconcileEvidence.Terminal',
          [typeof outcomeName === 'string' ? outcomeOf(outcomeName) : outcomeName],
        ),
      observedTerminal: (turn) =>
        call(
          [
            mod?.ReconcileProgram_evidenceObservedTerminal,
            mod?.evidenceObservedTerminal,
            mod?.ReconcileEvidence_Terminal,
            mod?.Terminal,
          ],
          'ReconcileEvidence.Terminal(observedTurn)',
          [turn],
        ),
      sessionCleared: () =>
        call(
          [
            mod?.ReconcileProgram_evidenceSessionCleared,
            mod?.evidenceSessionCleared,
            mod?.ReconcileEvidence_SessionCleared,
            mod?.SessionCleared,
          ],
          'ReconcileEvidence.SessionCleared',
          [],
        ),
    },

    publishMaps: {
      empty: () =>
        call(
          [
            mod?.ReconcileProgram_publishMapsEmpty,
            mod?.publishMapsEmpty,
            mod?.PublishMaps_empty,
            mod?.emptyPublishMaps,
          ],
          'PublishMaps.empty',
          [],
        ),
    },

    turnFixture: ({ session, physical, providerRun, outcome }) =>
      call(
        [
          mod?.ReconcileProgram_turnFixture,
          mod?.turnFixture,
          mod?.testTurn,
        ],
        'turnFixture',
        [session, physical, providerRun, typeof outcome === 'string' ? outcomeOf(outcome) : outcome],
      ),
  }
})()

/** HOST-004 ReconcileWake: IdleWake of QuiescencePermit | RetryWake | FailureWake. */
const buildReconcileWake = unionCase(ReconcileProgramModule.ReconcileWake, 'ReconcileWake')
export const reconcileWake = {
  idleWake: (permit) => buildReconcileWake('IdleWake', [permit]),
  retryWake: () => buildReconcileWake('RetryWake', []),
  failureWake: () => buildReconcileWake('FailureWake', []),
  abortWake: () => buildReconcileWake('AbortWake', []),
}