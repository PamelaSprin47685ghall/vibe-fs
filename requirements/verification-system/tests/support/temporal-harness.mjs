// Moved from tests/unit/temporal/harness.mjs (cutover Wave 2a) — G4R-1 Temporal Kernel foundation.
// Shared by ≥2 target packages (finality / change-integration / managed-session-lifecycle /
// effect-accounting / provider-attempt-recovery / causal-wait / time-capability), so it lives
// in verification-system/tests/support/ alongside the other shared harness modules.
//
// One World / Pure Time (changes/active/test.md):
//   Race is algebra, not scheduler lottery. Time is input, never authority.
//
// This harness composes DETERMINISTIC ports so a temporal proof can enumerate
// traces (A;B vs B;A) without spawning a real world, a real clock, or a real
// network. Every business rule stays in production; the harness only controls
// when callbacks fire, when completions land, and which durable facts survive a
// crash.
//
// Ports composed here (all production-backed where a production port exists):
//   - VirtualClock        via domain.mjs timerPort.createVirtual (PtyTiming.fs)
//   - InMemory durable    via domain.mjs agentJournal / EventStore (InMemoryGitRawStore + EventStore + EventStoreJournalWriter)
//   - DeterministicCompletionSource  (test-owned, no business logic)
//   - DeterministicEventQueue        (test-owned, explicit queue)
//   - dropEphemeral       (crash / restart: durable stays, ephemeral cleared)
//   - Trace runner        runTrace(world, events) → world' + pure fold helpers
//
// Next CE wiring targets (named here so G4R-2 has a todo list, not a blank):
//   1. ManagerWorkflow.tryObserve        — sessionPort + eventPort + journal → signal wake / fact decide
//   2. TurnCompletionProgram + FallbackController — confirmed failure → FallbackCursorAdvanced / FallbackExhausted
//   3. ReviewerWorkflow + ReviewController        — verdict / witness algebra (ReviewWitness, ReviewGuard)
//   4. FinalityController                         — FinalityReviewCohort roster + FinalityRequest lifecycle
//   5. OrchestratorProgram + SessionRecovery      — publish / conflict / recovery family permit
//
// Design: prefer the pure production fold (Fold.foldEnvelope / FallbackProjection)
// for theorems. Keep CE wiring thin until G4R-2 needs it; this harness never
// re-implements Manager, Reviewer, or Finality business rules.
//
// No second business state machine is present. No wall-clock is read. Every
// time-dependent proof advances the VirtualClock explicitly.

import {
  agentJournal,
  authorityRoot,
  cursor,
  envelope,
  fact,
  fallbackProjection,
  fold,
  idValue,
  logicalRunId,
  providerRun,
  sessionId,
  stream,
  timerPort,
} from './domain.mjs'

// ── VirtualClock ────────────────────────────────────────────────────────────
// Production ITimerPort is Node's setTimeout (PtyTiming.nodeTimerPort).
// Temporal world is VirtualTimerPort (PtyTiming.createVirtualTimerPort):
//   vt.advance(ms) fires due handles synchronously; cancel/dispose drops them.

export const createVirtualClock = () => timerPort.createVirtual()

// ── InMemory durable ───────────────────────────────────────────────────────
// EventStore-backed AgentJournal living on an InMemoryGitRawStore. The store
// is keyed by `directory` (eventStoreRegistry in domain.mjs) so a crash can
// be simulated by disposing the journal and resuming the same store.
// Re-export the production helpers so temporal tests never invent their own
// journal shape.

export const createDurableWorld = async (opts = {}) => {
  const vt = createVirtualClock()
  const created = await agentJournal.create({
    directory: opts.directory,
    runtime: opts.runtime ?? 'rt_temporal',
    pid: opts.pid ?? 4242,
    startedAt: opts.startedAt ?? '2026-01-01T00:00:00Z',
  })
  if (!created.ok) throw new Error(`createDurableWorld failed: ${created.error}`)
  return {
    vt,
    journal: created.journal,
    raw: created.raw,
    directory: opts.directory,
    dispose: created.dispose,
  }
}

// ── DeterministicCompletionSource ───────────────────────────────────────────
// A controlled completion port. Production completions arrive via Task/Promise;
// the temporal world queues them and resolves them in explicit order, so a trace
// can say "completion A before completion B" without racing real Tasks.
// No business logic: it is a bag of resolvers the test drives.

export class DeterministicCompletionSource {
  #pending = [] // { id, resolve, reject, promise }
  #nextId = 0

