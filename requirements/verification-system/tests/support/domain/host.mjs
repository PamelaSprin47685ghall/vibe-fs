// tests/unit/support/domain/host.mjs — host + session family.
// Host signals, handle projection/controller/completion codec, child recovery,
// clock/deadline/timer ports, host signal subscribe, pending run lifecycle,
// outcomes/introspection/diagnostics, loop detector/codec/sensor, runtime nudge,
// fallback controller, runtime/agent/package resources, blogger request
// context/runtime, parked transform, session recovery.

import { readFileSync } from 'node:fs'
import { join } from 'node:path'

import {
  HostEventCodecModule,
  LinkageProj,
  HandleControllerModule,
  HandleCompletionCodecModule,
  DeadlineModule,
  PtyTimingModule,
  DiagnosticModule,
  LoopDetectorModule,
  LoopEventCodecModule,
  LoopSensorModule,
  RuntimeNudgeModule,
  FallbackControllerModule,
  HostForkRunLifecycleModule,
  HostPendingRunModule,
  HostSignalSubscribeModule,
  RuntimeResourcesModule,
  ManagedAgentConfigModule,
  PackageResourcesModule,
  BloggerRequestContextModule,
  BloggerRuntimeModule,
  ParkedTransformModule,
  PluginRuntimeScopeModule,
  SharedStateModule,
  Outcome,
  FactModule,
  EnvelopeModule,
  OrchestratorProj,
  FsMap,
  FsList,
  unionCase,
  bind,
  member,
  caseOf,
  payloadOf,
  resultOf,
  toList,
  listItems,
  unwrapOption,
  isNone,
  isSome,
  okResult,
  utcOffset,
  ordinalComparer,
  caseNames,
  fableLibraryDir,
  BUILD_ROOT,
  prod,
} from './interop.mjs'
import {
  sessionId,
  providerRun,
  bloggerRequestId,
  frameEpochId,
  prefixEpochId,
  blobDigest,
  blobRef,
  idValue,
  handleId,
} from './identity.mjs'
import { completionKind, handleAbandonReason, handleOwnership } from './journal.mjs'

// ── host signals (docs/what/host.md) ───────────────────────────────────────────────────

export const hostSignals = (() => {
  const m = bind(HostEventCodecModule, 'HostEventCodec', ['isHostSignalEvent', 'tryDecode'])

  return {
    isHostSignalEvent: (raw) => m.isHostSignalEvent(raw),
    tryDecode: (raw) => m.tryDecode(raw),
  }
})()

// ── execution handles (docs/what/execution.md) ──────────────────────────────────────────────

export const handleProjection = (() => {
  const m = bind(LinkageProj, 'HandleProjection', [
    'empty',
    'link',
    'linkNamed',
    'complete',
    'abandon',
    'retire',
    'tryFind',
    'isRetired',
    'isAbandoned',
    'listable',
    'joinable',
    'reportableAbandoned',
    'activeHandles',
    'tryFindByChildSession',
    'tryFindByByname',
    'linkedChildren',
    'lifecycleSealsBlogger',
    'recordSealsBlogger',
  ])

  /**
   * Rejections carry no payload, so the case name is the whole answer.
   *
   * `resultOf` alone would hand back the union object, and `payloadOf` of a
   * fieldless case is `[]`. `JSON.stringify` makes that LOOK like a string —
   * Fable's union `toJSON` emits the case name — so a `deepEqual` against
   * `{ ok: false, error: 'AlreadyCompleted' }` fails while the log reads correct.
   */
  const decided = (result) => {
    const value = resultOf(result)
    return value.ok ? value : { ok: false, error: caseOf(value.error) }
  }

  /** EXEC-009 completion cell: kind + optional durable blob refs. */
  const completionOf = (kind, ref = undefined, digest = undefined) => ({
    Kind: typeof kind === 'string' ? completionKind.of(kind) : kind,
    CompletionRef: ref,
    CompletionDigest: digest,
  })

  return {
    empty: m.empty,
    link: (handle, child, targetAgent, role, current, ownership = handleOwnership.durableParentHandle()) =>
      decided(m.link(handle, child, targetAgent, role, ownership, current)),
    linkNamed: (handle, child, targetAgent, byname, role, current, ownership = handleOwnership.durableParentHandle()) =>
      decided(m.linkNamed(handle, child, targetAgent, byname, role, ownership, current)),
    complete: (handle, completion, current) =>
      decided(m.complete(handle, typeof completion === 'string' ? completionOf(completion) : completion, current)),
    completionOf,
    abandon: (handle, reason, current) =>
      decided(m.abandon(handle, typeof reason === 'string' ? handleAbandonReason.of(reason) : reason, current)),
    retire: (handle, current) => decided(m.retire(handle, current)),
    tryFind: (handle, current) => unwrapOption(m.tryFind(handle, current)),
    isRetired: (handle, current) => m.isRetired(handle, current),
    isAbandoned: (handle, current) => m.isAbandoned(handle, current),
    listable: (current) => listItems(m.listable(current)),
    joinable: (current) => listItems(m.joinable(current)),
    reportableAbandoned: (current) => listItems(m.reportableAbandoned(current)),
    activeHandles: (current) => listItems(m.activeHandles(current)),
    tryFindByChildSession: (child, current) => unwrapOption(m.tryFindByChildSession(child, current)),
    tryFindByByname: (byname, current) => unwrapOption(m.tryFindByByname(byname, current)),
    linkedChildren: (current) => listItems(m.linkedChildren(current)),
    lifecycleOf: (record) => caseOf(record.Lifecycle),
    lifecycleSealsBlogger: (lifecycle) => m.lifecycleSealsBlogger(lifecycle),
    recordSealsBlogger: (record) => m.recordSealsBlogger(record),
    /** EXEC-018 creation order assigned on HandleLinked. */
    creationOrder: (record) => record.CreationOrder,

    /** One handle record as comparable text. */
    read: (record) => {
      const lifecycle = caseOf(record.Lifecycle)
      let completion
      let completionRef
      let completionDigest
      let abandonReason
      if (lifecycle === 'CompletedAwaitingJoin') {
        const cell = payloadOf(record.Lifecycle)
        completion = caseOf(cell.Kind)
        completionRef = isSome(cell.CompletionRef) ? idValue.blobRef(cell.CompletionRef) : undefined
        completionDigest = isSome(cell.CompletionDigest) ? idValue.blobDigest(cell.CompletionDigest) : undefined
      } else if (lifecycle === 'Abandoned') {
        abandonReason = caseOf(payloadOf(record.Lifecycle))
      }
      return {
        handle: handleId.describe(record.Handle),
        child: idValue.session(record.ChildSessionId),
        targetAgent: record.TargetAgent,
        role: caseOf(record.CanonicalRole),
        lifecycle,
        // EXEC-018: HandleLinked fold order (stable join key #2).
        creationOrder: record.CreationOrder,
        // EXEC-005: `list` must distinguish which completion landed, so the kind is
        // part of the state rather than a flag beside it.
        completion,
        completionRef,
        completionDigest,
        abandonReason,
      }
    },
  }
})()

