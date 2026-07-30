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
//
// Emitted-name rule, applied throughout: when a module shares its name with a
// type in the same file, Fable suffixes the module (`FallbackProjectionModule_`);
// otherwise the members are plain exports. That is the single Fable convention
// this file exists to absorb.

import { existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
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

const [DateOffset, FsMap, FsList, FsResult] = await Promise.all([
  lib('DateOffset.js'),
  lib('Map.js'),
  lib('List.js'),
  lib('Result.js'),
])

const [
  Identity,
  RolesModule,
  FactModule,
  Outcome,
  EnvelopeModule,
  FoldModule,
  FactCodec,
  WriterModule,
  BootModule,
  BlogProj,
  PrefixProj,
  FallbackProj,
  ReviewProj,
  LinkageProj,
  OrchestratorProj,
  AssociationProj,
  Cursor,
  TerminalValidity,
  PrefixCandidateModule,
  RecoverySlotModule,
  CompactionPolicyModule,
  BloggerTomlModule,
  BloggerDeltaModule,
  CompanionPromptModule,
  CompanionIdentityModule,
  CompanionBuilderModule,
  ProbeSelectionModule,
  XPrefixModule,
  AttemptPlannerModule,
  Authority,
  AuthorityRun,
  Witness,
  Challenge,
  ProviderProj,
  DeadlineModule,
  ProcessRequest,
] = await Promise.all([
  prod('Kernel/Identity'),
  prod('Kernel/Roles'),
  prod('Kernel/Fact'),
  prod('Kernel/Outcome'),
  prod('Journal/Envelope'),
  prod('Journal/Fold'),
  prod('Journal/FactCodec'),
  prod('Journal/Writer'),
  prod('Journal/Boot'),
  prod('Journal/BlogProjection'),
  prod('Journal/PrefixEpochProjection'),
  prod('Journal/FallbackProjection'),
  prod('Journal/ReviewProjection'),
  prod('Journal/LinkageProjection'),
  prod('Journal/OrchestratorProjection'),
  prod('Journal/SessionAssociation'),
  prod('Domain/AgentPairCursor'),
  prod('Domain/TerminalValidity'),
  prod('Domain/PrefixCandidate'),
  prod('Domain/RecoverySlot'),
  prod('Domain/HostCompactionPolicy'),
  prod('Domain/BloggerToml'),
  prod('Domain/BloggerDelta'),
  prod('Domain/CompanionPrompt'),
  prod('Domain/CompanionIdentity'),
  prod('Domain/CompanionProjectionBuilder'),
  prod('Domain/PrefixProbeSelection'),
  prod('Domain/XPrefixProjection'),
  prod('Domain/AttemptPlanner'),
  prod('Domain/PromptAuthority'),
  prod('Domain/PromptAuthorityRun'),
  prod('Domain/ReviewWitness'),
  prod('Domain/ReviewChallenge'),
  prod('Domain/ProviderProjection'),
  prod('Process/Deadline'),
  prod('Process/ProcessRequest'),
])

// ── the one Fable naming convention ──────────────────────────────────────────
//
// Fable renders a module member's exported name from two independent facts:
//
//   1. A module below the file's root is prefixed with its own name:
//      `ReviewProjection.empty` emits as `ReviewProjection_empty`.
//   2. That prefix additionally gains a `Module` suffix when a TYPE of the same
//      name is declared in the same file, because the type already owns the plain
//      name: `ReviewRequirementProjection` is both a record and a module, so its
//      members emit as `ReviewRequirementProjectionModule_empty`.
//
// So one file can carry both spellings at once — `ReviewProjection.fs` exports
// `ReviewProjection_empty` and `ReviewRequirementProjectionModule_empty` side by
// side. The prefix is what disambiguates them, which is why `moduleName` is
// always passed explicitly and the unprefixed spelling is tried last.
//
// `member()` absorbs that rule and THROWS when no spelling exists. It is
// deliberately not a `A ?? B` fallback at each call site: a silent alternative
// would let a renamed or deleted production function read as `undefined` and take
// the other branch, which is the class of failure this facade exists to prevent.

const member = (mod, moduleName, name) => {
  const spellings = [`${moduleName}Module_${name}`, `${moduleName}_${name}`, name]
  const found = spellings.find((spelling) => mod[spelling] !== undefined)
  if (found === undefined) {
    const available = Object.keys(mod)
      .filter((key) => key.includes(name) || key.startsWith(moduleName))
      .join(', ')
    throw new Error(
      `${moduleName} exports none of ${spellings.map((s) => `'${s}'`).join(', ')}. Near matches: ${available || '(none)'}`,
    )
  }
  return mod[found]
}

/** Bind a module's members by name, resolved once at load time. */
const bind = (mod, moduleName, names) =>
  Object.fromEntries(names.map((name) => [name, member(mod, moduleName, name)]))

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

/**
 * The same instant carrying a non-zero UTC offset.
 *
 * Only PERSIST-001's byte-stability test needs this: a decoded envelope picks up
 * the READER's local offset, so proving the serialized bytes do not depend on it
 * requires constructing that state deliberately rather than hoping CI runs in a
 * particular timezone.
 */
export const offsetAt = (iso, offsetMinutes) => DateOffset.fromDate(new Date(iso), offsetMinutes * 60_000)

/** The UTC offset a DateTimeOffset carries, in minutes. `0` for a UTC value. */
export const offsetMinutesOf = (value) => DateOffset.offset(value) / 60_000

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

/** F# `Result` → { ok, value } | { ok: false, error }. */
export const resultOf = (value) =>
  caseOf(value) === 'Ok' ? { ok: true, value: payloadOf(value) } : { ok: false, error: payloadOf(value) }

/**
 * Build an F# `Result`, for a test that must HAND one to production.
 *
 * A deferred selector (`AttemptPlanner.plan`'s `selectProbe`) returns a `Result`, so a
 * test has to construct one. Writing `{ tag: 0, fields: [x] }` by hand is the ordinal
 * construction this facade exists to forbid: `Ok` and `Error` are positional, so the
 * two are one typo apart and swapping them yields a plan that looks plausible.
 */
export const okResult = (value) => new FsResult.FSharpResult$2(0, [value])
export const errorResult = (value) => new FsResult.FSharpResult$2(1, [value])

// ── case-name resolution ─────────────────────────────────────────────────────
// A union's tag ordinal is positional and silently shifts when a case is added
// in the middle. Resolving name → ordinal from cases() at load time turns that
// shift into an immediate, named failure.

const caseNames = (unionClass) => Object.create(unionClass.prototype).cases()

/**
 * Construct a union case by NAME.
 *
 * Two emitted shapes, and the difference is invisible at the call site:
 *   - multi-case: `constructor(tag, fields)`
 *   - single-case: `constructor(Item)` — no tag parameter at all
 *
 * Passing `(0, [x])` to a single-case union therefore builds `fields = [0]` and
 * silently discards `x`. Resolving the shape from `cases().length` here is what
 * keeps that mistake out of every test.
 *
 * A name that does not exist throws. That is the whole point: a case renamed in
 * production must fail loudly rather than land on a neighbouring ordinal.
 */
const unionCase = (unionClass, label) => {
  const names = caseNames(unionClass)
  return (caseName, fields = []) => {
    const index = names.indexOf(caseName)
    if (index < 0) throw new Error(`${label} has no case '${caseName}'. Available: ${names.join(', ')}`)
    return names.length === 1 ? new unionClass(fields[0]) : new unionClass(index, fields)
  }
}

const buildAgentFact = unionCase(FactModule.AgentFact, 'AgentFact')
const buildFact = unionCase(FactModule.Fact, 'Fact')
const buildRuntimeFact = unionCase(FactModule.RuntimeFact, 'RuntimeFact')
const buildStream = unionCase(EnvelopeModule.StreamId, 'StreamId')
const buildVerdict = unionCase(FactModule.ReviewGuardVerdict, 'ReviewGuardVerdict')
const buildAbandonReason = unionCase(FactModule.PromptAbandonReason, 'PromptAbandonReason')
const buildCompletionKind = unionCase(FactModule.HandleCompletionKind, 'HandleCompletionKind')

export const agentFactCaseNames = () => caseNames(FactModule.AgentFact)

// ── identity ─────────────────────────────────────────────────────────────────
// PROMPT-001: no generic message id. `role=user` on the wire is a
// PhysicalUserMessageId; the semantic root is an AuthorityRootUserMessageId; a
// `role=assistant` message is a ProviderRunIdentity. The absence of a
// `messageId(...)` helper here is the clause, not an omission.

const idModule = (name) => ({
  create: (value) => Identity[`${name}Module_create`](value),
  value: (id) => Identity[`${name}Module_value`](id),
})

const Ids = {
  runtime: idModule('RuntimeId'),
  session: idModule('SessionId'),
  child: idModule('ChildId'),
  process: idModule('ProcessId'),
  event: idModule('EventId'),
  logicalRun: idModule('LogicalRunId'),
  authorityRoot: idModule('AuthorityRootUserMessageId'),
  physicalUser: idModule('PhysicalUserMessageId'),
  promptKey: idModule('PromptKey'),
  transportReceipt: idModule('TransportReceipt'),
  providerRun: idModule('ProviderRunIdentity'),
  toolCall: idModule('ToolCallId'),
  systemPrompt: idModule('SystemPromptId'),
  reviewBarrier: idModule('ReviewBarrierId'),
  gitTree: idModule('GitTreeHash'),
  sealDigest: idModule('SealDigest'),
  agentHandle: idModule('AgentHandleId'),
  ptyHandle: idModule('PtyHandleId'),
  managerJob: idModule('ManagerJobId'),
  worktreeIdentity: idModule('WorktreeIdentity'),
  worktreePath: idModule('WorktreePath'),
  targetRef: idModule('TargetRef'),
  commit: idModule('CommitHash'),
  blobRef: idModule('BlobRef'),
  blobDigest: idModule('BlobDigest'),
}

export const runtimeId = (v) => Ids.runtime.create(v)
export const sessionId = (v) => Ids.session.create(v)
export const childId = (v) => Ids.child.create(v)
export const processId = (v) => Ids.process.create(v)
export const eventId = (v) => Ids.event.create(v)
export const logicalRunId = (v) => Ids.logicalRun.create(v)
export const authorityRoot = (v) => Ids.authorityRoot.create(v)
export const physicalUser = (v) => Ids.physicalUser.create(v)
export const promptKey = (v) => Ids.promptKey.create(v)
export const transportReceipt = (v) => Ids.transportReceipt.create(v)
export const providerRun = (v) => Ids.providerRun.create(v)
export const toolCallId = (v) => Ids.toolCall.create(v)
export const systemPromptId = (v) => Ids.systemPrompt.create(v)
export const reviewBarrierId = (v) => Ids.reviewBarrier.create(v)
export const gitTreeHash = (v) => Ids.gitTree.create(v)
export const sealDigest = (v) => Ids.sealDigest.create(v)
export const agentHandleId = (v) => Ids.agentHandle.create(v)
export const ptyHandleId = (v) => Ids.ptyHandle.create(v)
export const managerJobId = (v) => Ids.managerJob.create(v)
export const worktreeIdentity = (v) => Ids.worktreeIdentity.create(v)
export const worktreePath = (v) => Ids.worktreePath.create(v)
export const targetRef = (v) => Ids.targetRef.create(v)
export const commitHash = (v) => Ids.commit.create(v)
export const blobRef = (v) => Ids.blobRef.create(v)
export const blobDigest = (v) => Ids.blobDigest.create(v)

// Epoch ids wrap int64, so Fable represents them as BigInt. Taking a JS number
// here and converting once keeps `1` out of every call site — passing a plain
// number where F# expects int64 does not throw, it silently compares unequal.
export const frameEpochId = (value) => Identity.FrameEpochIdModule_create(BigInt(value))
export const prefixEpochId = (value) => Identity.PrefixEpochIdModule_create(BigInt(value))

export const localSeq = (value) => Identity.LocalSeqModule_create(BigInt(value))

export const idValue = Object.fromEntries(
  Object.entries(Ids).map(([name, module]) => [name, module.value]),
)
idValue.localSeq = (id) => Identity.LocalSeqModule_value(id)
idValue.frameEpoch = (id) => Identity.FrameEpochIdModule_value(id)
idValue.prefixEpoch = (id) => Identity.PrefixEpochIdModule_value(id)

/** PROMPT-002: one-way promotion. There is deliberately no inverse. */
export const promoteToAuthorityRoot = (physical) => Identity.PhysicalUserMessageIdModule_promoteToAuthorityRoot(physical)

/** PROMPT-005: is this receipt `accepted-*` shaped. */
export const isAdmissionShaped = (receipt) => Identity.TransportReceiptModule_isAdmissionShaped(receipt)

const buildHandleId = unionCase(Identity.HandleId, 'HandleId')

export const handleId = {
  agent: (value) => buildHandleId('Agent', [agentHandleId(value)]),
  pty: (value) => buildHandleId('Pty', [ptyHandleId(value)]),
  managerJob: (value) => buildHandleId('ManagerJob', [managerJobId(value)]),
  describe: (handle) => Identity.HandleIdModule_describe(handle),
  tryAgent: (handle) => unwrapOption(Identity.HandleIdModule_tryAgent(handle)),
}

export const fallbackAttemptIdentity = {
  dedupeKey: (identity) => Identity.FallbackAttemptIdentityModule_dedupeKey(identity),
}

export const reviewAttemptIdentity = {
  dedupeKey: (identity) => Identity.ReviewAttemptIdentityModule_dedupeKey(identity),
  isDistinctAttempt: (a, b) => Identity.ReviewAttemptIdentityModule_isDistinctAttempt(a, b),
}

// ── facts ────────────────────────────────────────────────────────────────────

export const verdict = {
  perfect: FactModule.ReviewGuardVerdict.Perfect,
  revise: FactModule.ReviewGuardVerdict.Revise,
  of: (name) => buildVerdict(name),
}

export const abandonReason = {
  sendFailed: (error) => buildAbandonReason('SendFailed', [error]),
  unresolvedAfterRecovery: () => buildAbandonReason('UnresolvedAfterRecovery'),
}

export const completionKind = {
  of: (name) => buildCompletionKind(name),
}

/** Build an AgentFact by case name with an anonymous-record payload. */
export const agentFact = (caseName, payload) => buildAgentFact(caseName, [payload])

/** Wrap an AgentFact as the top-level Fact union. */
export const asFact = (inner) => buildFact('Agent', [inner])

/** Convenience: build and wrap in one step. */
export const fact = (caseName, payload) => asFact(agentFact(caseName, payload))

/**
 * `RuntimeStarted`, wrapped as a top-level Fact.
 *
 * Its own helper because PROMPT-011 counts recovery attempts by folding this
 * fact, so a test needs to emit plugin starts without reaching for ordinals.
 */
export const runtimeStartedFact = ({ runtime = 'rt-test', pid = 1, startedAt = '2026-01-01T00:00:00Z' } = {}) =>
  buildFact('Runtime', [
    buildRuntimeFact('RuntimeStarted', [
      { RuntimeId: runtimeId(runtime), ProcessId: pid, StartedAt: utcOffset(startedAt) },
    ]),
  ])

export const stream = {
  workspace: () => buildStream('Workspace'),
  session: (id) => buildStream('Session', [id]),
  child: (id) => buildStream('Child', [id]),
  process: (id) => buildStream('Process', [id]),
}

// ── journal ──────────────────────────────────────────────────────────────────

/**
 * Build an envelope. `seq` counts from 1; `observedAt` is an ISO string so a
 * test never constructs a clock value by hand.
 *
 * `run` is the ProviderRunIdentity this fact was observed during, or omitted for
 * facts belonging to no run (HOST-010). It replaced `TurnId`, which was a third
 * name for the same thing.
 */
export const envelope = ({
  runtime = 'rt-test',
  seq = 1,
  observedAt = '2026-01-01T00:00:00Z',
  stream: streamId,
  run,
  fact: envelopeFact,
}) => ({
  RuntimeId: runtimeId(runtime),
  LocalSeq: localSeq(seq),
  ObservedAt: utcOffset(observedAt),
  EventId: eventId(`e${seq}`),
  Stream: streamId,
  ProviderRun: run === undefined ? undefined : providerRun(run),
  Fact: envelopeFact,
})

// `Envelope` and `Fold` each collide with a type in their own file, but only
// `Envelope` gained the `Module` infix — `Fold`'s members emit as `Fold_*`.
// Resolving both through `bind` means the facade never hard-codes which of the
// two spellings a given file happened to produce.
const Envelopes = bind(EnvelopeModule, 'Envelope', ['serialize', 'deserialize', 'compareSortKey'])
const Folds = bind(FoldModule, 'Fold', ['empty', 'apply', 'foldEnvelope', 'foldAgentFact'])

export const journal = {
  serialize: (env) => Envelopes.serialize(env),
  deserialize: (line) => resultOf(Envelopes.deserialize(line)),
  serializeFact: (value) => FactCodec.serializeFact(value),
  deserializeFact: (json) => resultOf(FactCodec.deserializeFact(json)),
  containsLegacyFallbackFields: (json) => FactCodec.containsLegacyFallbackFields(json),
  pre050MigrationMessage: FactCodec.pre050MigrationMessage,
  compareSortKey: (a, b) => Envelopes.compareSortKey(a, b),
}

export const fold = {
  empty: Folds.empty,

  /** `envelopes` may be a JS array; it is converted to an FSharpList here. */
  apply: (projection, envelopes) => resultOf(Folds.apply(projection, requireList(toList(envelopes), 'fold.apply'))),

  one: (projection, env) => resultOf(Folds.foldEnvelope(projection, env)),

  /** Round-trip through NDJSON, then fold. Proves the persisted shape folds. */
  replay: (envelopes) => {
    const decoded = [...envelopes].map((env) => {
      const result = journal.deserialize(journal.serialize(env))
      if (!result.ok) throw new Error(`envelope did not survive a round trip: ${result.error}`)
      return result.value
    })
    return resultOf(Folds.apply(Folds.empty, toList(decoded)))
  },

  /** Sessions map of a folded projection, keyed by session id string. */
  sessions: (projection) => mapToObject(projection.AgentProjections.Sessions, idValue.session),

  /** One session's bounded projections, or undefined. */
  session: (projection, id) => mapTryFind(sessionId(id), projection.AgentProjections.Sessions),

  orchestrator: (projection) => projection.AgentProjections.Orchestrator,
}

// ── journal on disk (PERSIST-002/004/005, verification layer 2) ───────────────
//
// `JournalWriter` and `Boot` are the only domain modules that touch a real
// filesystem, so they are the only place a layer-2 resource-contract test can
// exist at all. Everything below hands them a fresh temp directory: PERSIST-004
// is about what a partially written file does at startup, and that cannot be
// asserted against an in-memory stand-in without asserting the stand-in instead.
//
// The `.ndjson` filename is `<RuntimeId>.ndjson` and `Boot` re-derives the
// RuntimeId from it, so tests must not choose paths freely — `store()` owns that.

const Writers = bind(WriterModule, 'JournalWriter', ['create'])
const Boots = bind(BootModule, 'Boot', ['boot', 'captureFrontiers', 'kWayMerge'])

const writerMember = (name) => WriterModule[`JournalWriter__${name}`]
const appendTo = writerMember('Append')
const writerPath = writerMember('get_FilePath')
const writerSeq = writerMember('get_LocalSeq')
const writerCommitted = writerMember('get_LastCommittedLocalSeq')
const writerPoisoned = writerMember('get_IsPoisoned')

/**
 * A disposable journal directory.
 *
 * `store.open()` creates the runtime's `.ndjson` and its mandatory first
 * envelope (`RuntimeStarted`) in one step, because production has no way to get
 * a writer without it — `create` uses the `wx` open flag, so a second writer for
 * the same RuntimeId fails rather than reopening.
 *
 * The journal directory is a path INSIDE the temp dir that production creates
 * itself. `mkdtemp` already produces 0700, so creating it here would make the
 * PERSIST-006 assertion pass without production setting any mode at all.
 */
export const journalStore = () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-journal-'))
  const directory = join(base, 'runtimes')
  const opened = []

  /** For corrupt-journal cases there is no writer, so nothing has made the dir. */
  const ensureDirectory = () => {
    if (!existsSync(directory)) mkdirSync(directory, { recursive: true })
  }

  return {
    directory,

    open: ({ runtime = 'rt_1', pid = 4242, startedAt = '2026-01-01T00:00:00Z' } = {}) => {
      const [writer, initEnvelope] = Writers.create(directory, runtimeId(runtime), pid, utcOffset(startedAt))
      opened.push(writer)

      return {
        initEnvelope,
        path: writerPath(writer),

        /** PERSIST-002: `{ committed: true, envelope }` or `{ committed: false, ... }`. */
        append: (streamId, envelopeFact, run) => {
          const result = appendTo(writer, streamId, run === undefined ? undefined : providerRun(run), envelopeFact)
          return caseOf(result) === 'Committed'
            ? { committed: true, envelope: payloadOf(result) }
            : { committed: false, eventId: idValue.event(result.fields[0]), failure: caseOf(result.fields[1]) }
        },

        /** Next LocalSeq to be written, and the last one that reached the file. */
        seq: () => Number(writerSeq(writer)),
        lastCommittedSeq: () => Number(writerCommitted(writer)),
        poisoned: () => writerPoisoned(writer),
        dispose: () => writer.Dispose(),
      }
    },

    /** PERSIST-006 permission bits, as octal strings production actually set. */
    modes: (runtime = 'rt_1') => ({
      directory: (statSync(directory).mode & 0o777).toString(8),
      file: (statSync(join(directory, `${runtime}.ndjson`)).mode & 0o777).toString(8),
    }),

    /** Raw file text, for asserting the NDJSON shape rather than a decoded value. */
    lines: (runtime = 'rt_1') =>
      readFileSync(join(directory, `${runtime}.ndjson`), 'utf8')
        .split('\n')
        .filter((line) => line !== ''),

    /** Write the file directly. The ONLY way to express a corrupt journal. */
    writeRaw: (runtime, text) => {
      ensureDirectory()
      writeFileSync(join(directory, `${runtime}.ndjson`), text)
    },

    files: () => (existsSync(directory) ? readdirSync(directory).sort() : []),

    /** PERSIST-004: `{ envelopes, diagnostics, frontier }`, all plain JS. */
    boot: () => {
      const snapshot = Boots.boot(directory)
      return {
        envelopes: listItems(snapshot.Envelopes),
        diagnostics: listItems(snapshot.Diagnostics),
        frontier: mapToObject(snapshot.Frontier, idValue.runtime),
      }
    },

    frontier: () => mapToObject(Boots.captureFrontiers(directory), idValue.runtime),

    close: () => {
      for (const writer of opened) {
        try {
          writer.Dispose()
        } catch {
          // Already disposed by the test; closing twice is not a failure.
        }
      }
      rmSync(base, { recursive: true, force: true })
    },
  }
}

