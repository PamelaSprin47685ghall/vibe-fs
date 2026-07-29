// tests-mjs/domain.mjs — the ONLY file allowed to know Fable's output shape.
//
// VERIFY-008. Production is .fs; layers 1-3 tests are .mjs consuming
// build/next. Fable's emitted names and container shapes are compiler
// artifacts, not domain concepts, so they are confined here — exactly as
// VERIFY-005 confines dynamic Host access to adapters and codecs.
//
// Rules for test files importing this module:
//   - never read a DU `tag` ordinal; use caseOf()
//   - never construct a fact by ordinal; use fact.<name>() which resolves the
//     ordinal from cases() at load time, so a renamed case fails loudly
//   - never build a DateTimeOffset with `new Date(...)`; use utcOffset()
//   - never touch FSharpMap/FSharpList internals; use mapEntries()/listItems()

import { readdirSync } from 'node:fs'
import { join } from 'node:path'

const BUILD_ROOT = new URL('../build/next/', import.meta.url).pathname

// ── locating the emitted library ─────────────────────────────────────────────
// The fable-library directory carries its version (fable-library-js.5.13.0).
// Hardcoding it would make a Fable upgrade look like a test failure.

const FABLE_MODULES = join(BUILD_ROOT, 'fable_modules')

const fableLibraryDir = (() => {
  const candidates = readdirSync(FABLE_MODULES).filter((entry) => entry.startsWith('fable-library-js.'))
  if (candidates.length !== 1) {
    throw new Error(
      `expected exactly one fable-library-js.* in ${FABLE_MODULES}, found: ${candidates.join(', ') || '(none)'}`,
    )
  }
  return join(FABLE_MODULES, candidates[0])
})()

const lib = (name) => import(join(fableLibraryDir, name))
const prod = (name) => import(join(BUILD_ROOT, `${name}.js`))

const [DateOffset, FsMap, FsList] = await Promise.all([lib('DateOffset.js'), lib('Map.js'), lib('List.js')])

const [Identity, FactModule, Outcome, EnvelopeModule, FoldModule, FactCodec, Cursor, Authority, Witness, DeadlineModule] =
  await Promise.all([
    prod('Kernel/Identity'),
    prod('Kernel/Fact'),
    prod('Kernel/Outcome'),
    prod('Journal/Envelope'),
    prod('Journal/Fold'),
    prod('Journal/FactCodec'),
    prod('Domain/AgentPairCursor'),
    prod('Domain/PromptAuthority'),
    prod('Domain/ReviewWitness'),
    prod('Process/Deadline'),
  ])

// ── values that cross the boundary ───────────────────────────────────────────

/**
 * Build a DateTimeOffset the way F# sees one.
 *
 * A bare `new Date(iso)` has no `offset` property, so Fable's compareDates
 * takes its DateTime branch and adds the LOCAL timezone offset. Under a
 * non-UTC TZ this silently shifts every comparison: Deadline.isExpired
 * returns true for a deadline that has not passed. A wrong test that declares
 * a wrong implementation correct is worse than no test, so this is the only
 * sanctioned way to produce a clock value.
 */
export const utcOffset = (iso) => DateOffset.fromDate(new Date(iso), 0)

/** Read a discriminated-union case name. Never compare `tag` in a test. */
export const caseOf = (value) => {
  if (value === null || value === undefined) return undefined
  if (typeof value.cases !== 'function' || typeof value.tag !== 'number') {
    throw new TypeError(`caseOf expects an F# union, received ${JSON.stringify(value)}`)
  }
  return value.cases()[value.tag]
}

/** Union payload. Single-field cases return the field, multi-field the array. */
export const payloadOf = (value) => {
  const fields = value?.fields ?? []
  return fields.length === 1 ? fields[0] : fields
}

/** FSharpMap → [key, value] pairs, insertion-independent (F# maps are sorted). */
export const mapEntries = (map) => [...map]