// ── EXEC-009 consume path (docs/what/execution.md) ──────────────────────────────────────────

/** `HostForkRuntime.Join` reads `HandleProjection.joinable` (above) as the fact
 *  source, then CAS-retires via `HandleController.consume` and materialises the
 *  completion from the durable blob via `HandleCompletionCodec.tryRead`. The
 *  mailbox is notification-only; these are the production exports C6 added.
 *  There is no `tryJoin` on the projection — reality uses `joinable` + consume. */
// P0-RECOVERY-JOIN-001: recordCompletion requires JoinableCompletion proof.
const ChildRecoveryModule = await prod('Execution/Delegation/Fork/ChildRecovery')
const terminalEvidenceCompleted = member(ChildRecoveryModule, 'TerminalEvidence', 'completed')
const terminalEvidenceFailed = member(ChildRecoveryModule, 'TerminalEvidence', 'failed')
const tryFromProvenTerminal = member(
  ChildRecoveryModule,
  'JoinableCompletion',
  'tryFromProvenTerminal',
)
// Clean-break: tryFromDurableCompleted deleted. Facade keeps a permanent Error
// so RED tests still call the name; production has no weak kind+body proof.
const resolveChild = member(ChildRecoveryModule, 'ChildRecovery', 'resolveChild')
const fromDecoded = member(ChildRecoveryModule, 'JoinableCompletion', 'fromDecoded')
const falseTerminalReplacementAgentId = member(
  ChildRecoveryModule,
  'FalseTerminalMigration',
  'replacementAgentId',
)
const joinReturnedImpliesProofBeforeCommit = member(
  ChildRecoveryModule,
  'ChildRecovery',
  'joinReturnedImpliesProofBeforeCommit',
)

export const childRecovery = (() => {
  const DurableClass =
    ChildRecoveryModule.DurableHandleEvidence ??
    ChildRecoveryModule.ChildRecovery_DurableHandleEvidence
  const SnapshotClass =
    ChildRecoveryModule.ChildSnapshotEvidence ??
    ChildRecoveryModule.ChildRecovery_ChildSnapshotEvidence
  const ObservationClass =
    ChildRecoveryModule.HostObservation ?? ChildRecoveryModule.ChildRecovery_HostObservation
  const JoinTraceClass =
    ChildRecoveryModule.JoinRecoveryTrace ?? ChildRecoveryModule.ChildRecovery_JoinRecoveryTrace
  const FinalityClass =
    ChildRecoveryModule.ChildFinality ?? ChildRecoveryModule.ChildRecovery_ChildFinality

  if (typeof DurableClass !== 'function') throw new Error('ChildRecovery.DurableHandleEvidence missing')
  if (typeof SnapshotClass !== 'function') throw new Error('ChildRecovery.ChildSnapshotEvidence missing')
  if (typeof ObservationClass !== 'function') throw new Error('ChildRecovery.HostObservation missing')
  if (typeof JoinTraceClass !== 'function') throw new Error('ChildRecovery.JoinRecoveryTrace missing')
  if (typeof FinalityClass !== 'function') throw new Error('ChildRecovery.ChildFinality missing')

  const durableOf = unionCase(DurableClass, 'DurableHandleEvidence')
  const snapshotOf = unionCase(SnapshotClass, 'ChildSnapshotEvidence')
  const observationOf = unionCase(ObservationClass, 'HostObservation')
  const joinTraceOf = unionCase(JoinTraceClass, 'JoinRecoveryTrace')
  const finalityOf = unionCase(FinalityClass, 'ChildFinality')

  return {
    durableUnknown: () => durableOf('Unknown', []),
    durableActive: () => durableOf('Active', []),
    durableRetired: () => durableOf('Retired', []),
    durableCompletedAwaitingJoin: (proof) => durableOf('CompletedAwaitingJoin', [proof]),
    durableAbandoned: (reason) => durableOf('Abandoned', [reason]),

    snapshotMissing: () => snapshotOf('Missing', []),
    snapshotActive: () => snapshotOf('Active', []),
    snapshotUnreadable: (reason) => snapshotOf('Unreadable', [reason]),
    snapshotTerminal: (evidence) => snapshotOf('Terminal', [evidence]),

    abortedObserved: (reason) => observationOf('AbortedObserved', [reason]),
    parentCancelled: () => observationOf('ParentCancelled', []),
    recoveryPending: () => observationOf('RecoveryInFlight', []),
    recoveryInFlight: () => observationOf('RecoveryInFlight', []),
    sessionActive: () => observationOf('SessionActive', []),

    evidenceCompleted: (agentId, handle, child, body) =>
      terminalEvidenceCompleted(agentId, handle, child, body),
    evidenceFailed: (agentId, handle, child, body) =>
      terminalEvidenceFailed(agentId, handle, child, body),

    tryFromProvenTerminal: (evidence) => resultOf(tryFromProvenTerminal(evidence)),
    /** Deleted weak proof. Always Error (SendFailure+body is not JoinableCompletion). */
    tryFromDurableCompleted: (_agentId, _handle, _child, _kind, _body) => ({
      ok: false,
      error: 'tryFromDurableCompleted deleted: decode DurableCompletionDecode then fromDecoded',
    }),
    fromDecoded: (agentId, handle, child, decoded, encodedBody) =>
      fromDecoded(agentId, handle, child, decoded, encodedBody),
    replacementAgentId: (originalAgentId, badDigest) =>
      falseTerminalReplacementAgentId(originalAgentId, badDigest),

    resolveChild: (durable, snapshot, observations) =>
      resolveChild(durable, snapshot, toList(observations)),

    /** JoinableCompletion cases — no fromAborted export exists on production module. */
    joinableCompletionExports: () =>
      Object.keys(ChildRecoveryModule).filter(
        (k) => k.includes('JoinableCompletion') || k.includes('fromAborted'),
      ),

    // ── JoinRecoveryTrace (§九) ────────────────────────────────────────────
    finalitySucceeded: (body) => finalityOf('Succeeded', [body]),
    finalityFailed: (body) => finalityOf('Failed', [body]),
    finalityAbandoned: (reason) =>
      finalityOf('Abandoned', [
        typeof reason === 'string' ? handleAbandonReason.of(reason) : reason,
      ]),

    rawAbortObserved: (childSession) => joinTraceOf('RawAbortObserved', [childSession]),
    childRecoveryStarted: (childSession) => joinTraceOf('ChildRecoveryStarted', [childSession]),
    terminalProofIssued: (agentId) => joinTraceOf('TerminalProofIssued', [agentId]),
    handleCompletionCommitted: (agentId) => joinTraceOf('HandleCompletionCommitted', [agentId]),
    joinReturned: (agentId, finality) => joinTraceOf('JoinReturned', [agentId, finality]),

    joinReturnedImpliesProofBeforeCommit: (events) =>
      joinReturnedImpliesProofBeforeCommit(toList(events)),
  }
})()