/** PERSIST-004 merge order across runtime streams, without touching disk. */
export const kWayMerge = (streams) => listItems(Boots.kWayMerge(toList(streams.map((s) => toList(s)))))

// ── fallback (SSOT/04) ───────────────────────────────────────────────────────

export const cursor = {
  initial: Cursor.initial,
  atOffset: (offset) => Cursor.atOffset(offset),
  advance: (offset) => Cursor.advance(offset),
  recordFailure: (value) => Cursor.recordFailure(value),
  recordSuccess: (value) => Cursor.recordSuccess(value),
  side: (offset) => caseOf(Cursor.side(offset)),
  sideSequence: (count) => listItems(Cursor.sideSequence(count)).map(caseOf),
  effectiveAgent: (pair, value) => Cursor.effectiveAgent(pair, value),
  isValidAdvance: (prevOffset, nextOffset, prevCount, nextCount) =>
    Cursor.isValidAdvance(prevOffset, nextOffset, prevCount, nextCount),

  /** CTX-006: is this one of the primed slots (A′ / B′). */
  isRecoverySlot: (offset) => Cursor.isRecoverySlot(offset),
  attemptIdentity: (session, run, root, providerRunId) => Cursor.attemptIdentity(session, run, root, providerRunId),

  /** FALLBACK-005: `MayContinue` | `Exhausted`, with the cursor as payload. */
  recoveryVerdict: (budget, value) => caseOf(Cursor.recoveryVerdict(budget, value)),

  defaultBudget: Cursor.DefaultAutoRecoveryBudget,
}