/** FSharpMap → plain object, for maps keyed by a string-like identity. */
export const mapToObject = (map, keyToString) =>
  Object.fromEntries(mapEntries(map).map(([key, value]) => [keyToString(key), value]))

export const mapCount = (map) => FsMap.count(map)
export const mapTryFind = (key, map) => unwrapOption(FsMap.tryFind(key, map))

/** FSharpList → array. */
export const listItems = (list) => FsList.toArray(list)

/**
 * array → FSharpList.
 *
 * Mandatory whenever F# expects a `list`. Fable's FSharpList__get_IsEmpty tests
 * `xs.tail == null`, and a JS array has no `tail`, so an array reports itself
 * EMPTY: `List.fold` returns the initial state and every subsequent assertion
 * describes an untouched projection. Nothing throws. Verified:
 *
 *   List.fold((a,x) => a+x, 0, [1,2,3])            → 0
 *   List.fold((a,x) => a+x, 0, ofArray([1,2,3]))   → 6
 *
 * Same hazard class as utcOffset: a silently wrong test declares a wrong
 * implementation correct.
 */
export const toList = (items) => (Array.isArray(items) ? FsList.ofArray(items) : items)

/**
 * Guard the post-conversion value. `toList` handles arrays, so anything still
 * lacking FSharpList structure here is a mistake the caller must see — most
 * often a single envelope passed where a sequence was meant, which would
 * otherwise fold nothing and pass.
 */
const requireList = (value, label) => {
  if (value?.tail === undefined && value?.head === undefined) {
    throw new TypeError(
      `${label} expects an envelope sequence (array or FSharpList), received ${JSON.stringify(value)?.slice(0, 80)}`,
    )
  }
  return value
}

/**
 * F# `option` is erased: `None` is undefined, `Some x` is x. Explicit helpers
 * keep that fact out of every assertion.
 */
export const unwrapOption = (value) => value
export const isNone = (value) => value === undefined || value === null
export const isSome = (value) => !isNone(value)

// ── case-name resolution ─────────────────────────────────────────────────────
// A union's tag ordinal is positional and silently shifts when a case is added
// in the middle. Resolving name → ordinal from cases() at load time turns that
// shift into an immediate, named failure.

const caseIndexer = (unionClass, label) => {
  const probe = Object.create(unionClass.prototype)
  const names = probe.cases()
  return (caseName) => {
    const index = names.indexOf(caseName)
    if (index < 0) {
      throw new Error(`${label} has no case '${caseName}'. Available: ${names.join(', ')}`)
    }
    return index
  }
}

const agentFactCase = caseIndexer(FactModule.AgentFact, 'AgentFact')
const factCase = caseIndexer(FactModule.Fact, 'Fact')
const streamCase = caseIndexer(EnvelopeModule.StreamId, 'StreamId')
const verdictCase = caseIndexer(FactModule.ReviewGuardVerdict, 'ReviewGuardVerdict')

export const agentFactCaseNames = () => Object.create(FactModule.AgentFact.prototype).cases()

// ── identity ─────────────────────────────────────────────────────────────────

export const sessionId = (value) => Identity.SessionIdModule_create(value)
export const messageId = (value) => Identity.MessageIdModule_create(value)
export const runtimeId = (value) => Identity.RuntimeIdModule_create(value)
export const eventId = (value) => Identity.EventIdModule_create(value)
export const childId = (value) => Identity.ChildIdModule_create(value)
export const processId = (value) => Identity.ProcessIdModule_create(value)
export const dispatchId = (value) => Identity.DispatchIdModule_create(value)
export const promptKeyRef = (value) => Identity.PromptKeyRefModule_create(value)
export const localSeq = (value) => Identity.LocalSeqModule_create(BigInt(value))
export const reviewBarrierId = (value) => Witness.ReviewBarrierIdModule_create(value)