export const handleController = (() => {
  const m = bind(HandleControllerModule, 'HandleController', [
    'link',
    'recordCompletion',
    'recordAbandon',
    'retire',
    'consume',
    'agentHandle',
  ])

  // Fable erases `option`: Some x = x, None = undefined. Controllers take
  // `AgentJournal option`; pass the instance directly.
  //
  // recordCompletion facade still accepts (agentId, kind, body) for tests; it
  // mints JoinableCompletion via Domain TerminalEvidence (no raw Aborted path).
  return {
    link: async (journal, parentId, agentId, childSessionId, targetAgent, role, ownership = handleOwnership.durableParentHandle()) =>
      resultOf(await m.link(journal, parentId, agentId, childSessionId, targetAgent, role, ownership)),
    recordCompletion: async (journal, parentId, agentId, kind, body, childSessionId) => {
      const kindName = typeof kind === 'string' ? kind : caseOf(kind)
      const content = body === undefined || body === null ? '' : String(body)
      if (content === '') return { ok: false, error: 'proven terminal body must be non-empty' }
      const handle = m.agentHandle(agentId)
      const child =
        childSessionId === undefined || childSessionId === null
          ? sessionId(`fixture-child-${agentId}`)
          : typeof childSessionId === 'string'
            ? sessionId(childSessionId)
            : childSessionId
      let evidence
      if (kindName === 'Terminal') {
        evidence = terminalEvidenceCompleted(agentId, handle, child, content)
      } else if (kindName === 'SendFailure') {
        evidence = terminalEvidenceFailed(agentId, handle, child, content)
      } else {
        return { ok: false, error: 'Cancelled is not joinable under P0-RECOVERY-JOIN-001' }
      }
      const proof = resultOf(tryFromProvenTerminal(evidence))
      if (!proof.ok) return proof
      return resultOf(await m.recordCompletion(journal, parentId, proof.value))
    },
    recordAbandon: async (journal, parentId, agentId, reason, abandonedAt) =>
      resultOf(
        await m.recordAbandon(
          journal,
          parentId,
          agentId,
          typeof reason === 'string' ? handleAbandonReason.of(reason) : reason,
          abandonedAt ?? utcOffset('2026-01-01T00:00:00Z'),
        ),
      ),
    retire: async (journal, parentId, agentId) => resultOf(await m.retire(journal, parentId, agentId)),
    consume: async (journal, parentId, handle) => {
      const value = resultOf(await m.consume(journal, parentId, handle))
      return value.ok ? { ok: true, record: value.value } : { ok: false, error: caseOf(value.error) }
    },
  }
})()

export const handleCompletionCodec = (() => {
  const m = bind(HandleCompletionCodecModule, 'HandleCompletionCodec', [
    'encodeOutcome',
    'tryDecode',
    'tryRead',
    'tryReadBody',
    'decodeBody',
    'tryMaterialiseRunCompletion',
  ])

  return {
    encodeOutcome: (runId, outcome) => m.encodeOutcome(runId, outcome),
    tryDecode: (record, agentId, json, completedAt = utcOffset('2024-01-01T00:00:00.000Z')) =>
      resultOf(m.tryDecode(record, agentId, json, completedAt)),
    tryRead: async (journal, record, agentId, completedAt = utcOffset('2024-01-01T00:00:00.000Z')) => {
      const value = resultOf(await m.tryRead(journal, record, agentId, completedAt))
      return value.ok ? { ok: true, value: unwrapOption(value.value) } : { ok: false, error: value.error }
    },
    tryReadBody: (journal, record) => resultOf(m.tryReadBody(journal, record)),
    decodeBody: (json) => m.decodeBody(json),
    tryMaterialiseRunCompletion: (
      record,
      agentId,
      decoded,
      completedAt = utcOffset('2024-01-01T00:00:00.000Z'),
    ) => m.tryMaterialiseRunCompletion(record, agentId, decoded, completedAt),
    /**
     * Legacy false-abort completion blob (pre-v2). Keys match historical
     * status=aborted plant. Decode → LegacyFalseAbort (never RunCompletion).
     */
    legacyAbortedBody: ({
      runId = 'run-legacy-abort',
      code = 'CANCELLED',
      message = 'host abort observation written as finality',
      childSessionId = '',
    } = {}) =>
      JSON.stringify({
        status: 'aborted',
        run_id: runId,
        code,
        message,
        child_session_id: childSessionId,
      }),
  }
})()