export const fallbackProjection = (() => {
  const m = bind(FallbackProj, 'FallbackProjection', ['forAuthority', 'applyAdvance', 'mayContinue'])
  return {
    forAuthority: (runId, root) => m.forAuthority(runId, root),
    applyAdvance: (identity, prevOffset, nextOffset, count, current) =>
      resultOf(m.applyAdvance(identity, prevOffset, nextOffset, count, current)),
    mayContinue: (budget, current) => m.mayContinue(budget, current),
  }
})()

// ── failure-driven context recovery (SSOT/12) ────────────────────────────────

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
    'normalizeNewlines',
    'renderString',
    'renderItem',
    'render',
    'byteCount',
  ])
  const buildPart = unionCase(BloggerTomlModule.BloggerDeltaPart, 'BloggerDeltaPart')

  const part = (kind, ...fields) => buildPart(kind, fields)

  return {
    truncationMarker: m.TruncationMarker,
    normalizeNewlines: (text) => m.normalizeNewlines(text),
    renderString: (text) => m.renderString(text),
    renderItem: (item) => m.renderItem(item),
    render: (items) => m.render(toList(items)),
    byteCount: (text) => m.byteCount(text),

    text: (value) => part('TextPart', value),
    reasoning: (value) => part('ReasoningPart', value),
    toolCall: (tool, args) => part('ToolCallPart', tool, args),
    toolResult: (tool, value) => part('ToolResultPart', tool, value),
    imageOmitted: (mediaType) => part('ImageOmitted', mediaType),
    mediaOmitted: (mediaType) => part('MediaOmitted', mediaType),

    item: ({ turn, role, part: p, truncated = false }) => ({
      Turn: turn,
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
        bytes: bloggerToml.byteCount(chunk.Toml),
        itemCount: listItems(chunk.Items).length,
        kinds: listItems(chunk.Items).map((item) => caseOf(item.Part)),
        truncatedFlags: listItems(chunk.Items).map((item) => item.Truncated),
        nextCursor: { turn: chunk.NextCursor.TurnIndex, part: chunk.NextCursor.PartIndex },
        nextCutoff: chunk.NextCoverableTurnCutoffExclusive,
      }
    },
  }
})()