export const idValue = {
  session: (id) => Identity.SessionIdModule_value(id),
  message: (id) => Identity.MessageIdModule_value(id),
  runtime: (id) => Identity.RuntimeIdModule_value(id),
  event: (id) => Identity.EventIdModule_value(id),
  child: (id) => Identity.ChildIdModule_value(id),
  process: (id) => Identity.ProcessIdModule_value(id),
  dispatch: (id) => Identity.DispatchIdModule_value(id),
  promptKeyRef: (id) => Identity.PromptKeyRefModule_value(id),
  localSeq: (id) => Identity.LocalSeqModule_value(id),
  reviewBarrier: (id) => Witness.ReviewBarrierIdModule_value(id),
}

// ── facts ────────────────────────────────────────────────────────────────────

export const verdict = {
  perfect: FactModule.ReviewGuardVerdict.Perfect,
  revise: FactModule.ReviewGuardVerdict.Revise,
  of: (name) => new FactModule.ReviewGuardVerdict(verdictCase(name), []),
}

/** Build an AgentFact by case name with an anonymous-record payload. */
export const agentFact = (caseName, payload) =>
  new FactModule.AgentFact(agentFactCase(caseName), [payload])

/** Wrap an AgentFact as the top-level Fact union. */
export const asFact = (inner) => new FactModule.Fact(factCase('Agent'), [inner])

/** Convenience: build and wrap in one step. */
export const fact = (caseName, payload) => asFact(agentFact(caseName, payload))

export const stream = {
  workspace: () => new EnvelopeModule.StreamId(streamCase('Workspace'), []),
  session: (id) => new EnvelopeModule.StreamId(streamCase('Session'), [id]),
  child: (id) => new EnvelopeModule.StreamId(streamCase('Child'), [id]),
  process: (id) => new EnvelopeModule.StreamId(streamCase('Process'), [id]),
}

// ── journal ──────────────────────────────────────────────────────────────────

/**
 * Build an envelope. `seq` counts from 1; `observedAt` is an ISO string so a
 * test never constructs a clock value by hand.
 */
export const envelope = ({
  runtime = 'rt-test',
  seq = 1,
  observedAt = '2026-01-01T00:00:00Z',
  stream: streamId,
  turn,
  fact: envelopeFact,
}) => ({
  RuntimeId: runtimeId(runtime),
  LocalSeq: localSeq(seq),
  ObservedAt: utcOffset(observedAt),
  EventId: eventId(`e${seq}`),
  Stream: streamId,
  TurnId: turn,
  Fact: envelopeFact,
})

export const journal = {
  serialize: (env) => EnvelopeModule.EnvelopeModule_serialize(env),

  /** Decode one NDJSON line. Returns { ok, value } or { ok: false, error }. */
  deserialize: (line) => {
    const result = EnvelopeModule.EnvelopeModule_deserialize(line)
    return caseOf(result) === 'Ok'
      ? { ok: true, value: payloadOf(result) }
      : { ok: false, error: payloadOf(result) }
  },

  serializeFact: (value) => FactCodec.serializeFact(value),

  deserializeFact: (json) => {
    const result = FactCodec.deserializeFact(json)
    return caseOf(result) === 'Ok'
      ? { ok: true, value: payloadOf(result) }
      : { ok: false, error: payloadOf(result) }
  },

  containsLegacyFallbackFields: (json) => FactCodec.containsLegacyFallbackFields(json),
  pre050MigrationMessage: FactCodec.pre050MigrationMessage,
  compareSortKey: (a, b) => EnvelopeModule.EnvelopeModule_compareSortKey(a, b),
}

export const fold = {
  empty: FoldModule.empty,

  /** `envelopes` may be a JS array; it is converted to an FSharpList here. */
  apply: (projection, envelopes) => FoldModule.apply(projection, requireList(toList(envelopes), 'fold.apply')),

  /** Round-trip through NDJSON, then fold. Proves the persisted shape folds. */
  replay: (envelopes) => {
    const decoded = [...envelopes].map((env) => {
      const result = journal.deserialize(journal.serialize(env))
      if (!result.ok) throw new Error(`envelope did not survive a round trip: ${result.error}`)
      return result.value
    })
    return FoldModule.apply(FoldModule.empty, toList(decoded))
  },

  /** Sessions map of a folded projection, keyed by session id string. */
  sessions: (projection) => mapToObject(projection.AgentProjections.Sessions, idValue.session),
}