// ── process (docs/what/execution.md) ────────────────────────────────────────────────────────

/** A frozen clock. Deadline takes `unit -> DateTimeOffset`, i.e. a thunk. */
export const clockAt = (iso) => () => utcOffset(iso)

export const deadline = (() => {
  const m = bind(DeadlineModule, 'Deadline', ['MaxTimerWaitMs', 'ofBudget', 'remaining', 'isExpired', 'nextWaitMs'])

  return {
    maxTimerWaitMs: m.MaxTimerWaitMs,
    /** `budgetMs` is milliseconds; Fable represents TimeSpan as a number of ms. */
    ofBudget: (nowIso, budgetMs) => m.ofBudget(utcOffset(nowIso), budgetMs),
    remainingMs: (clock, value) => m.remaining(clock, value),
    isExpired: (clock, value) => m.isExpired(clock, value),
    nextWaitMs: (clock, value) => m.nextWaitMs(clock, value),
  }
})()

/**
 * ITimerPort (VERIFY-004): virtual clock + node port surface via PtyTiming.
 * Delay Task is thenable under Fable; cancel/dispose must leave callbacks unfired.
 */
export const timerPort = (() => {
  const m = bind(PtyTimingModule, 'PtyTiming', [
    'createVirtualTimerPort',
    'nodeTimerPort',
    'timerTask',
  ])

  const asThenable = (task) => {
    if (task == null) return Promise.reject(new Error('timerPort: Delay task is null'))
    if (typeof task.then === 'function') return task
    if (typeof task.ContinueWith === 'function') {
      return new Promise((resolve, reject) => {
        task.ContinueWith((t) => {
          if (t.IsFaulted) reject(t.Exception)
          else resolve(t.Result)
        })
      })
    }
    return Promise.resolve(task)
  }

  const wrapHandle = (handle) => {
    if (handle == null || typeof handle.Cancel !== 'function') {
      throw new Error('timerPort: ITimerHandle missing Cancel')
    }
    const delayTask = handle.Delay ?? handle.delay
    return {
      delay: () => asThenable(delayTask),
      cancel: () => handle.Cancel(),
    }
  }

  const wrapPort = (port) => {
    if (port == null || typeof port.Delay !== 'function' || typeof port.Dispose !== 'function') {
      throw new Error('timerPort: ITimerPort missing Delay/Dispose')
    }
    return {
      delay: (ms) => wrapHandle(port.Delay(ms | 0)),
      dispose: () => port.Dispose(),
    }
  }

  return {
    createVirtual: () => {
      const vt = m.createVirtualTimerPort()
      if (vt == null || vt.Port == null || typeof vt.Advance !== 'function') {
        throw new Error('timerPort: createVirtualTimerPort shape unexpected')
      }
      return {
        /** Wrapped test surface (delay/cancel/dispose). */
        port: wrapPort(vt.Port),
        /** Raw Fable ITimerPort for inject into HostSignalSubscribe.trySubscribe. */
        rawPort: vt.Port,
        advance: (ms) => vt.Advance(ms | 0),
        nowMs: () => (typeof vt.NowMs === 'function' ? vt.NowMs() : vt.NowMs),
      }
    },
    createNode: () => wrapPort(m.nodeTimerPort()),
    /** Fire-and-forget production timerTask (no cancel surface). */
    timerTask: (ms) => asThenable(m.timerTask(ms | 0)),
  }
})()

/**
 * IClockPort (rabbit §15 / G4R-CE S11): virtual wall clock via PtyTiming.createVirtualClockPort.
 * Inject `rawPort` into ChildRecoveryWorkflow.Ports.Clock (and peers).
 */
export const clockPort = (() => {
  const m = bind(PtyTimingModule, 'PtyTiming', ['createVirtualClockPort', 'nodeClockPort'])

  return {
    createVirtual: () => {
      const vc = m.createVirtualClockPort()
      if (vc == null || vc.Port == null || typeof vc.AdvanceMs !== 'function' || typeof vc.Set !== 'function') {
        throw new Error('clockPort: createVirtualClockPort shape unexpected')
      }
      return {
        /** Raw Fable IClockPort for Ports.Clock injection. */
        rawPort: vc.Port,
        advanceMs: (ms) => vc.AdvanceMs(ms | 0),
        set: (value) => vc.Set(value),
        utcNow: () => vc.Port.UtcNow(),
      }
    },
    createNode: () => m.nodeClockPort(),
  }
})()

/** Structural markers for HostSignalSubscribe reconnect + heartbeat (emitJsExpr body). */
export const hostSignalSubscribe = (() => {
  const sourcePath = join(BUILD_ROOT, 'OpenCode/Signals/HostSignalSubscribe.js')
  const trySubscribeFn = bind(HostSignalSubscribeModule, 'HostSignalSubscribe', ['trySubscribe']).trySubscribe
  return {
    source: () => readFileSync(sourcePath, 'utf8'),
    /**
     * @param {object} input plugin input (client / serverUrl / events)
     * @param {(event: unknown) => void} onSignalEvent
     * @param {object} [timerPort] optional raw ITimerPort (vt.Port); Fable Option = null|port
     */
    trySubscribe: (input, onSignalEvent, timerPort) =>
      trySubscribeFn(input, onSignalEvent, timerPort === undefined ? null : timerPort),
    reconnectMarkers: ['2 **', '10000', 'stream ended normally'],
    heartbeatMarkers: [
      'onHeartbeatTimeout',
      'port.Delay',
      'state.heartbeatHandle',
      '.Cancel',
      'state.lastEventMs',
    ],
  }
})()