/** COMPANION-004 / COMPANION-010: the fixed prompt text, with no interpolation. */
export const companionPrompt = {
  system: CompanionPromptModule.System,
  normalInstruction: CompanionPromptModule.NormalInstruction,
  squashInstruction: CompanionPromptModule.SquashInstruction,
  memoryPreamble: CompanionPromptModule.CompanionMemoryPreamble,
  memoryBlock: (frozenB) => CompanionPromptModule.companionMemoryBlock(frozenB),
}

/**
 * COMPANION-013: the four synthetic identity formulas.
 *
 * `sha256` is injected so a test can supply a visible, deterministic stand-in and
 * assert on the INPUT the formula composed. Asserting on real hex would only prove
 * the digest is stable, not that the right fields went into it.
 */
export const companionIdentity = {
  sealRoot: (sha256, { session, epoch, cutoff, prefixDigest, frozenDigest }) =>
    CompanionIdentityModule.sealRoot(
      sha256,
      sessionId(session),
      prefixEpochId(epoch),
      cutoff,
      prefixDigest,
      blobDigest(frozenDigest),
    ),

  companionMemoryMessageId: (sha256, seal) => CompanionIdentityModule.companionMemoryMessageId(sha256, seal),

  frameMessageId: (sha256, { blogger, epoch, ordinal, digest }) =>
    CompanionIdentityModule.frameMessageId(sha256, sessionId(blogger), frameEpochId(epoch), ordinal, blobDigest(digest)),

  instructionMessageId: (sha256, { blogger, epoch, kind }) =>
    CompanionIdentityModule.instructionMessageId(sha256, sessionId(blogger), frameEpochId(epoch), kind),
}

/** COMPANION-005 / CTX-012: the Companion's provider-visible message list. */
export const companionProjection = (() => {
  const m = bind(CompanionBuilderModule, 'CompanionProjectionBuilder', ['build', 'isFirstTurnShape'])
  const buildKind = unionCase(CompanionBuilderModule.CompanionRequestKind, 'CompanionRequestKind')

  return {
    normal: buildKind('Normal', []),
    squash: (frameCount) => buildKind('Squash', [frameCount]),

    /**
     * `frames` is `[{ digest, body }]`; `delta` is `{ messageId, toml }` or omitted.
     *
     * The tuple lists are converted here: an F# tuple is a JS array, and a `list` of
     * them still needs `toList` or it folds as empty.
     */
    build: (sha256, { blogger, epoch, kind, frames, delta }) => {
      const plan = m.build(
        sha256,
        sessionId(blogger),
        frameEpochId(epoch),
        kind,
        toList(frames.map((f) => [blobDigest(f.digest), f.body])),
        delta === undefined ? undefined : [delta.messageId, delta.toml],
      )

      const messages = listItems(plan.Messages).map((msg) => ({
        id: msg.MessageId,
        role: msg.Role,
        text: msg.Text,
        physical: msg.IsPhysical,
      }))

      return {
        system: plan.System,
        messages,
        roles: messages.map((msg) => msg.role),
        texts: messages.map((msg) => msg.text),
        physicalFlags: messages.map((msg) => msg.physical),
        isFirstTurnShape: m.isFirstTurnShape(plan),
      }
    },
  }
})()

/**
 * PROMPT-008: which physical request this is, and the two questions it answers.
 *
 * The kinds are built by case NAME. All four are payload-free, so an ordinal-based
 * construction would compile, run, and answer `clearsFailureCountOnSuccess` for the
 * wrong kind — the exact class of silent failure this facade exists to prevent.
 */
export const requestKind = (() => {
  const build = unionCase(PrefixCandidateModule.ProviderRequestKind, 'ProviderRequestKind')
  const m = bind(PrefixCandidateModule, 'ProviderRequestKind', [
    'label',
    'clearsFailureCountOnSuccess',
    'mayCarryProbe',
  ])

  const of = (name) => build(name, [])

  return {
    workMain: of('WorkMain'),
    bloggerMain: of('BloggerMain'),
    bloggerSquash: of('BloggerSquash'),
    interactionRepair: of('InteractionRepair'),
    all: ['WorkMain', 'BloggerMain', 'BloggerSquash', 'InteractionRepair'].map(of),

    nameOf: (kind) => caseOf(kind),
    label: (kind) => m.label(kind),
    clearsFailureCountOnSuccess: (kind) => m.clearsFailureCountOnSuccess(kind),
    mayCarryProbe: (kind) => m.mayCarryProbe(kind),
  }
})()