// ── fallback cursor ──────────────────────────────────────────────────────────

export const cursor = {
  initial: Cursor.initial,
  atOffset: (offset) => Cursor.atOffset(offset),
  advance: (offset) => Cursor.advance(offset),
  advanceCursor: (value, providerAttempt) => Cursor.advanceCursor(value, BigInt(providerAttempt)),
  side: (offset) => caseOf(Cursor.side(offset)),
  sideSequence: (count) => listItems(Cursor.sideSequence(count)).map(caseOf),
  effectiveAgent: (pair, value) => Cursor.effectiveAgent(pair, value),
  attemptIdentity: (logicalRunId, authorityRoot, providerAttempt) =>
    Cursor.attemptIdentity(logicalRunId, authorityRoot, providerAttempt),
  failureIdentity: (identity) => Cursor.failureIdentity(identity),
}

// ── prompt authority ─────────────────────────────────────────────────────────

export const authority = {
  empty: Authority.empty,
  newPromptKey: (...args) => Authority.newPromptKey(...args),
  stableLogicalRunId: (...args) => Authority.stableLogicalRunId(...args),
  parseAgentName: (name) => Authority.parseAgentName(name),
  tryParseRole: (name) => Authority.tryParseRole(name),
  tryParseTier: (name) => Authority.tryParseTier(name),
  agentPair: (...args) => Authority.agentPair(...args),
  effectiveAgentAt: (...args) => Authority.effectiveAgentAt(...args),
  roleLabel: (role) => Authority.roleLabel(role),
  tierLabel: (tier) => Authority.tierLabel(tier),
  originLabel: (origin) => Authority.originLabel(origin),
  repairIdentity: (...args) => Authority.repairIdentity(...args),
}

// ── review witness ───────────────────────────────────────────────────────────

export const reviewWitness = {
  isConfirmed: (value) => Witness.ReviewWitnessModule_isConfirmed(value),
  isPerfectPending: (value) => Witness.ReviewWitnessModule_isPerfectPending(value),
  isRevision: (value) => Witness.ReviewWitnessModule_isRevision(value),
  isDistinct: (a, b) => Witness.ReviewWitnessModule_isDistinctWitness(a, b),
  canConfirm: (...args) => Witness.ReviewWitnessModule_canConfirm(...args),
  gitTreeHash: (value) => Witness.ReviewWitnessModule_getGitTreeHash(value),
  invalidateByTreeChange: (...args) => Witness.ReviewWitnessModule_invalidateByTreeChange(...args),
}

// ── process deadline ─────────────────────────────────────────────────────────

/** A frozen clock. Deadline takes `unit -> DateTimeOffset`, i.e. a thunk. */
export const clockAt = (iso) => () => utcOffset(iso)

export const deadline = {
  /** `budgetMs` is milliseconds; Fable represents TimeSpan as a number of ms. */
  ofBudget: (nowIso, budgetMs) => DeadlineModule.DeadlineModule_ofBudget(utcOffset(nowIso), budgetMs),
  remainingMs: (clock, value) => DeadlineModule.DeadlineModule_remaining(clock, value),
  isExpired: (clock, value) => DeadlineModule.DeadlineModule_isExpired(clock, value),
  nextWaitMs: (clock, value) => DeadlineModule.DeadlineModule_nextWaitMs(clock, value),
}

// ── outcomes ─────────────────────────────────────────────────────────────────

export const outcome = {
  isValidAgentRunResult: (value) => Outcome.AgentRunResult__get_IsValid(value),
}

// ── introspection, for the facade's own meta-test ────────────────────────────

export const introspect = {
  fableLibraryDir,
  buildRoot: BUILD_ROOT,
}