/**
 * HostForkRunLifecycle: complete claims the run immediately (no Ready gate).
 * markReady is a no-op kept for call-site API shape.
 *
 * Top-level lets compile to NON-curried multi-arg JS functions (length 7–9).
 * Call them with all arguments at once — do not curried-reduce.
 */
export const pendingRunLifecycle = (() => {
  const life = bind(HostForkRunLifecycleModule, 'HostForkRunLifecycle', [
    'complete',
    'markReady',
    'installRun',
    'failRun',
  ])
  const pending = bind(HostPendingRunModule, 'HostPendingRun', ['completionSource'])

  const callMulti = (fn, args) => {
    if (typeof fn !== 'function') {
      throw new Error('pendingRunLifecycle: member is not a function')
    }
    return fn(...args)
  }

  return {
    completionSource: () => pending.completionSource(),
    complete: (...args) => callMulti(life.complete, args),
    markReady: (...args) => callMulti(life.markReady, args),
    installRun: (...args) => callMulti(life.installRun, args),
    failRun: (...args) => callMulti(life.failRun, args),
  }
})()

// ── outcomes ─────────────────────────────────────────────────────────────────

export const outcome = {
  /** EXEC-006: a completed run must carry session-wide A. */
  isValidAgentRunResult: (value) => Outcome.AgentRunResult__get_IsValid(value),
}

// ── introspection, for the facade's own meta-test ────────────────────────────

export const introspect = {
  fableLibraryDir,
  buildRoot: BUILD_ROOT,
  caseNames,
  unions: {
    AgentFact: FactModule.AgentFact,
    Fact: FactModule.Fact,
    RuntimeFact: FactModule.RuntimeFact,
    StreamId: EnvelopeModule.StreamId,
    ReviewGuardVerdict: FactModule.ReviewGuardVerdict,
    PromptAbandonReason: FactModule.PromptAbandonReason,
    HandleCompletionKind: FactModule.HandleCompletionKind,
    JobProgress: OrchestratorProj.JobProgress,
  },
}

// ── CTX-014: diagnostic schema ───────────────────────────────────────────────

export const diagnostic = (() => {
  const m = bind(DiagnosticModule, 'Diagnostic', ['emit', 'fatal'])
  return {
    /** Expected / best-effort — validates whitelist, never prints. */
    emit: (operation, fields) => m.emit(operation, toList(fields)),
    /** Unexpected — prints once then kills process (gated under node:test). */
    fatal: (operation, fields) => m.fatal(operation, toList(fields)),
  }
})()

// ── docs/what/loop.md: LOOP detector + text-delta codec ────────────────────────────────

/**
 * LOOP-003…005: pure exponentially decayed weighted-distinct-token detector.
 * Tokenization uses gpt-tokenizer/o200k_base; Host abort stays out.
 */
export const loopDetector = (() => {
  const m = bind(LoopDetectorModule, 'LoopDetector', [
    'TokenVocabularySize',
    'HalfLife',
    'Lambda',
    'NormalWeightedDistinctCount',
    'TheoreticalLoopWeightedDistinctCount',
    'LoopWeightedDistinctThreshold',
    'create',
    'pushText',
    'evaluate',
  ])

  const read = (evaluation) => ({
    state: caseOf(evaluation.State),
    isLoop: Boolean(evaluation.IsLoop),
    weightedDistinctTokens: evaluation.WeightedDistinctTokenCount,
    step: evaluation.Step,
  })

  return {
    tokenizerVocabularySize: m.TokenVocabularySize,
    halfLife: m.HalfLife,
    lambda: m.Lambda,
    normalWeightedDistinctCount: m.NormalWeightedDistinctCount,
    theoreticalLoopWeightedDistinctCount: m.TheoreticalLoopWeightedDistinctCount,
    loopWeightedDistinctThreshold: m.LoopWeightedDistinctThreshold,
    create: () => m.create(),
    pushText: (detector, text) => read(m.pushText(detector, text)),
    evaluate: (detector) => read(m.evaluate(detector)),
  }
})()

/** LOOP-009: Host raw event → typed text delta, fail closed. */
export const loopEventCodec = (() => {
  const m = bind(LoopEventCodecModule, 'LoopEventCodec', ['isLoopTextDelta', 'tryDecodeTextDelta'])
  return {
    isLoopTextDelta: (raw) => Boolean(m.isLoopTextDelta(raw)),
    tryDecodeTextDelta: (raw) => {
      const decoded = unwrapOption(m.tryDecodeTextDelta(raw))
      if (isNone(decoded)) return undefined
      return {
        sessionId: idValue.session(decoded.SessionId),
        messageId: unwrapOption(decoded.MessageId),
        partId: unwrapOption(decoded.PartId),
        field: unwrapOption(decoded.Field),
        delta: decoded.Delta,
      }
    },
  }
})()

/**
 * LOOP-002/006: edge sensor over Host deltas.
 *
 * Fable emits instance methods as free functions (`LoopSensor__Observe_…`).
 * The facade owns that spelling; tests only see plain methods.
 */