/**
 * FALLBACK-012 / CTX-006 / CTX-007: the recovery slot's control flow.
 *
 * `arming` is exposed only through the three named constructors. There is
 * deliberately no `armingOf(offset)` here, mirroring the production module: the
 * question "is offset N armed" has no answer, and offering one would let a test
 * assert the parked-cursor bug as correct behaviour.
 */
export const recoverySlot = (() => {
  const m = bind(RecoverySlotModule, 'RecoverySlot', [
    'beginSequence',
    'afterFailureAdvance',
    'afterRestart',
    'isArmed',
    'mayRecover',
    'onSquashOutcome',
    'onMainOutcome',
    'advancesCursor',
    'nextArming',
  ])
  const buildOutcome = unionCase(RecoverySlotModule.AttemptOutcome, 'AttemptOutcome')

  /**
   * Wrap a decision so its name is readable AND the value stays usable.
   *
   * The value is carried through rather than rebuilt from the name: reconstructing a
   * `SlotDecision` from a string would mean re-supplying `CommitMain`'s payload here,
   * so the facade would be guessing what production returned instead of reporting it.
   *
   * `nextArming` is the union VALUE and `nextArmingName` is the string. Both exist
   * because they serve opposite needs: a trace threads the value into the next
   * `mayRecover` call, while an assertion reads the name. Exposing only the name
   * makes the accessor lossy — the caller cannot feed it back — and exposing only the
   * value makes every assertion write `caseOf` itself.
   */
  const decisionOf = (decision) => ({
    name: caseOf(decision),
    clearsFailureCount: caseOf(decision) === 'CommitMain' ? payloadOf(decision) : undefined,
    advancesCursor: m.advancesCursor(decision),
    nextArming: m.nextArming(decision),
    nextArmingName: caseOf(m.nextArming(decision)),
  })

  return {
    beginSequence: m.beginSequence,
    afterFailureAdvance: m.afterFailureAdvance,
    afterRestart: m.afterRestart,

    armingName: (arming) => caseOf(arming),
    isArmed: (arming) => m.isArmed(arming),

    /** CTX-006: arming AND an odd (primed) offset AND material to work with. */
    mayRecover: (arming, offset, hasMaterial) => m.mayRecover(arming, offset, hasMaterial),

    /** `{ name, clearsFailureCount, advancesCursor, nextArming }`. */
    onSquash: (outcome) => decisionOf(m.onSquashOutcome(buildOutcome(outcome, []))),

    onMain: ({ kind, repairSpent = false, outcome }) =>
      decisionOf(m.onMainOutcome(kind, repairSpent, buildOutcome(outcome, []))),
  }
})()

/**
 * HOST-008 / COMPANION-002: the Work ↔ Companion relation.
 *
 * This is what replaced Companion eligibility. There is no `hasCompanion(role)` here
 * and there must never be one: the question is "is this session itself a Companion",
 * not "does this role deserve one".
 */
export const sessionAssociation = (() => {
  const m = bind(AssociationProj, 'SessionAssociationProjection', [
    'empty',
    'tryFind',
    'isCompanion',
    'tryMainSessionOf',
    'tryBloggerOf',
    'link',
    'unlink',
    'describe',
  ])

  return {
    empty: m.empty,

    isCompanion: (id, current) => m.isCompanion(sessionId(id), current),

    mainSessionOf: (id, current) => {
      const main = unwrapOption(m.tryMainSessionOf(sessionId(id), current))
      return isNone(main) ? undefined : idValue.session(main)
    },

    bloggerOf: (id, current) => {
      const blogger = unwrapOption(m.tryBloggerOf(sessionId(id), current))
      return isNone(blogger) ? undefined : idValue.session(blogger)
    },

    /** `{ kind, blogger, parent }`, or undefined when there is no record. */
    entry: (id, current) => {
      const found = unwrapOption(m.tryFind(sessionId(id), current))
      if (isNone(found)) return undefined

      const kind = caseOf(found.Kind)

      return {
        kind,
        mainSessionId: kind === 'CompanionSession' ? idValue.session(payloadOf(found.Kind)) : undefined,
        blogger: isNone(found.BloggerSessionId) ? undefined : idValue.session(found.BloggerSessionId),
        parent: isNone(found.ParentSessionId) ? undefined : idValue.session(found.ParentSessionId),
      }
    },

    /** All session ids in the map, sorted, so a test can assert the whole shape. */
    ids: (current) => mapEntries(current).map(([id]) => idValue.session(id)).sort(),

    link: ({ main, blogger, parent }, current) => {
      const result = resultOf(
        m.link(sessionId(main), sessionId(blogger), parent === undefined ? undefined : sessionId(parent), current),
      )
      return result.ok ? result : { ok: false, error: caseOf(result.error), message: m.describe(result.error) }
    },

    unlink: (main, current) => m.unlink(sessionId(main), current),
  }
})()

/**
 * HOST-006: the prevention layer's required settings and the containment decision.
 *
 * The verdicts carry payloads, so `verdictOf` reports the case name alongside the
 * rendered message — a test asserting only the name would pass while the operator
 * message said nothing useful, and asserting only the message would break on wording.
 */
export const hostCompaction = (() => {
  const m = bind(CompactionPolicyModule, 'HostCompactionPolicy', [
    'requiredSettings',
    'autoContinueEnabled',
    'isContainableCompaction',
    'nextReanchor',
    'judgeFirstTurn',
    'describeVerdict',
  ])

  const settings = listItems(m.requiredSettings).map((setting) => ({
    path: listItems(setting.Path).join('.'),
    required: setting.Required,
    clause: setting.Clause,
    reason: setting.Reason,
    value: setting,
  }))

  const verdictOf = (verdict) => ({
    name: caseOf(verdict),
    message: m.describeVerdict(verdict),
  })

  return {
    settings,
    settingPaths: settings.map((s) => s.path),
    autoContinueEnabled: m.autoContinueEnabled,

    isContainableCompaction: (isCompaction) => m.isContainableCompaction(isCompaction),

    /**
     * `undefined` when every observed compaction has already been reanchored.
     *
     * `alreadyReanchored` is a list of id strings here and becomes the predicate the
     * production signature takes. Production asks a keyed question because the caller
     * holds an indexed projection (PERSIST-008); a test has a handful of ids, so the
     * conversion belongs at this boundary rather than in every test.
     */
    nextReanchor: (observed, alreadyReanchored = []) => {
      const handled = new Set(alreadyReanchored)
      const next = unwrapOption(
        m.nextReanchor(toList(observed.map(providerRun)), (run) => handled.has(idValue.providerRun(run))),
      )
      return isNone(next) ? undefined : idValue.providerRun(next)
    },

    judgeFirstTurn: ({ unavailable, session, pseudoRuns }) =>
      verdictOf(
        m.judgeFirstTurn(
          unavailable === undefined ? undefined : settings.find((s) => s.path === unavailable).value,
          sessionId(session),
          pseudoRuns,
        ),
      ),
  }
})()

/**
 * CTX-011: candidate selection for one recovery slot.
 *
 * `recomputeDigest` is supplied by the test as a plain function, which is the point of
 * the signature: the cutoff proof compares the Companion's recorded digest against a
 * fresh hash of X's CURRENT prefix, so a test can make them agree or disagree without
 * building a transcript.
 */