  /** Enqueue one pending completion. Returns { id, promise, resolve, reject }. */
  enqueue() {
    let resolve
    let reject
    const promise = new Promise((res, rej) => {
      resolve = res
      reject = rej
    })
    const entry = { id: this.#nextId++, resolve, reject, promise }
    this.#pending.push(entry)
    return entry
  }

  /** Resolve the oldest pending completion with `value`. */
  resolveNext(value) {
    const entry = this.#pending.shift()
    if (!entry) throw new Error('DeterministicCompletionSource: no pending completion to resolve')
    entry.resolve(value)
    return entry.id
  }

  /** Resolve a specific id. */
  resolveId(id, value) {
    const idx = this.#pending.findIndex((e) => e.id === id)
    if (idx < 0) throw new Error(`DeterministicCompletionSource: no pending completion ${id}`)
    const [entry] = this.#pending.splice(idx, 1)
    entry.resolve(value)
    return entry.id
  }

  /** Reject the oldest pending completion. */
  rejectNext(reason) {
    const entry = this.#pending.shift()
    if (!entry) throw new Error('DeterministicCompletionSource: no pending completion to reject')
    entry.reject(reason)
    return entry.id
  }

  get pendingCount() {
    return this.#pending.length
  }

  /** Drain all pending promises (for await). */
  pendingPromises() {
    return this.#pending.map((e) => e.promise)
  }

  clear() {
    // Reject rather than leak: a cleared world must not leave pending tasks.
    for (const entry of this.#pending.splice(0)) {
      entry.reject(new Error('DeterministicCompletionSource cleared'))
      entry.promise.catch(() => {})
    }
  }
}

// ── DeterministicEventQueue ─────────────────────────────────────────────────
// Explicit queue. A trace is a sequence of events; this queue makes that
// sequence first-class so a test can enumerate permutations (A;B vs B;A)
// without setTimeout. No business logic: it only orders opaque events.

export class DeterministicEventQueue {
  #items = []

  enqueue(event) {
    this.#items.push(event)
  }

  enqueueAll(events) {
    for (const e of events) this.#items.push(e)
  }

  dequeue() {
    return this.#items.shift()
  }

  peek() {
    return this.#items[0]
  }

  get length() {
    return this.#items.length
  }

  get isEmpty() {
    return this.#items.length === 0
  }

  drain() {
    return this.#items.splice(0)
  }