export const loopSensor = (() => {
  const LoopSensor = LoopSensorModule.LoopSensor

  // Fable instance methods emit as free functions with a content hash suffix
  // (`LoopSensor__Observe_4E60E31B`). The hash is not stable across Fable
  // versions, so resolve by prefix once at load time.
  const method = (name) => {
    const prefix = `LoopSensor__${name}_`
    const key = Object.keys(LoopSensorModule).find((entry) => entry.startsWith(prefix))
    if (key === undefined) {
      throw new Error(
        `LoopSensor has no emitted method '${name}'. Available: ${Object.keys(LoopSensorModule)
          .filter((entry) => entry.startsWith('LoopSensor__'))
          .join(', ')}`,
      )
    }
    return LoopSensorModule[key]
  }

  const observe = method('Observe')
  const isArmed = method('IsArmed')
  const tryArm = method('TryArm')
  const clearArmed = method('ClearArmed')
  const dropSession = method('DropSession')
  const resetDetector = method('ResetDetector')

  const textDelta = (session, text) => ({
    type: 'message.part.delta',
    properties: {
      sessionID: session,
      messageID: 'msg_a',
      partID: 'prt_1',
      field: 'text',
      delta: text,
    },
  })

  return {
    /**
     * `owned` is a Set/array of session ids, or a predicate (sessionId) => bool.
     * `abort` receives the session id string and may return a Promise.
     */
    create: ({ owned, abort }) => {
      const owns =
        typeof owned === 'function'
          ? owned
          : (sid) => {
              const value = idValue.session(sid)
              if (owned instanceof Set) return owned.has(value)
              if (Array.isArray(owned)) return owned.includes(value)
              return false
            }

      const abortFn = (sid) => {
        const outcome = abort(idValue.session(sid))
        const asPromise = Promise.resolve(outcome === undefined ? undefined : outcome)
        return asPromise.then(() => okResult(undefined))
      }

      return new LoopSensor(owns, abortFn)
    },

    observe: (sensor, raw) => observe(sensor, raw),
    isArmed: (sensor, session) => Boolean(isArmed(sensor, sessionId(session))),
    tryArm: (sensor, session) => Boolean(tryArm(sensor, sessionId(session))),
    clearArmed: (sensor, session) => clearArmed(sensor, sessionId(session)),
    dropSession: (sensor, session) => dropSession(sensor, sessionId(session)),
    resetDetector: (sensor, session) => resetDetector(sensor, sessionId(session)),
    textDelta,
  }
})()


/** LOOP-006 / FALLBACK continuation prose — loaded from ProviderResources (PROMPT-019). */
export const runtimeNudge = (() => {
  const m = bind(RuntimeNudgeModule, 'RuntimeNudge', [
    'ProviderRetry',
    'LoopContinue',
    'BackgroundJoin',
    'ReviewerVerdictRequired',
    'MissingClosingReport',
    'InteractionContinue',
  ])
  const providerRoot = join(BUILD_ROOT, '..', 'resources/provider')
  const readDoc = (semanticPath) => {
    const raw = readFileSync(join(providerRoot, semanticPath, 'en.md'), 'utf8')
      .replace(/\r\n/g, '\n')
      .replace(/\r/g, '\n')
      .trimEnd()
    return raw.split('\n').map((line) => (line === '' ? '#' : `# ${line}`)).join('\n') + '\n'
  }
  const readLines = (semanticPath) =>
    readFileSync(join(providerRoot, semanticPath, 'en.md'), 'utf8')
      .replace(/\r\n/g, '\n')
      .replace(/\r/g, '\n')
      .trimEnd()
      .split('\n')
  return {
    providerRetry: readDoc(m.ProviderRetry),
    loopContinue: readDoc(m.LoopContinue),
    backgroundJoinGuard: readDoc(m.BackgroundJoin),
    providerRetryInstructions: readLines(m.ProviderRetry),
    loopContinueInstructions: readLines(m.LoopContinue),
    backgroundJoinGuardInstructions: readLines(m.BackgroundJoin),
  }
})()

export const fallbackController = (() => {
  const recordConfirmedFailure = member(FallbackControllerModule, 'FallbackLedger', 'recordConfirmedFailure')

  return {
    /**
     * LOOP-006 bridge half: after LoopKillArmed is observed, the completion path
     * records one confirmed failure. Tests drive that single writer directly.
     */
    recordConfirmedFailure: async (journal, budget, session, run, reason) => {
      const result = resultOf(
        await recordConfirmedFailure(journal, budget, sessionId(session), providerRun(run), reason),
      )
      if (!result.ok) return result
      let outcome = caseOf(result.value)
      if (outcome === 'RecoveryAdvanced') outcome = 'Advanced'
      else if (outcome === 'RecoveryExhausted') outcome = 'Exhausted'
      return { ok: true, outcome }
    },
    mayContinue: (outcomeUnion) => {
      if (typeof outcomeUnion === 'string') {
        return outcomeUnion === 'Advanced' || outcomeUnion === 'RecoveryAdvanced' || outcomeUnion === 'AlreadyRecorded' || outcomeUnion === 'NoActiveRun'
      }
      const tag = outcomeUnion?.tag ?? 0
      return tag === 0 || tag === 2 || tag === 3
    },
  }
})()


// ── Runtime package resources (install once before EnforcerHost / BlogTool / StaticTools) ──

/** Process-local holder: same contract as SpikePlugin init (RuntimeResources.install load). */
export const runtimeResources = (() => {
  const api = bind(RuntimeResourcesModule, 'RuntimeResources', [
    'load',
    'loadFor',
    'install',
    'current',
    'enforcerRulesFor',
  ])
  return {
    load: () => api.load(),
    loadFor: (lang) => api.loadFor(lang),
    install: (resources) => api.install(resources),
    /** Plugin-init equivalent for unit tests that drive EnforcerHost without SpikePlugin. */
    installFromPackage: () => api.install(api.load()),
    current: () => api.current(),
    enforcerRulesFor: (lang) => listItems(api.enforcerRulesFor(lang)),
  }
})()

/**
 * AGENT-002/007: Host-final agent config gate. `configureFromHostConfig` is the
 * plugin's `config` hook body: it validates the 20 managed agents and applies
 * Wanxiangshu-owned `mode` / `permission` / `prompt` fields onto the Host's
 * live config object (never model bindings). Tests observe the writes on the
 * same object the Host's Agent.state will read.
 */