export const probeSelection = (() => {
  const m = bind(ProbeSelectionModule, 'PrefixProbeSelection', ['select', 'describeNoCandidate'])

  return {
    /**
     * `{ ok: true, probe }` or `{ ok: false, error, message }`.
     *
     * The reason NAME is what a test asserts; `message` is carried so a diagnostic
     * regression is visible too — a refusal whose text says nothing useful is a
     * refusal an operator cannot act on.
     */
    select: ({
      session = 'ses_x',
      committedEpoch,
      committedSnapshot,
      coverableCutoff,
      coveredDigest,
      requestStartCutoff,
      frozenRef = 'blob-frozen',
      frozenDigest = 'frozen-digest',
      recomputeDigest,
      sha256 = (input) => `«${input}»`,
    }) => {
      const result = resultOf(
        m.select(
          sha256,
          sessionId(session),
          prefixEpochId(committedEpoch),
          committedSnapshot,
          coverableCutoff,
          coveredDigest,
          requestStartCutoff,
          blobRef(frozenRef),
          blobDigest(frozenDigest),
          recomputeDigest,
        ),
      )

      if (!result.ok) {
        return { ok: false, error: caseOf(result.error), message: m.describeNoCandidate(result.error) }
      }

      const probe = result.value

      return {
        ok: true,
        probeId: probe.ProbeId,
        basedOnEpoch: idValue.prefixEpoch(probe.BasedOnEpochId),
        candidate: probe.Candidate,
        cutoff: probe.Candidate.CutoffExclusive,
        sealRoot: probe.Candidate.SealRoot,
        syntheticId: probe.Candidate.SyntheticMessageId,
      }
    },
  }
})()

/** AGENT-001: the ten canonical roles and two tiers, by case name. */
export const roles = (() => {
  const buildRole = unionCase(RolesModule.Role, 'Role')
  const buildTier = unionCase(RolesModule.AgentTier, 'AgentTier')

  return {
    of: (name) => buildRole(name, []),
    tier: (name) => buildTier(name, []),
    nameOf: (role) => caseOf(role),
    permissions: (role) => [...RolesModule.Roles_permissions(role)].map(caseOf).sort(),
  }
})()

/**
 * COMPANION-009 / CTX-010: which prefix X sends, as a plan over message positions.
 *
 * `frozenBBody` is supplied by the caller because the snapshot carries a `BlobRef`,
 * never the body (PERSIST-007). Passing it here is the same split `ResolvedPrefixMemory`
 * makes in production: the journal records where the body is, and only a resolved copy
 * reaches the transform boundary.
 */
export const xPrefix = (() => {
  const m = bind(XPrefixModule, 'XPrefixProjection', ['forSnapshot', 'forChoice', 'requiredBlob', 'replacesPrefix'])

  const planOf = (plan) => {
    const memory = unwrapOption(plan.CompanionMemory)

    return {
      dropLeading: plan.DropLeading,
      replacesPrefix: m.replacesPrefix(plan),
      memoryId: isNone(memory) ? undefined : memory[0],
      memoryText: isNone(memory) ? undefined : memory[1],
    }
  }

  return {
    forSnapshot: (snapshot, frozenBBody = '') => planOf(m.forSnapshot(snapshot, frozenBBody)),
    forChoice: (choice, committed, frozenBBody = '') => planOf(m.forChoice(choice, committed, frozenBBody)),

    /** `undefined` when the plan needs no blob read. */
    requiredBlob: (choice, committed) => {
      const ref = unwrapOption(m.requiredBlob(choice, committed))
      return isNone(ref) ? undefined : idValue.blobRef(ref)
    },
  }
})()

/** CTX-010: the two prefix choices, built by case name. */
export const projectionChoice = (() => {
  const build = unionCase(PrefixCandidateModule.XProjectionChoice, 'XProjectionChoice')

  return {
    committed: build('UseCommittedEpoch', []),
    probe: (value) => build('UsePrefixProbe', [value]),
    nameOf: (choice) => caseOf(choice),
  }
})()

/**
 * CTX-010: a `PrefixProbe`, for a test that must hand one to production.
 *
 * Built here rather than as an object literal at each call site so the field names live
 * in one place. A misspelled field on an F# record reaching JS reads as `undefined`
 * rather than failing — the same silent class as the three hazards this facade exists
 * to close.
 */
export const prefixProbe = ({ id = 'probe-1', basedOnEpoch = 0, candidate }) => ({
  ProbeId: id,
  BasedOnEpochId: prefixEpochId(basedOnEpoch),
  Candidate: candidate,
})

/** CTX-011: a `NoCandidateReason`, by case name. Payload-carrying cases take fields. */
export const noCandidateReason = (() => {
  const build = unionCase(ProbeSelectionModule.NoCandidateReason, 'NoCandidateReason')
  return (name, ...fields) => build(name, fields)
})()

/**
 * PROMPT-008: the one call site of `buildAttemptExecutionProfile`.
 *
 * `mayRecover` is passed in rather than derived from a cursor, mirroring production:
 * arming is a control-flow fact of the caller's recovery sequence (FALLBACK-012), and a
 * planner that decided it from an offset would be the parked-cursor bug.
 */
