// Shared deterministic temporal proof support.
//
// Production timer, clock, journal, and fold ownership is exposed by the
// registered TemporalSurface. This file only supplies trace composition and
// test-owned completion/provider controls; no production representation leaks
// through the harness.

import { mkdirSync, mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { isAbsolute, join } from 'node:path'

import * as temporal from '../../../../dist/Verification/TemporalSurface.js'

// ── deterministic virtual time ──────────────────────────────────────────────

const wrapTimerHandle = (handle) => ({
  delay: () => temporal.timerAwait(handle),
  cancel: () => temporal.timerCancel(handle),
})

export const createVirtualClock = () => {
  const timer = temporal.createVirtualTimer()
  return {
    port: {
      delay: (milliseconds) => wrapTimerHandle(temporal.timerDelay(timer, milliseconds)),
      dispose: () => temporal.timerDispose(timer),
    },
    advance: (milliseconds) => temporal.timerAdvance(timer, milliseconds),
    nowMs: () => Number(temporal.timerNowMs(timer)),
  }
}

// ── durable temporal world ──────────────────────────────────────────────────

let nextDirectory = 0
const physicalDirectories = new Map()

const logicalDirectory = (value) => {
  if (typeof value === 'string' && value.length > 0) return value
  const generated = `temporal-${nextDirectory}`
  nextDirectory += 1
  return generated
}

const physicalDirectoryFor = (logical) => {
  const existing = physicalDirectories.get(logical)
  if (existing) return existing

  if (isAbsolute(logical)) {
    mkdirSync(logical, { recursive: true })
    physicalDirectories.set(logical, logical)
    return logical
  }

  const physical = mkdtempSync(join(tmpdir(), 'wxs-temporal-'))
  physicalDirectories.set(logical, physical)
  return physical
}

const worldFromJournal = (logical, physical, journal, vt) => ({
  vt,
  journal,
  raw: { commonDir: physical },
  directory: logical,
  dispose: () => temporal.journalDispose(journal),
})

export const createDurableWorld = async (opts = {}) => {
  const directory = logicalDirectory(opts.directory)
  const physical = physicalDirectoryFor(directory)
  const opened = await temporal.openJournal(
    physical,
    opts.runtime ?? 'rt_temporal',
    opts.pid ?? 4242,
    opts.startedAt ?? '2026-01-01T00:00:00Z',
  )
  if (!opened.ok) throw new Error(`createDurableWorld failed: ${opened.error}`)
  return worldFromJournal(directory, physical, opened.journal, createVirtualClock())
}

export const durableSnapshot = (world) => temporal.journalSnapshot(world.journal)
export const persistedEnvelopes = (world) => temporal.journalPersistedEnvelopes(world.journal)

// Crash simulation keeps the durable common-dir and drops only process-local
// timer/journal capabilities. Reopening the same physical store re-integrates
// the canonical projection instead of carrying an in-memory mirror forward.
export const dropEphemeral = async (world, opts = {}) => {
  const directory = world.directory
  if (typeof directory !== 'string' || directory.length === 0) {
    throw new Error('dropEphemeral requires a world with a `directory` (durable store key)')
  }

  const physical = world.raw?.commonDir ?? physicalDirectoryFor(directory)
  try {
    world.dispose?.()
  } catch {
    // Already disposed; recovery remains driven by durable bytes.
  }
  try {
    world.vt?.port?.dispose?.()
  } catch {
    // Already disposed; recovery remains driven by durable bytes.
  }

  const resumed = await temporal.resumeJournal(
    physical,
    opts.runtime ?? 'rt_temporal_recovered',
    opts.pid ?? 4243,
    opts.startedAt ?? '2026-01-01T01:00:00Z',
  )
  if (!resumed.ok) throw new Error(`dropEphemeral resume failed: ${resumed.error}`)
  return worldFromJournal(directory, physical, resumed.journal, createVirtualClock())
}

// ── deterministic completion source ─────────────────────────────────────────

export class DeterministicCompletionSource {
  #pending = []
  #nextId = 0

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

  resolveNext(value) {
    const entry = this.#pending.shift()
    if (!entry) throw new Error('DeterministicCompletionSource: no pending completion to resolve')
    entry.resolve(value)
    return entry.id
  }

  resolveId(id, value) {
    const index = this.#pending.findIndex((entry) => entry.id === id)
    if (index < 0) throw new Error(`DeterministicCompletionSource: no pending completion ${id}`)
    const [entry] = this.#pending.splice(index, 1)
    entry.resolve(value)
    return entry.id
  }

  rejectNext(reason) {
    const entry = this.#pending.shift()
    if (!entry) throw new Error('DeterministicCompletionSource: no pending completion to reject')
    entry.reject(reason)
    return entry.id
  }

  get pendingCount() {
    return this.#pending.length
  }

  pendingPromises() {
    return this.#pending.map((entry) => entry.promise)
  }

  clear() {
    for (const entry of this.#pending.splice(0)) {
      entry.reject(new Error('DeterministicCompletionSource cleared'))
      entry.promise.catch(() => {})
    }
  }
}

// ── deterministic event queue ───────────────────────────────────────────────

export class DeterministicEventQueue {
  #items = []

  enqueue(event) {
    this.#items.push(event)
  }

  enqueueAll(events) {
    for (const event of events) this.#items.push(event)
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

  static interleavings(a, b) {
    const output = []
    const visit = (indexA, indexB, prefix) => {
      if (indexA === a.length && indexB === b.length) {
        output.push([...prefix])
        return
      }
      if (indexA < a.length) {
        prefix.push(a[indexA])
        visit(indexA + 1, indexB, prefix)
        prefix.pop()
      }
      if (indexB < b.length) {
        prefix.push(b[indexB])
        visit(indexA, indexB + 1, prefix)
        prefix.pop()
      }
    }
    visit(0, 0, [])
    return output
  }

  static permutations(items) {
    if (items.length > 6) throw new Error('permutations: cap 6 items to avoid combinatorial explosion')
    if (items.length <= 1) return [[...items]]
    const output = []
    for (let index = 0; index < items.length; index += 1) {
      const rest = [...items.slice(0, index), ...items.slice(index + 1)]
      for (const permutation of DeterministicEventQueue.permutations(rest)) {
        output.push([items[index], ...permutation])
      }
    }
    return output
  }
}

// ── deterministic provider replay ──────────────────────────────────────────

export const createRecordedProviderPort = () => {
  const responses = []
  return {
    enqueue(response, match) {
      responses.push({ response, match })
    },
    async request(prompt) {
      const index = responses.findIndex((entry) => (entry.match ? entry.match(prompt) : true))
      if (index < 0) throw new Error('RecordedProviderPort: no enqueued response for request')
      const [entry] = responses.splice(index, 1)
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

// ── plain trace helpers ─────────────────────────────────────────────────────

export const foldEnvelopes = (envelopes) => temporal.fold(envelopes)

export const ROOT_SELECTION_IDENTITY_SEED = {
  Kind: 'RootSelection',
  OwnerSessionId: null,
  OwnerLogicalRunId: null,
  OwnerAuthorityRootUserMessageId: null,
  ParticipantIdentity: {
    SelectedAgent: 'coder',
    PeerAgent: 'coder',
    Role: 'coder',
    InitialTier: 'Deep',
    Persona: 'Coder',
    PersonaCatalogVersion: 1,
    Origin: 'ResolvedAtRoot',
  },
}

export const envelopesForSession = (
  session,
  facts,
  { startSeq = 1, runtime = 'rt_temporal', runFor, observedAt = '2026-01-01T00:00:00Z' } = {},
) =>
  facts.map((fact, index) => {
    const run = runFor?.(fact, index)
    return {
      runtime,
      seq: startSeq + index,
      observedAt,
      id: `e${startSeq + index}`,
      stream: { kind: 'Session', session },
      ...(run === undefined ? {} : { run }),
      fact,
    }
  })

export const fallbackFacts = {
  authorityRoot: ({ session = 'ses_a', logical = 'run_L', root = 'msg_u1' } = {}) => ({
    family: 'Prompt',
    case: 'AuthorityRootAccepted',
    payload: {
      SchemaVersion: 2,
      SessionId: session,
      LogicalRunId: logical,
      AuthorityRootUserMessageId: root,
      AuthorityKind: 'HumanRoot',
      IdentitySeed: ROOT_SELECTION_IDENTITY_SEED,
    },
  }),

  advance: ({ session = 'ses_a', logical = 'run_L', root = 'msg_u1', run, previous, next, count, reason = 'provider_error' } = {}) => ({
    family: 'Fallback',
    case: 'FallbackCursorAdvanced',
    payload: {
      SessionId: session,
      LogicalRunId: logical,
      AuthorityRootUserMessageId: root,
      ProviderRun: run,
      PreviousOffset: previous,
      NextOffset: next,
      ConsecutiveFailureCount: count,
      Reason: reason,
    },
  }),

  exhausted: ({ session = 'ses_a', logical = 'run_L', root = 'msg_u1', count, offset } = {}) => ({
    family: 'Fallback',
    case: 'FallbackExhausted',
    payload: {
      SessionId: session,
      LogicalRunId: logical,
      AuthorityRootUserMessageId: root,
      FinalConsecutiveFailureCount: count,
      FinalOffset: offset,
    },
  }),
}

export const DurableTraceEvents = {
  appendAgentFact: (stream, run, fact) => ({ kind: 'appendAgentFact', stream, run, fact }),
  advanceClock: (milliseconds) => ({ kind: 'advanceClock', ms: milliseconds }),
}

export const runTrace = async (world, events) => {
  let last = { ok: true }
  for (const event of events) {
    if (event.kind === 'advanceClock') {
      world.vt.advance(event.ms)
      continue
    }
    if (event.kind === 'appendAgentFact') {
      last = await temporal.journalAppendAgent(world.journal, event.stream, event.run, event.fact)
      continue
    }
    throw new Error(`runTrace: unknown event kind '${event.kind}'`)
  }
  return { world, last }
}

export const assertPureConfluence = (envelopeSeqA, envelopeSeqB, projectionReader) => {
  const foldA = temporal.fold(envelopeSeqA)
  const foldB = temporal.fold(envelopeSeqB)
  if (!foldA.ok) throw new Error(`assertPureConfluence: trace A failed to fold: ${JSON.stringify(foldA.error)}`)
  if (!foldB.ok) throw new Error(`assertPureConfluence: trace B failed to fold: ${JSON.stringify(foldB.error)}`)
  const readA = projectionReader(foldA.value)
  const readB = projectionReader(foldB.value)
  return {
    ok: JSON.stringify(readA) === JSON.stringify(readB),
    readA,
    readB,
    foldA: foldA.value,
    foldB: foldB.value,
  }
}

export const nextCEWiringTargets = [
  'ManagerWorkflow.tryObserve',
  'TurnCompletionProgram+FallbackController',
  'ReviewerWorkflow+ReviewController',
  'FinalityController',
  'OrchestratorProgram+SessionRecovery',
]