export const managedAgentConfig = (() => {
  const api = bind(ManagedAgentConfigModule, 'ManagedAgentConfig', ['validate', 'configureFromHostConfig'])
  return {
    validate: (config) => resultOf(api.validate(config)),
    /**
     * Runs the full config-hook path (validate + owned-field apply) and returns
     * the gate result. The config object is mutated in place, exactly as the
     * Host's `config` hook contract requires.
     */
    configure: (config) => resultOf(api.configureFromHostConfig(config)),
  }
})()

// ── docs/what/enforcer.md ENFORCER-160/162: 挂起 transform 原语 ────────────────────────────

export const bloggerRequestContext = (() => {
  const build = unionCase(BloggerRequestContextModule.BloggerRequestContext, 'BloggerRequestContext')
  const m = bind(BloggerRequestContextModule, 'BloggerRequestContext', [
    'toml',
    'isMain',
    'requestId',
    'observedPrefixEpoch',
  ])

  const main = ({
    requestId = 'req-main',
    mainSession = 'ses-main',
    bloggerSession = 'ses-blogger',
    toml,
    previousIngested = 0,
    nextIngested = 1,
    previousCutoff = 0,
    nextCutoff = 0,
    nextDigest = '',
    frameEpoch = 0,
    deltaDigest = 'sha-delta',
    observedEpoch = 0,
  } = {}) =>
    build('Main', [
      {
        RequestId: bloggerRequestId(requestId),
        MainSessionId: sessionId(mainSession),
        BloggerSessionId: sessionId(bloggerSession),
        Toml: toml ?? '[[new_work_to_record]]\nuser = "work"',
        // Fable int64 is bigint; a JS number 0 compares unequal to 0n and
        // BlogProjection.applyEntry rejects as IngestCursorMismatch.
        PreviousIngestedThroughSequence: BigInt(previousIngested),
        NextIngestedThroughSequence: BigInt(nextIngested),
        PreviousCoverableTurnCutoffExclusive: previousCutoff,
        NextCoverableTurnCutoffExclusive: nextCutoff,
        NextCoveredPrefixDigest: nextDigest,
        FrameEpochId: frameEpochId(frameEpoch),
        DeltaDigest: blobDigest(deltaDigest),
        ObservedPrefixEpochId: prefixEpochId(observedEpoch),
      },
    ])

  const squash = ({
    requestId = 'req-squash',
    mainSession = 'ses-main',
    bloggerSession = 'ses-blogger',
    frameEpoch = 0,
    coveredFrameCount = 1,
    digests = ['sha-f0'],
    observedEpoch = 0,
  } = {}) =>
    build('Squash', [
      {
        RequestId: bloggerRequestId(requestId),
        MainSessionId: sessionId(mainSession),
        BloggerSessionId: sessionId(bloggerSession),
        FrameEpochId: frameEpochId(frameEpoch),
        CoveredFrameCount: coveredFrameCount,
        FrameDigests: toList(digests.map(blobDigest)),
        ObservedPrefixEpochId: prefixEpochId(observedEpoch),
      },
    ])

  return {
    main,
    squash,
    toml: (ctx) => unwrapOption(m.toml(ctx)),
    isMain: (ctx) => m.isMain(ctx),
    requestId: (ctx) => m.requestId(ctx),
    observedPrefixEpoch: (ctx) => m.observedPrefixEpoch(ctx),
    kindOf: (ctx) => caseOf(ctx),
  }
})()

export const bloggerRuntime = (() => {
  // PR7 Slice 4 D6: BloggerRuntimeState/Cell + transition API deleted.
  // Facade: pure routing + drain helpers. Flight ownership lives on parkedTransform.
  const m = bind(BloggerRuntimeModule, 'BloggerRuntime', [
    'blocksNewRequest',
    'decideMaterial',
    'openDrain',
  ])
  const DrainWindow =
    BloggerRuntimeModule.DrainWindow ?? BloggerRuntimeModule.BloggerRuntime_DrainWindow

  return {
    blocksNewRequest: (durableSealed, hasFlight, drainOpen) =>
      m.blocksNewRequest(durableSealed, hasFlight, drainOpen),
    decideMaterial: (hasParked, hasFlight, ctx) => caseOf(m.decideMaterial(hasParked, hasFlight, ctx)),
    openDrain: (root) => m.openDrain(root),
    /** Physical forceSeal target: DrainWindow.Closed (no cell). */
    closedDrain: () => {
      if (DrainWindow?.Closed !== undefined) return DrainWindow.Closed
      if (typeof DrainWindow === 'function') return new DrainWindow(0, [])
      throw new Error('bloggerRuntime.closedDrain: DrainWindow.Closed missing from dist')
    },
    drainOpenOf: (window) => caseOf(window) === 'Open',
  }
})()