export const attemptPlanner = (() => {
  const m = bind(AttemptPlannerModule, 'AttemptPlanner', ['plan', 'probeOf', 'promotableProbe'])
  const buildOutcome = unionCase(RecoverySlotModule.AttemptOutcome, 'AttemptOutcome')

  /** A complete AuthorityExecutionProfile. Every field is required — PROMPT-002 fixes them all. */
  const authority = ({
    session = 'ses_x',
    run = 'run-1',
    root = 'msg_root',
    kind = rootKind.human,
    selected = 'fast-coder',
    peer = 'deep-coder',
    role = 'Coder',
    tier = 'Fast',
  } = {}) => ({
    SessionId: sessionId(session),
    LogicalRunId: logicalRunId(run),
    AuthorityRootUserMessageId: authorityRoot(root),
    AuthorityKind: kind,
    SelectedAgent: selected,
    PeerAgent: peer,
    CanonicalRole: roles.of(role),
    SelectedTier: roles.tier(tier),
  })

  return {
    authority,

    plan: ({
      authorityProfile = authority(),
      cursor: cursorValue = cursor.initial,
      physical = 'msg_user',
      run = 'msg_assistant',
      origin = promptOrigin.authorityRoot(rootKind.human),
      kind,
      mayRecover = false,
      selectProbe = () => {
        throw new Error('selectProbe must not be called when the slot may not recover')
      },
    }) => {
      const plan = m.plan(
        authorityProfile,
        cursorValue,
        physicalUser(physical),
        providerRun(run),
        origin,
        kind,
        mayRecover,
        selectProbe,
      )

      const noProbeReason = unwrapOption(plan.NoProbeReason)
      const probe = unwrapOption(m.probeOf(plan))

      return {
        value: plan,
        requestKind: caseOf(plan.Profile.RequestKind),
        choice: caseOf(plan.Profile.ProjectionChoice),
        effectiveAgent: plan.Profile.EffectiveAgent,
        canonicalRole: caseOf(plan.Profile.Authority.CanonicalRole),
        toolCapabilities: [...plan.Profile.ToolCapabilitySet].map(caseOf).sort(),
        systemPromptId: idValue.systemPrompt(plan.Profile.SystemPromptId),
        noProbeReason: isNone(noProbeReason) ? undefined : caseOf(noProbeReason),
        probeId: isNone(probe) ? undefined : probe.ProbeId,
      }
    },

    /** CTX-012: `undefined` unless this attempt carried a probe AND produced a usable terminal. */
    promotableProbeId: (plan, outcome) => {
      const probe = unwrapOption(m.promotableProbe(plan.value, buildOutcome(outcome, [])))
      return isNone(probe) ? undefined : probe.ProbeId
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

/**
 * COMPANION-005 / CTX-011: the Companion frame sequence and its coverage.
 *
 * `frame()` builds a `BlogFrame` by kind NAME, never by tag ordinal — inserting a
 * case ahead of `Squash` would otherwise silently turn every squash frame into an
 * entry, and no assertion would notice.
 */
export const blogProjection = (() => {
  const m = bind(BlogProj, 'BlogProjection', [
    'empty',
    'withSeed',
    'frameCount',
    'coverableFrames',
    'squashWidth',
    'applyEntry',
    'applySquash',
    'applyReanchor',
    'hasCoverage',
  ])
  const buildKind = unionCase(BlogProj.BlogFrameKind, 'BlogFrameKind')

  return {
    empty: m.empty,
    withSeed: (seed, state) => m.withSeed(seed, state),
    frameCount: (state) => m.frameCount(state),
    squashWidth: (state) => m.squashWidth(state),
    hasCoverage: (state) => m.hasCoverage(state),
    frameEpochOf: (state) => idValue.frameEpoch(state.FrameEpochId),

    frame: ({ kind, digest, ref }) => ({
      Kind: buildKind(kind, []),
      Digest: blobDigest(digest),
      TextRef: blobRef(ref),
    }),

    frameKinds: (state) => listItems(state.Frames).map((f) => caseOf(f.Kind)),

    cursor: (turn, part) => ({ TurnIndex: turn, PartIndex: part }),

    coverage: (state) => ({
      ingestTurn: state.Coverage.IngestCursor.TurnIndex,
      ingestPart: state.Coverage.IngestCursor.PartIndex,
      cutoff: state.Coverage.CoverableTurnCutoffExclusive,
      digest: state.Coverage.CoveredPrefixDigest,
      coverableFrames: state.Coverage.CoverableFrameCount,
    }),

    /** CTX-011: the frames a probe may build FrozenB from, by kind. */
    coverableFrameKinds: (state) => listItems(m.coverableFrames(state)).map((f) => caseOf(f.Kind)),

    /** Rejections carry payloads; the name alone is what a test asserts on. */
    applyEntry: ({ epoch, previous, next, previousCutoff, nextCutoff, digest, frame }, state) => {
      const result = resultOf(
        m.applyEntry(frameEpochId(epoch), previous, next, previousCutoff, nextCutoff, digest, frame, state),
      )
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    applySquash: ({ previousEpoch, nextEpoch, count, frame }, state) => {
      const result = resultOf(m.applySquash(frameEpochId(previousEpoch), frameEpochId(nextEpoch), count, frame, state))
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    applyReanchor: (state) => m.applyReanchor(state),
  }
})()

/** COMPANION-009 / CTX-012: which X prefix generation is in force. */
export const prefixEpochProjection = (() => {
  const m = bind(PrefixProj, 'PrefixEpochProjection', [
    'empty',
    'applyRebase',
    'applyReanchor',
    'hasSnapshot',
    'isReanchored',
  ])

  return {
    empty: m.empty,
    hasSnapshot: (state) => m.hasSnapshot(state),
    epochOf: (state) => idValue.prefixEpoch(state.EpochId),

    snapshot: ({ ref, digest, cutoff, prefixDigest, sealRoot, syntheticId }) => ({
      FrozenBRef: blobRef(ref),
      FrozenBDigest: blobDigest(digest),
      CutoffExclusive: cutoff,
      CoveredPrefixDigest: prefixDigest,
      SealRoot: sealRoot,
      SyntheticMessageId: syntheticId,
    }),

    applyRebase: ({ previousEpoch, nextEpoch, candidate }, state) => {
      const result = resultOf(m.applyRebase(prefixEpochId(previousEpoch), prefixEpochId(nextEpoch), candidate, state))
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    /**
     * `observedRun` is the compaction pseudo-run being reanchored (HOST-006).
     *
     * Required, not optional: the projection records it so the same compaction cannot
     * be reanchored twice, and a facade default would let a test skip the argument and
     * silently exercise a shape production cannot produce.
     */
    applyReanchor: ({ previousEpoch, nextEpoch, observedRun }, state) => {
      const result = resultOf(
        m.applyReanchor(prefixEpochId(previousEpoch), prefixEpochId(nextEpoch), providerRun(observedRun), state),
      )
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    isReanchored: (run, state) => m.isReanchored(providerRun(run), state),

    reanchoredRuns: (state) => [...state.ReanchoredRuns].map(idValue.providerRun).sort(),
  }
})()

// ── review (SSOT/05) ─────────────────────────────────────────────────────────

export const reviewWitness = {
  isConfirmed: (value) => Witness.ReviewWitnessModule_isConfirmed(value),
  isPerfectPending: (value) => Witness.ReviewWitnessModule_isPerfectPending(value),
  isRevision: (value) => Witness.ReviewWitnessModule_isRevision(value),
  gitTreeHash: (value) => unwrapOption(Witness.ReviewWitnessModule_gitTreeHash(value)),
  confirmedReviewer: (value) => unwrapOption(Witness.ReviewWitnessModule_confirmedReviewer(value)),
  isValidForTree: (tree, value) => Witness.ReviewWitnessModule_isValidForTree(tree, value),
  attemptIdentity: (barrier, witness) => Witness.ReviewWitnessModule_attemptIdentity(barrier, witness),
  isDistinctAttempt: (barrier, a, b) => Witness.ReviewWitnessModule_isDistinctAttempt(barrier, a, b),
  confirm: (barrier, challengeDigest, secondInputDigest, first, second) =>
    unwrapOption(Witness.ReviewWitnessModule_confirm(barrier, challengeDigest, secondInputDigest, first, second)),
  noReview: Witness.ReviewWitness.NoReview,
}

/** REVIEW-003: the fixed challenge, its version, and its digest. */
export const reviewChallenge = {
  text: Challenge.Text,
  textVersion: Challenge.TextVersion,
  contentDigest: (sha256) => Challenge.contentDigest(sha256),
}

export const reviewProjection = (() => {
  const m = bind(ReviewProj, 'ReviewProjection', [
    'empty',
    'startBarrier',
    'applySeal',
    'applyChallengeIssued',
    'applyVerdict',
    'applyConfirmedWitness',
    'hasObservedAttempt',
    'satisfiesGuard',
  ])

  return {
    empty: m.empty,
    startBarrier: (barrier, tree, current) => m.startBarrier(barrier, tree, current),
    applySeal: (seal, current) => m.applySeal(seal, current),
    applyChallengeIssued: (challenge, current) => m.applyChallengeIssued(challenge, current),
    applyVerdict: (attempt, value, current) => resultOf(m.applyVerdict(attempt, value, current)),
    applyConfirmedWitness: (barrier, challengeDigest, secondInputDigest, first, second, current) =>
      resultOf(m.applyConfirmedWitness(barrier, challengeDigest, secondInputDigest, first, second, current)),
    hasObservedAttempt: (attempt, current) => m.hasObservedAttempt(attempt, current),
    satisfiesGuard: (tree, current) => m.satisfiesGuard(tree, current),
  }
})()

export const reviewRequirements = (() => {
  const m = bind(ReviewProj, 'ReviewRequirementProjection', ['empty', 'addRequirement', 'clearOnConfirmation'])

  return {
    empty: m.empty,
    addRequirement: (session, root, current) => m.addRequirement(session, root, current),
    clearOnConfirmation: (run, current) => m.clearOnConfirmation(run, current),
  }
})()

/** VERIFY-007: the two provider projections, and the one-way downgrade. */
export const providerProjection = {
  canonicalVersion: ProviderProj.CanonicalVersion,
  toSemantic: (wire) => ProviderProj.toSemantic(wire),
  renderWire: (wire) => ProviderProj.renderWire(wire),
  renderSemantic: (semantic) => ProviderProj.renderSemantic(semantic),
  isAppendOnlyPrefix: (previous, next) => ProviderProj.isAppendOnlyPrefix(previous, next),
  sealDigest: (sha256, wire) => ProviderProj.sealDigest(sha256, wire),
  toolResultDigest: (sha256, canonical) => ProviderProj.toolResultDigest(sha256, canonical),
  toolResultDigests: (sha256, wire) => listItems(ProviderProj.toolResultDigests(sha256, wire)),
  fixtureKey: (semantic) => ProviderProj.fixtureKey(semantic),
  semanticallyEqual: (a, b) => ProviderProj.semanticallyEqual(a, b),
}

// ── execution handles (SSOT/09) ──────────────────────────────────────────────

export const handleProjection = (() => {
  const m = bind(LinkageProj, 'HandleProjection', [
    'empty',
    'link',
    'complete',
    'retire',
    'tryFind',
    'isRetired',
    'listable',
    'joinable',
    'activeHandles',
    'tryFindByChildSession',
    'linkedChildren',
  ])

  return {
    empty: m.empty,
    link: (handle, child, targetAgent, role, current) => resultOf(m.link(handle, child, targetAgent, role, current)),
    complete: (handle, kind, current) => resultOf(m.complete(handle, kind, current)),
    retire: (handle, current) => resultOf(m.retire(handle, current)),
    tryFind: (handle, current) => unwrapOption(m.tryFind(handle, current)),
    isRetired: (handle, current) => m.isRetired(handle, current),
    listable: (current) => listItems(m.listable(current)),
    joinable: (current) => listItems(m.joinable(current)),
    activeHandles: (current) => listItems(m.activeHandles(current)),
    tryFindByChildSession: (child, current) => unwrapOption(m.tryFindByChildSession(child, current)),
    linkedChildren: (current) => listItems(m.linkedChildren(current)),
    lifecycleOf: (record) => caseOf(record.Lifecycle),
  }
})()

// ── orchestrator (SSOT/06) ───────────────────────────────────────────────────

export const orchestratorProjection = (() => {
  const m = bind(OrchestratorProj, 'OrchestratorProjection', [
    'empty',
    'tryFind',
    'tryFindByManagerSession',
    'activeJobs',
    'createJob',
    'recordProgress',
    'recoveryAction',
  ])

  return {
    empty: m.empty,
    tryFind: (jobId, current) => unwrapOption(m.tryFind(jobId, current)),
    tryFindByManagerSession: (session, current) => unwrapOption(m.tryFindByManagerSession(session, current)),
    activeJobs: (current) => listItems(m.activeJobs(current)),
    createJob: (job, current) => m.createJob(job, current),
    recordProgress: (jobId, progress, current) => m.recordProgress(jobId, progress, current),

    /** ORCH-007: the single recovery action, by case name. */
    recoveryAction: (currentHead, job) => caseOf(m.recoveryAction(currentHead, job)),
    recoveryActionPayload: (currentHead, job) => payloadOf(m.recoveryAction(currentHead, job)),
    progressOf: (job) => caseOf(job.Progress),
  }
})()

export const jobProgress = (() => {
  const build = unionCase(OrchestratorProj.JobProgress, 'JobProgress')
  return { of: (name, payload) => build(name, payload === undefined ? [] : [payload]) }
})()

// ── prompt authority (SSOT/03) ───────────────────────────────────────────────

export const authority = {
  empty: Authority.empty,
  originLabel: (origin) => Authority.originLabel(origin),
  tryParseContinuationKind: (name) => unwrapOption(Authority.tryParseContinuationKind(name)),
  roleLabel: (role) => Authority.roleLabel(role),
  tryParseRole: (name) => unwrapOption(Authority.tryParseRole(name)),
  tierLabel: (tier) => Authority.tierLabel(tier),
  tryParseTier: (name) => unwrapOption(Authority.tryParseTier(name)),

  /** AGENT-002/003. Typed rejection form; `caseOf` the error to name it. */
  parseAgentName: (name) => resultOf(Authority.parseAgentNameTyped(name)),

  stableLogicalRunId: (sha256, runtime, session, root) => Authority.stableLogicalRunId(sha256, runtime, session, root),
  agentPair: (profile) => Authority.agentPair(profile),
  effectiveAgentAt: (profile, offset) => Authority.effectiveAgentAt(profile, offset),
  effectiveAgentFor: (profile, value) => Authority.effectiveAgentFor(profile, value),
  /**
   * PROMPT-011 claim scope. NOT hashed — it is a `\u001f`-joined string, so a test
   * can read the four components it names. Takes no `sha256`; only `derivePromptKey`
   * hashes.
   */
  claimScopeDigest: (session, runId, origin, payloadDigest) =>
    Authority.claimScopeDigest(session, runId, origin, payloadDigest),
  nextClaimSequence: (scope, projection) => Authority.nextClaimSequence(scope, projection),
  derivePromptKey: (...args) => Authority.derivePromptKey(...args),
  repairPayloadDigest: (run, kind) => Authority.repairPayloadDigest(run, kind),
  repairAlreadyClaimed: (...args) => Authority.repairAlreadyClaimed(...args),
  systemPromptIdFor: (role) => Authority.systemPromptIdFor(role),
  buildAttemptExecutionProfile: (...args) => Authority.buildAttemptExecutionProfile(...args),

  allowsTool: (permission, profile) => Authority.allowsTool(permission, profile),

  /** PROMPT-011 bounds. */
  recoveryTailWindow: Authority.RecoveryTailWindow,
  recoveryAttemptBudget: Authority.RecoveryAttemptBudget,
  countRecoveryAttempt: (projection) => Authority.countRecoveryAttempt(projection),
  recoveryBudgetSpent: (claim) => Authority.recoveryBudgetSpent(claim),
}

export const authorityRun = {
  createAuthorityRoot: (sha256, runtime, session, kind, physical, agent) =>
    resultOf(AuthorityRun.createAuthorityRoot(sha256, runtime, session, kind, physical, agent)),
  claimAgentOwnerRoot: (key, session, payloadDigest, agent) =>
    resultOf(AuthorityRun.claimAgentOwnerRoot(key, session, payloadDigest, agent)),
  claimContinuation: (key, session, kind, profile, effectiveAgent, payloadDigest) =>
    AuthorityRun.claimContinuation(key, session, kind, profile, effectiveAgent, payloadDigest),
  registerAuthority: (profile, projection) => AuthorityRun.registerAuthority(profile, projection),
  registerClaim: (claim, projection) => AuthorityRun.registerClaim(claim, projection),
  submitClaim: (key, receipt, projection) => AuthorityRun.submitClaim(key, receipt, projection),
  acceptClaim: (key, physical, projection) => AuthorityRun.acceptClaim(key, physical, projection),
  abandonClaim: (key, projection) => AuthorityRun.abandonClaim(key, projection),
  resolveKnownOrigin: (physical, key, hostCompact, projection) =>
    caseOf(AuthorityRun.resolveKnownOrigin(physical, key, hostCompact, projection)),
}

export const rootKind = {
  human: Authority.RootAuthorityKind.HumanRoot,
  agentOwner: Authority.RootAuthorityKind.AgentOwnerRoot,
}

export const continuationKind = {
  of: (name) => {
    const parsed = unwrapOption(Authority.tryParseContinuationKind(name))
    if (isNone(parsed)) throw new Error(`unknown ContinuationKind '${name}'`)
    return parsed
  },
}

export const promptOrigin = (() => {
  const build = unionCase(Authority.PromptOrigin, 'PromptOrigin')
  return {
    authorityRoot: (kind) => build('AuthorityRoot', [kind]),
    continuation: (kind) => build('Continuation', [kind]),
    hostInternal: Authority.PromptOrigin.HostInternal,
    unknown: Authority.PromptOrigin.UnknownOrigin,
  }
})()

// ── process (SSOT/09) ────────────────────────────────────────────────────────

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