  toArray() {
    return [...this.#items]
  }

  clear() {
    this.#items.length = 0
  }

  // Exhaustively enumerate distinct interleavings of two independent sequences.
  // Useful for the A/A/B/B style commutativity proofs where two signals race.
  static interleavings(a, b) {
    const out = []
    const rec = (ia, ib, prefix) => {
      if (ia === a.length && ib === b.length) {
        out.push([...prefix])
        return
      }
      if (ia < a.length) {
        prefix.push(a[ia])
        rec(ia + 1, ib, prefix)
        prefix.pop()
      }
      if (ib < b.length) {
        prefix.push(b[ib])
        rec(ia, ib + 1, prefix)
        prefix.pop()
      }
    }
    rec(0, 0, [])
    return out
  }

  // All permutations of one sequence (for A;B == B;A proofs). Caps at 720 (6!).
  static permutations(items) {
    if (items.length > 6) throw new Error('permutations: cap 6 items to avoid combinatorial explosion')
    if (items.length <= 1) return [[...items]]
    const out = []
    for (let i = 0; i < items.length; i++) {
      const rest = [...items.slice(0, i), ...items.slice(i + 1)]
      for (const perm of DeterministicEventQueue.permutations(rest)) {
        out.push([items[i], ...perm])
      }
    }
    return out
  }
}

// ── dropEphemeral ───────────────────────────────────────────────────────────
// Crash simulation: durable facts survive (EventStore tip), ephemeral cells
// disappear. The temporal world resumes from the same `directory` store via
// AgentJournal.createFromProjection (resumeOrCreate). No ephemeral blob or
// revision is carried over except what the durable fold recomputes.
//
// Contract (G4R §12):
//   world1 → durable facts F
//   DROP EPHEMERAL CELLS
//   world2 := recover(F)
//   → no duplicate publish, no retired handle resurrected, etc.

export const dropEphemeral = async (world, opts = {}) => {
  const directory = world.directory
  if (typeof directory !== 'string' || directory.length === 0) {
    throw new Error('dropEphemeral requires a world with a `directory` (durable store key)')
  }
  try {
    world.dispose?.()
  } catch {
    // Already disposed; ignore.
  }
  try {
    world.vt?.port?.dispose?.()
  } catch {
    // Already disposed; ignore.
  }

  const vt2 = createVirtualClock()
  const resumed = await agentJournal.createFromBoot({
    directory,
    runtime: opts.runtime ?? 'rt_temporal_recovered',
    pid: opts.pid ?? 4243,
    startedAt: opts.startedAt ?? '2026-01-01T01:00:00Z',
  })
  if (!resumed.ok) throw new Error(`dropEphemeral resume failed: ${resumed.error}`)
  return {
    vt: vt2,
    journal: resumed.journal,
    raw: resumed.raw ?? world.raw,
    directory,
    dispose: resumed.dispose,
  }
}

// ── RecordedProviderPort (stub / first version) ─────────────────────────────
// Production provider transport is a Host-owned port. Temporal tests use this
// recorded stub so the same business rule can be exercised with a deterministic
// provider answer without a real network. Not a business SM: it just replays
// enqueued responses in order.

export const createRecordedProviderPort = () => {
  const responses = [] // { match?, response }
  return {
    enqueue(response, match) {
      responses.push({ response, match })
    },
    async request(prompt) {
      const idx = responses.findIndex((r) => (r.match ? r.match(prompt) : true))
      if (idx < 0) throw new Error('RecordedProviderPort: no enqueued response for request')
      const [entry] = responses.splice(idx, 1)
      return entry.response
    },
    get pendingCount() {
      return responses.length
    },
    clear() {
      responses.length = 0
    },
  }
}

// ── Trace runner ────────────────────────────────────────────────────────────
// Two levels:
//
//   1. Pure fold level: runPureTrace(envelopes) → Result<Projection,_>
//      Uses production Fold.foldEnvelope. Zero I/O, zero wall clock.
//      This is what confluence / idempotence theorems run on.
//
//   2. Durable level: runTrace(world, events) → world'
//      Appends AgentFacts to the world's AgentJournal via production
//      AgentJournal.appendAgent, then folds from the journal's snapshot so
//      the assertion observes the same bytes the durable store persisted.
//      Time advances are explicit vt.advance calls encoded as events.

/** Fold a sequence of envelopes (JS array or FSharpList) through production Fold. */
export const foldEnvelopes = (envelopes) => {
  // domain.mjs `fold.apply` already converts arrays via toList.
  return fold.apply(fold.empty, envelopes)
}

/** Build numbered envelopes for one session from facts. */
export const envelopesForSession = (sessionIdStr, facts, { startSeq = 1, runtime = 'rt_temporal', runFor } = {}) => {
  const sid = sessionId(sessionIdStr)
  return facts.map((f, i) => envelope({ seq: startSeq + i, stream: stream.session(sid), run: runFor?.(f, i), fact: f }))
}

// Helpers for the Fallback domain used by confluence theorems.

export const fallbackFacts = {
  authorityRoot: ({ session = 'ses_a', logical = 'run_L', root = 'msg_u1', kind = 'HumanRoot' } = {}) =>
    fact('AuthorityRootAccepted', {
      SessionId: sessionId(session),
      LogicalRunId: logicalRunId(logical),
      AuthorityRootUserMessageId: authorityRoot(root),
      AuthorityKind: kind,
      SelectedAgent: 'fast-coder',
      PeerAgent: 'deep-coder',
      CanonicalRole: 'coder',
      SelectedTier: 'fast',
    }),

  advance: ({ session = 'ses_a', logical = 'run_L', root = 'msg_u1', run, previous, next, count, reason = 'provider_error' } = {}) =>
    fact('FallbackCursorAdvanced', {
      SessionId: sessionId(session),
      LogicalRunId: logicalRunId(logical),
      AuthorityRootUserMessageId: authorityRoot(root),
      ProviderRun: providerRun(run),
      PreviousOffset: previous,
      NextOffset: next,
      ConsecutiveFailureCount: count,
      Reason: reason,
    }),

  exhausted: ({ session = 'ses_a', logical = 'run_L', root = 'msg_u1', count, offset } = {}) =>
    fact('FallbackExhausted', {
      SessionId: sessionId(session),
      LogicalRunId: logicalRunId(logical),
      AuthorityRootUserMessageId: authorityRoot(root),
      FinalConsecutiveFailureCount: count,
      FinalOffset: offset,
    }),
}

// Durable trace events. Each event is applied to the world's journal; the
// resulting projection is the durable truth, not an ephemeral mirror.
export const DurableTraceEvents = {
  appendAgentFact: (streamId, runOrNull, agentFactValue) => ({
    kind: 'appendAgentFact',
    stream: streamId,
    run: runOrNull,
    fact: agentFactValue,
  }),
  advanceClock: (ms) => ({ kind: 'advanceClock', ms }),
}

/**
 * Apply a sequence of durable trace events to a world. Returns the world (for
 * chaining) and the last append result. Pure clock advances are applied to
 * vt; appends go through production AgentJournal.appendAgent.
 * The caller observes the durable snapshot via agentJournal.snapshot(world.journal).
 */
export const runTrace = async (world, events) => {
  let last = { ok: true }
  for (const ev of events) {
    if (ev.kind === 'advanceClock') {
      world.vt.advance(ev.ms)
      continue
    }
    if (ev.kind === 'appendAgentFact') {
      const result = await agentJournal.appendAgent(ev.stream, ev.run ?? undefined, ev.fact, world.journal)
      last = result
      // Do not throw on AlreadyObserved / absorbed rejections: the fold
      // absorbs them, and the durable append path returns Ok regardless of
      // whether the projection changed. Only a poisoned / fatal append should
      // stop the trace; those come back as { ok:false } in domain.mjs shape.
      // For harness-level traces, we keep going so confluence can be observed.
      continue
    }
    throw new Error(`runTrace: unknown event kind '${ev.kind}'`)
  }
  return { world, last }
}

// Pure helper: fold an array of envelope-arrays and compare projections.
// Used by confluence theorems to assert A;B == B;A without a journal.
export const assertPureConfluence = (envelopeSeqA, envelopeSeqB, projectionReader) => {
  const foldA = fold.apply(fold.empty, envelopeSeqA)
  const foldB = fold.apply(fold.empty, envelopeSeqB)
  // Both folds must succeed for confluence to be meaningful; if one fails the
  // caller should use assertPrecedence instead.
  if (!foldA.ok) throw new Error(`assertPureConfluence: trace A failed to fold: ${JSON.stringify(foldA.error)}`)
  if (!foldB.ok) throw new Error(`assertPureConfluence: trace B failed to fold: ${JSON.stringify(foldB.error)}`)
  const readA = projectionReader(foldA.value)
  const readB = projectionReader(foldB.value)
  return { ok: JSON.stringify(readA) === JSON.stringify(readB), readA, readB, foldA: foldA.value, foldB: foldB.value }
}

// ── CE owner documentation ──────────────────────────────────────────────────
// The five CE owners this kernel will wrap next (G4R-2). Each is a production
// workflow / controller; the harness will not duplicate their business logic.
//
// 1. ManagerWorkflow.tryObserve
//      src/Wanxiangshu/Mission/Manager/Workflow.fs
//      tryObserve : sessionPort -> eventPort -> journal -> Task<ManagerObservation>
//      Owner of Manager terminal sequencing. Wakes on Host signals; decides on
//      durable facts (Signal wakes; Fact decides).
//
// 2. TurnCompletionProgram + FallbackController
//      src/Wanxiangshu/Application/Reconciliation/TurnCompletionProgram.fs
//      src/Wanxiangshu/Session/FallbackController.fs  (recordConfirmedFailure / mayContinue)
//      Owner of per-turn completion and of the A/A/B/B fallback cursor. Converts
//      a confirmed provider failure into FallbackCursorAdvanced / FallbackExhausted
//      (single writer, FALLBACK-003). Trace theorems will prove A,A,B,A etc.
//      collapse to the same durable cursor.
//
// 3. ReviewerWorkflow + ReviewController
//      src/Wanxiangshu/Application/Review/ReviewerWorkflow.fs
//      Owner of review verdict (REVISE / PERFECT) and witness algebra. The
//      ReviewProjection fold decides whether a cohort member graduated on a
//      confirmed witness — no program-counter field.
//
// 4. FinalityController
//      src/Wanxiangshu/Mission/Finality/OpenCode/Tool.fs
//      src/Wanxiangshu/Journal/FinalityReviewCohort.fs  (rosterOf)
//      Owner of finality request lifecycle and cohort assembly. Trace theorems
//      will prove cohort confluence across races (e.g. cancel-before-sibling-observe).
//
// 5. OrchestratorProgram + SessionRecovery
//      src/Wanxiangshu/Change/Program.fs  (run)
//      src/Wanxiangshu/Execution/Session/Recovery/Model.fs  (authorizeFamilyResume)
//      Owner of publish reconciliation and durable family recovery. Temporal
//      tests will use dropEphemeral to prove no duplicate publish / no
//      resurrected handle after crash.
//
// Until G4R-2, theorems run on the pure folds (FallbackProjection, ManagerLifecycleProjection,
// FinalityReviewCohort, etc.) so no CE is wrapped prematurely and no Domain program-counter
// field is added for tests.

export const nextCEWiringTargets = [
  'ManagerWorkflow.tryObserve',
  'TurnCompletionProgram+FallbackController',
  'ReviewerWorkflow+ReviewController',
  'FinalityController',
  'OrchestratorProgram+SessionRecovery',
]

// Re-exports for ergonomic theorem files.
export { cursor, fallbackProjection, fold, sessionId, logicalRunId, authorityRoot, providerRun }