export const parkedTransform = (() => {
  const ParkedTransform = ParkedTransformModule.ParkedTransform
  const PluginRuntimeScope = PluginRuntimeScopeModule.PluginRuntimeScope
  // Fable compiles `member _.ParkedTransformHost` to a module-level getter.
  const getParkedTransformHost =
    PluginRuntimeScopeModule.PluginRuntimeScope__get_ParkedTransformHost

  const entry = (value) => ({
    sessionId: value.SessionId,
    completed: value.Completion,
  })

  const projectContext = (ctx) => {
    if (ctx === undefined || ctx === null) return undefined
    const tag = caseOf(ctx)
    if (tag === 'Main') {
      const main = ctx.fields[0]
      return {
        kind: 'Main',
        toml: main.Toml,
        previousIngested: main.PreviousIngestedThroughSequence,
        nextIngested: main.NextIngestedThroughSequence,
      }
    }
    if (tag === 'Squash') {
      const squash = ctx.fields[0]
      return {
        kind: 'Squash',
        coveredFrameCount: squash.CoveredFrameCount,
      }
    }
    return { kind: tag }
  }

  return {
    /** `lifetimeMs` — Fable represents TimeSpan as a number of ms. */
    create: (sessionId, lifetimeMs) => entry(new ParkedTransform(sessionId, lifetimeMs)),
    resume: (value) => value.TryResume(),
    cancel: (value) => value.TryCancel(),
    scope: () => {
      // Blogger flights are process-shared (SharedState). Isolate each unit-test
      // scope so a prior KEY flight does not leak across PluginRuntimeScope instances.
      SharedStateModule.clearBloggerFlightsForTests()
      return new PluginRuntimeScope(null)
    },
    // The IParkedTransformHost view of a scope (Blogger owner), for call sites
    // whose parameters are typed `IParkedTransformHost` (e.g. handleContinuation).
    host: (scope) => getParkedTransformHost(scope),
    park: (scope, sessionId, lifetimeMs) => getParkedTransformHost(scope).ParkTransform(sessionId, lifetimeMs),
    resumeParked: (scope, sessionId) => getParkedTransformHost(scope).ResumeParked(sessionId),
    cancelParked: (scope, sessionId) => getParkedTransformHost(scope).CancelParked(sessionId),
    setPendingOffer: (scope, sessionId, context) => getParkedTransformHost(scope).SetPendingOffer(sessionId, context),
    // Back-compat alias used by parked-transform tests (PendingOffer path).
    offerParked: (scope, sessionId, context) => getParkedTransformHost(scope).SetPendingOffer(sessionId, context),
    hasParked: (scope, sessionId) => getParkedTransformHost(scope).HasParked(sessionId),
    hasFlight: (scope, sessionId) => getParkedTransformHost(scope).HasFlight(sessionId),
    tryGetFlight: (scope, sessionId) => projectContext(getParkedTransformHost(scope).TryGetFlight(sessionId)),
    consumeStaged: (scope, sessionId) => projectContext(getParkedTransformHost(scope).TryTakePendingOffer(sessionId)),
    setCurrentRequest: (scope, sessionId, context) => getParkedTransformHost(scope).SetCurrentRequest(sessionId, context),
    peekCurrentRequest: (scope, sessionId) => projectContext(getParkedTransformHost(scope).TryPeekCurrentRequest(sessionId)),
    clearCurrentRequest: (scope, sessionId) => getParkedTransformHost(scope).ClearCurrentRequest(sessionId),
    // Physical drain-window slot (PR7 Slice 4 D6: cell dual-write removed).
    getDrainWindow: (scope, sessionId) => getParkedTransformHost(scope).GetDrainWindow(sessionId),
    setDrainWindow: (scope, sessionId, window) => getParkedTransformHost(scope).SetDrainWindow(sessionId, window),
    isDrainOpen: (scope, sessionId) => getParkedTransformHost(scope).IsDrainOpen(sessionId),
    dispose: (scope) => scope.Dispose(),
  }
})()
const SessionRecoveryModule = await prod('Execution/Session/Recovery/Model')

export const sessionRecovery = (() => {
  const authorize = member(SessionRecoveryModule, 'SessionRecovery', 'authorizeFamilyResume')
  const permitRoot = member(SessionRecoveryModule, 'FamilyRecoveryPermit', 'root')

  const RecoveryBlockClass =
    SessionRecoveryModule.RecoveryBlock ?? SessionRecoveryModule.SessionRecovery_RecoveryBlock
  const SessionRecoveryClass =
    SessionRecoveryModule.SessionRecovery ?? SessionRecoveryModule.SessionRecovery_SessionRecovery
  const NonEmptyClass =
    SessionRecoveryModule.NonEmpty$1 ??
    SessionRecoveryModule.SessionRecovery_NonEmpty$1 ??
    SessionRecoveryModule.NonEmpty

  if (typeof RecoveryBlockClass !== 'function') {
    throw new Error('SessionRecovery.RecoveryBlock missing')
  }
  if (typeof SessionRecoveryClass !== 'function') {
    throw new Error('SessionRecovery.SessionRecovery missing')
  }

  const blockOf = unionCase(RecoveryBlockClass, 'RecoveryBlock')
  const recoveryOf = unionCase(SessionRecoveryClass, 'SessionRecovery')

  const nonEmptyOne = (value) => {
    // F# record { Head; Tail } for NonEmpty<'a>
    if (NonEmptyClass && typeof NonEmptyClass === 'function') {
      try {
        return new NonEmptyClass(value, FsList.empty())
      } catch {
        // fall through to plain object shape Fable often uses for records
      }
    }
    return { Head: value, Tail: FsList.empty() }
  }

  const emptyMap = FsMap.empty(ordinalComparer)

  return {
    authorizeFamilyResume: (root, sequence, recovered) => authorize(root, sequence, recovered),
    permitRoot: (permit) => permitRoot(permit),

    snapshotUnreadable: (session, reason) => blockOf('SnapshotUnreadable', [session, reason]),
    childRecoveryFailed: (session, reason) => blockOf('ChildRecoveryFailed', [session, reason]),
    blocked: (block) => recoveryOf('Blocked', [nonEmptyOne(block)]),
    waiting: (block) => recoveryOf('Waiting', [nonEmptyOne(block)]),
    recovered: (session) => recoveryOf('Recovered', [nonEmptyOne(sessionRecovery.childRecoveryFailed(session, 'test'))]),

    recoveredClosure: (root, resultsBySession = {}) => {
      const pairs = Object.entries(resultsBySession).map(([id, outcome]) => [sessionId(id), outcome])
      const results =
        pairs.length === 0
          ? emptyMap
          : FsMap.ofArray(pairs, {
              Compare: (left, right) => {
                const a = idValue.session(left)
                const b = idValue.session(right)
                return a < b ? -1 : a > b ? 1 : 0
              },
            })
      return {
        Closure: {
          Root: root,
          Nodes: FsList.empty(),
          Digest: '',
          JournalSequence: 0n,
        },
        Results: results,
      }
    },


  }
})()