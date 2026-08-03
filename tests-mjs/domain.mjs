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

const [DateOffset, FsMap, FsList, FsResult, FsSet, AsyncBuilder] = await Promise.all([
  lib('DateOffset.js'),
  lib('Map.js'),
  lib('List.js'),
  lib('Result.js'),
  lib('Set.js'),
  lib('AsyncBuilder.js'),
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
  DiagnosticModule,
  ProjectionModule,
  SyntheticTomlModule,
  ToolResultBoundModule,
  ForkChildPayloadModule,
  BloggerTomlModule,
  BloggerDeltaModule,
  CompanionPromptModule,
  CompanionIdentityModule,
  CompanionBuilderModule,
  ProbeSelectionModule,
  XPrefixModule,
  AttemptPlannerModule,
  ExecutorSummarize,
  Authority,
  AuthorityRun,
  Witness,
  Challenge,
  ProviderProj,
  XTraceModule,
  LifecycleWorkRecordModule,
  ManagedAgentCatalogModule,
  XTraceCaptureModule,
  HostMessageCodecModule,
  DeadlineModule,
  ProcessRequest,
  FlowModule,
  OrchestratorRuntime,
  OrchestratorTypes,
  StrengthTypesModule,
  StrengthPredictorModule,
  StrengthControllerModule,
  StrengthValueModule,
  EnforcerCatalogModule,
  EnforcerCodecModule,
  EnforcerThrottleModule,
  EnforcerNudgeModule,
  EnforcerCycleModule,
  StudentTeacherModule,
  BloggerRequestContextModule,
  BloggerRuntimeModule,
  ParkedTransformModule,
  PluginRuntimeScopeModule,
  AgentJournalModule,
  PromptDispatcherModule,
  PromptDispatcherSendModule,
  HostEventCodecModule,
  HandleControllerModule,
  HandleCompletionCodecModule,
  ReviewSealModule,
  SessionSnapshotPortModule,
  ToolHostCodecModule,
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
  prod('Infrastructure/OpenCode/Host/Diagnostic'),
  prod('Infrastructure/OpenCode/Codec/Projection'),
  prod('Domain/SyntheticToml'),
  prod('Domain/ToolResultBound'),
  prod('Domain/ForkChildPayload'),
  prod('Domain/BloggerToml'),
  prod('Domain/BloggerDelta'),
  prod('Domain/CompanionPrompt'),
  prod('Domain/CompanionIdentity'),
  prod('Domain/CompanionProjectionBuilder'),
  prod('Domain/PrefixProbeSelection'),
  prod('Domain/XPrefixProjection'),
  prod('Domain/AttemptPlanner'),
  prod('Infrastructure/OpenCode/Tools/ExecutorSummarize'),
  prod('Domain/PromptAuthority'),
  prod('Domain/PromptAuthorityRun'),
  prod('Domain/ReviewWitness'),
  prod('Domain/ReviewChallenge'),
  prod('Domain/ProviderProjection'),
  prod('Domain/XTrace'),
  prod('Domain/LifecycleWorkRecord'),
  prod('Domain/ManagedAgentCatalog'),
  prod('Application/Reconciliation/XTraceCapture'),
  prod('Infrastructure/OpenCode/Codec/HostMessageCodec'),
  prod('Process/Deadline'),
  prod('Process/ProcessRequest'),
  prod('Kernel/Flow'),
  prod('Application/Orchestration/Orchestrator'),
  prod('Application/Orchestration/Orchestrator.Types'),
  prod('Domain/StrengthTypes'),
  prod('Domain/StrengthPredictor'),
  prod('Domain/StrengthController'),
  prod('Domain/StrengthValue'),
  prod('Domain/EnforcerCatalog.gen'),
  prod('Domain/EnforcerCodec'),
  prod('Domain/EnforcerThrottle'),
  prod('Domain/EnforcerNudge'),
  prod('Domain/EnforcerCycle'),
  prod('Domain/StudentTeacher'),
  prod('Domain/BloggerRequestContext'),
  prod('Session/BloggerRuntimeState'),
  prod('Session/ParkedTransform'),
  prod('Infrastructure/OpenCode/Host/PluginRuntimeScope'),
  prod('Journal/AgentJournal'),
  prod('Application/Prompting/PromptDispatcher'),
  prod('Application/Prompting/PromptDispatcherSend'),
  prod('Infrastructure/OpenCode/Codec/HostEventCodec'),
  prod('Session/HandleController'),
  prod('Session/HandleCompletionCodec'),
  prod('Application/Reconciliation/ReviewSeal'),
  prod('Infrastructure/OpenCode/Host/SessionSnapshotPort'),
  prod('Infrastructure/OpenCode/Codec/ToolHostCodec'),
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
//
// A third rule, discovered by `domain.meta.test.mjs` sweeping the facade for
// undefined members: Fable appends `$` to an emitted name that would collide with
// a JS built-in or reserved word. `ReviewChallenge.Text` emits as `Text$`, and
// reading `.Text` off the module gave `undefined` — a bound clause constant that
// silently became nothing. Trying the `$` spelling here is what makes that a
// resolution rule instead of a per-call-site accident.

const member = (mod, moduleName, name) => {
  const spellings = [
    `${moduleName}Module_${name}`,
    `${moduleName}_${name}`,
    name,
    // Fable's reserved-name escape. Last, so a real member always wins.
    `${moduleName}Module_${name}$`,
    `${moduleName}_${name}$`,
    `${name}$`,
  ]
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

/** Plain object → FSharpMap (for string-keyed maps). */
export const mapOf = (obj) =>
  FsMap.ofArray(Object.entries(obj).map(([k, v]) => [k, v]), ordinalComparer)

/** FSharpList → array. */
export const listItems = (list) => FsList.toArray(list)

// F# `Set<string>` needs a comparer object; Fable does not infer one from the
// element type. `REVIEW-010`'s `IncludedToolResultDigests` is such a set, and it
// is the causal evidence a confirmation rests on — so a test that built it wrong
// would be proving the wrong thing about the most load-bearing check in SSOT/05.
const ordinalComparer = { Compare: (left, right) => (left < right ? -1 : left > right ? 1 : 0) }

/**
 * array → `FSharpSet<string>`.
 *
 * Passing a JS array where F# expects a Set does not throw: `Set.contains` walks
 * a tree structure the array does not have and answers `false` for everything. A
 * seal built that way would refuse every confirmation while looking exactly like
 * correct fail-closed behaviour.
 */
export const stringSet = (items) => FsSet.ofArray(items, ordinalComparer)

export const setItems = (value) => Array.from(value)
export const setCount = (value) => FsSet.count(value)
export const setContains = (item, value) => FsSet.contains(item, value)

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
  bloggerRequest: idModule('BloggerRequestId'),
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
export const bloggerRequestId = (v) => Ids.bloggerRequest.create(v)

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

  /**
   * The cursor's two quantities as a plain object.
   *
   * `assert.deepEqual` compares prototypes, and every cursor coming out of the
   * domain is an F# record instance — so comparing one against `{ Offset, ... }`
   * fails on the class, not on the values, and the diff blames the wrong thing.
   */
  read: (value) => ({ offset: value.Offset, failures: value.ConsecutiveFailureCount }),
}

export const fallbackProjection = (() => {
  const m = bind(FallbackProj, 'FallbackProjection', [
    'forAuthority',
    'applyAdvance',
    'applyExhausted',
    'recordSuccess',
    'mayContinue',
  ])
  return {
    /** FALLBACK-001. There is deliberately no `empty`: a run and a root are required. */
    forAuthority: (runId, root) => m.forAuthority(runId, root),

    /** Rejections carry no payload, so the case name is the whole answer. */
    applyAdvance: (identity, prevOffset, nextOffset, count, current) => {
      const result = resultOf(m.applyAdvance(identity, prevOffset, nextOffset, count, current))
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    applyExhausted: (current) => m.applyExhausted(current),
    recordSuccess: (current) => m.recordSuccess(current),
    mayContinue: (budget, current) => m.mayContinue(budget, current),

    /** The durable state as plain JS, so a renamed field cannot read `undefined`. */
    read: (current) => ({
      logicalRun: idValue.logicalRun(current.LogicalRunId),
      authorityRoot: idValue.authorityRoot(current.AuthorityRootUserMessageId),
      offset: current.Cursor.Offset,
      failures: current.Cursor.ConsecutiveFailureCount,
      dedupeKeys: listItems(current.RecentFailureKeys).length,
      exhausted: current.Exhausted,
    }),
  }
})()

// ── failure-driven context recovery (SSOT/12) ────────────────────────────────

/**
 * ARCH-010 / REVIEW-002: what a newly forked child is told, as one payload.
 *
 * `render` takes named fields rather than positional arguments: the record's three fields are all
 * strings or string collections, so a positional call cannot be read for correctness.
 */
export const forkChildPayload = (() => {
  const m = bind(ForkChildPayloadModule, 'ForkChildPayload', [
    'BaseInstructions',
    'ParentWorkRecordInstruction',
    'RequirementsInstruction',
    'render',
    'relay',
  ])

  return {
    baseInstructions: listItems(m.BaseInstructions),
    parentWorkRecordInstruction: m.ParentWorkRecordInstruction,
    requirementsInstruction: m.RequirementsInstruction,

    render: ({ assignment, parentWorkRecord, originalUserRequirements = [], payload }) =>
      m.render(
        new ForkChildPayloadModule.ForkChildAssignment(
          assignment,
          parentWorkRecord,
          toList(originalUserRequirements),
          payload,
        ),
      ),

    relay: (assignment, parentWorkRecord, requirements = [], payload) =>
      m.relay(assignment, parentWorkRecord, toList(requirements), payload),
  }
})()

/**
 * ARCH-010: the one canonical writer for runtime synthetic TOML.
 *
 * Exposed separately from `bloggerToml` because the ownership split is the point of the
 * clause. Blogger owns which parts exist and their key order; the string rules and the
 * document layout belong here, to every synthetic surface equally. A facade that let
 * `bloggerToml.renderString` keep working would tell the next reader that Blogger owns
 * string rendering — the local dialect ARCH-010 forbids.
 *
 * `document` resolves to Fable's `document$`: the plain name would collide with the DOM
 * global, so Fable escapes it. `member()` tries the `$` spelling last, which is what
 * turns that into a resolution rule rather than a per-call-site accident.
 */
export const syntheticToml = (() => {
  const m = bind(SyntheticTomlModule, 'SyntheticToml', [
    'normalizeNewlines',
    'renderString',
    'comment',
    'field',
    'tableArrayEntry',
    'document',
    'byteCount',
  ])

  return {
    normalizeNewlines: (text) => m.normalizeNewlines(text),
    renderString: (text) => m.renderString(text),
    comment: (text) => m.comment(text),
    field: (name, renderedValue) => m.field(name, renderedValue),
    tableArrayEntry: (name, fields) => m.tableArrayEntry(name, toList(fields)),
    document: (instructions, body) => m.document(toList(instructions), toList(body)),
    byteCount: (text) => m.byteCount(text),
  }
})()

/**
 * Custom tool result pre-bound (tail kept) under OpenCode Host Truncate defaults.
 * Host: 2000 lines / 51200 bytes / default head. We keep tail so Host no-ops.
 */
export const toolResultBound = (() => {
  const m = bind(ToolResultBoundModule, 'ToolResultBound', [
    'HostMaxLines',
    'HostMaxBytes',
    'Marker',
    'MarkerBytes',
    'ContentMaxLines',
    'ContentMaxBytes',
    'bound',
  ])

  return {
    hostMaxLines: m.HostMaxLines,
    hostMaxBytes: m.HostMaxBytes,
    marker: m.Marker,
    markerBytes: m.MarkerBytes,
    contentMaxLines: m.ContentMaxLines,
    contentMaxBytes: m.ContentMaxBytes,
    bound: (text) => m.bound(text),
  }
})()

/**
 * EXECUTOR-001: the plain-intent prompt composers for the Executor map/reduce path.
 *
 * `summarizeChunkPrompt` and `reduceBatchPrompt` are pure string → string functions: they
 * take an index/level and a content body and return the intent instruction only. The
 * actual chunk/combined content is carried by the fork envelope's `content` field.
 */
export const executorSummarize = (() => {
  const m = bind(ExecutorSummarize, 'ExecutorSummarize', [
    'summarizeChunkPrompt',
    'reduceBatchPrompt',
  ])

  return {
    summarizeChunkPrompt: (index) => m.summarizeChunkPrompt(index),
    reduceBatchPrompt: (level) => m.reduceBatchPrompt(level),
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
    'renderItem',
    'renderWith',
    'render',
  ])
  const buildPart = unionCase(BloggerTomlModule.BloggerDeltaPart, 'BloggerDeltaPart')

  const part = (kind, ...fields) => buildPart(kind, fields)

  return {
    truncationMarker: m.TruncationMarker,
    renderItem: (item) => m.renderItem(item),
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

/**
 * COMPANION-003 / HOST-005: XTrace — X 的唯一原始语义轨迹。
 *
 * Cursor 是独立单调序列（不随 Host compaction 作废）；part 复用 SemanticPart
 * 语义；renderer 永不输出 provenance。
 *
 * Fable 把 int64 编译为 BigInt，facade 在此吸收（VERIFY-008：Fable 约定只允许
 * 出现在 domain.mjs）。
 */export const xTrace = (() => {
  const m = bind(XTraceModule, 'XTrace', [
    'originCursor',
    'nextCursor',
    'isAfter',
    'sliceBetween',
    'sliceFrom',
    'head',
    'flatten',
    'isWorkRecordPart',
    'forWorkRecord',
    'renderItem',
    'render',
  ])
  const semanticPart = unionCase(ProviderProj.SemanticPart, 'SemanticPart')

  const part = (kind, ...fields) => semanticPart(kind, fields)

  const cursorOf = ({ Sequence }) => ({ Sequence: Number(Sequence) })
  const toCursor = (sequence) => ({ Sequence: BigInt(sequence) })
  const toCursorList = (items) => toList(items.map((item) => ({ ...item, Cursor: toCursor(item.Cursor.Sequence) })))
  const fromCursorList = (list) => listItems(list).map((item) => ({ ...item, Cursor: cursorOf(item.Cursor) }))

  return {
    originCursor: cursorOf(m.originCursor),
    next: (cursor) => cursorOf(m.nextCursor(toCursor(cursor.Sequence))),
    isAfter: (next, previous) => m.isAfter(toCursor(next.Sequence), toCursor(previous.Sequence)),
    sliceBetween: (start, end, items) => fromCursorList(m.sliceBetween(toCursor(start.Sequence), toCursor(end.Sequence), toCursorList(items))),
    sliceFrom: (start, items) => fromCursorList(m.sliceFrom(toCursor(start.Sequence), toCursorList(items))),
    head: (items) => cursorOf(m.head(toCursorList(items))),
    text: (value) => part('SemanticText', value),
    reasoning: (value) => part('SemanticReasoning', value),
    toolCall: (name, args) => part('SemanticToolCall', name, args),
    toolResult: (value) => part('SemanticToolResult', value),
    media: (mediaType, digest) => part('SemanticMedia', mediaType, digest),

    /** 一个 XTraceItem。`part` 必须是本 facade 的 part 构造器产物。 */
    item: ({ sequence, role = 'user', part: partValue, provenance = '' } = {}) => ({
      Cursor: { Sequence: sequence },
      Provenance: provenance,
      Role: role,
      Part: partValue,
    }),

    /** `[{ role, parts }]` → 平铺的 `{ role, part }` F# list。 */
    flatten: (turns) => {
      const result = m.flatten(toList(turns.map((turn) => ({ Role: turn.role, Parts: toList(turn.parts) }))))
      return listItems(result).map((entry) => ({ role: entry.Role, part: entry.Part }))
    },

    renderItem: m.renderItem,
    render: m.render,
    /** COMPANION-003: LWR projection — drop raw tool call/result. */
    forWorkRecord: (items) => fromCursorList(m.forWorkRecord(toCursorList(items))),
    isWorkRecordPart: (partValue) => m.isWorkRecordPart(partValue),
    toItems: (items) => toList(items),
  }
})()

/**
 * COMPANION-003 / COMPANION-012: 唯一 semantic capture mapper。
 *
 * MessagePart → SemanticPart。Activity 是 transport bookkeeping，被丢弃。
 */
export const xTraceCapture = (() => {
  const m = bind(XTraceCaptureModule, 'XTraceCapture', ['semanticPart', 'captureProjection', 'captureOpening', 'lifecycleWorkRecord'])
  const semanticPart = unionCase(ProviderProj.SemanticPart, 'SemanticPart')
  const messagePart = unionCase(HostMessageCodecModule.MessagePart, 'MessagePart')

  const part = (kind, ...fields) => messagePart(kind, fields)

  return {
    text: (value) => part('Text', value),
    reasoning: (value) => part('Reasoning', value),
    toolCall: (callId, name, args) => part('ToolCall', callId, name, args),
    toolResult: (callId, result) => part('ToolResult', callId, result),
    activity: (kind) => part('Activity', kind),

    map: (messagePartValue) => {
      const mapped = m.semanticPart(messagePartValue)
      return isNone(mapped) ? undefined : { tag: caseOf(mapped), part: mapped }
    },

    /** `semantic({ messages: [{ role, parts }] })` → ProviderSemanticProjection. */
    semantic: ({ messages = [] } = {}) => ({
      ProviderId: undefined,
      ModelId: undefined,
      Variant: undefined,
      Tools: toList([]),
      System: toList([]),
      Messages: toList(
        messages.map((turn) => ({
          Role: turn.role,
          Parts: toList(
            turn.parts.flatMap((part) => {
              const mapped = m.semanticPart(part)
              return isNone(mapped) ? [] : [mapped]
            }),
          ),
        })),
      ),
    }),

    /**
     * COMPANION-007: synchronise the XTrace with the semantic projection.
     * `journal` is the `{ journal }` from `agentJournal.create`.
     * Returns the updated XTrace projection state (or `undefined` without a
     * journal).
     */
    captureProjection: (journal, sessionIdValue, semanticProjection) => {
      const result = m.captureProjection(journal, sessionIdValue, semanticProjection)
      return isNone(result) ? undefined : result
    },

    /** COMPANION-003: capture the opening; requirements are a JS array. */
    captureOpening: (journal, sessionIdValue, text, requirements = []) =>
      m.captureOpening(journal, sessionIdValue, text, toList(requirements)),

    /**
     * COMPANION-003 / EXEC-006: LifecycleWorkRecord as opaque text.
     * `includeOpening` default true (parent→child). Join path passes false.
     */
    lifecycleWorkRecord: (journal, sessionIdValue, includeOpening = true) => {
      const result = m.lifecycleWorkRecord(journal, sessionIdValue, includeOpening)
      return isNone(result) ? undefined : result
    },
  }
})()

/** COMPANION-003: LWR — 唯一跨 Session 工作记录。 */export const lifecycleWorkRecord = (() => {
  const m = bind(LifecycleWorkRecordModule, 'LifecycleWorkRecord', ['render', 'materialize'])
  const opening = ({ assignment = '', requirements = [] } = {}) => ({
    AssignmentText: assignment,
    AuthoritativeRequirements: toList(requirements),
  })

  return {
    opening,
    render: (record, includeOpening = true) => m.render(includeOpening, record),
    // Default includeOpening=true (parent→child / same-session). Pass false for join.
    materialize: (
      openingValue,
      frames,
      traceItems,
      ingestedThrough,
      terminalItems,
      openingEnd = { Sequence: 0 },
      includeOpening = true,
    ) =>
      m.materialize(
        openingValue,
        toList(frames),
        toList(traceItems),
        { IngestedThrough: { Sequence: BigInt(ingestedThrough.Sequence) } },
        { Sequence: BigInt(openingEnd.Sequence) },
        toList(terminalItems),
        includeOpening,
      ),
  }
})()

/** COMPANION-004/005 / ENFORCER-030: request strings; system lives in blogger-system.md. */
export const companionPrompt = {
  normalInstruction: CompanionPromptModule.NormalInstruction,
  squashInstruction: CompanionPromptModule.SquashInstruction,
  memoryPreamble: CompanionPromptModule.CompanionMemoryPreamble,
  workingRecord: (body) => CompanionPromptModule.workingRecordMessage(body),
  newWork: (toml) => CompanionPromptModule.newWorkMessage(toml),
  memoryBlock: (frozenRecordPrefix) => CompanionPromptModule.companionMemoryBlock(frozenRecordPrefix),
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
        // Plan no longer carries System (ENFORCER-030 / COMPANION-004).
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
    // SSOT/16 LEARN-050: the Student's two request kinds.
    studentLearn: of('StudentLearn'),
    studentCompile: of('StudentCompile'),
    all: ['WorkMain', 'BloggerMain', 'BloggerSquash', 'InteractionRepair', 'StudentLearn', 'StudentCompile'].map(of),

    of,
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
 * AGENT-001…004 (C5): the sole managed-agent identity directory.
 *
 * `nameOf`/`peerNameOf` take Role/AgentTier VALUES; build them with `roles.of`
 * and `roles.tier` above (same union construction). List/set members are read
 * fresh per call so a renamed Fable member fails loudly at load time instead
 * of reading `undefined` (VERIFY-008).
 */
export const managedAgentCatalog = (() => {
  const m = bind(ManagedAgentCatalogModule, 'ManagedAgentCatalog', [
    'roleLabel',
    'tryParseRole',
    'tierLabel',
    'wireTierLabel',
    'tryParseTier',
    'peerTier',
    'nameOf',
    'peerNameOf',
    'allPublicRoles',
    'allInternalRoles',
    'allRoles',
    'publicForkableRoles',
    'requiredNames',
    'publicForkableNames',
    'orchestratorForkableNames',
    'inspectorToolNames',
    'coderToolNames',
    'legacyAgentNames',
    'isLegacyAgentName',
    'formatLegacyNameNotSupported',
    'formatLegacyNameInConfig',
  ])

  return {
    /** AGENT-001: canonical role → lowercase label. */
    roleLabel: (role) => m.roleLabel(role),
    /** AGENT-001: label → Role, or undefined. */
    tryParseRole: (name) => unwrapOption(m.tryParseRole(name)),
    /** AGENT-001: journal spelling Fast / Deep. */
    tierLabel: (tier) => m.tierLabel(tier),
    /** AGENT-001: wire spelling fast / deep. */
    wireTierLabel: (tier) => m.wireTierLabel(tier),
    /** AGENT-001: wire label → AgentTier, or undefined. */
    tryParseTier: (name) => unwrapOption(m.tryParseTier(name)),
    /** AGENT-003: Fast ⇄ Deep. */
    peerTier: (tier) => m.peerTier(tier),
    /** AGENT-002: `nameOf(Fast, Coder)` = 'fast-coder'. */
    nameOf: (tier, role) => m.nameOf(tier, role),
    /** AGENT-003: same role, opposite tier. */
    peerNameOf: (tier, role) => m.peerNameOf(tier, role),
    allPublicRoles: () => listItems(m.allPublicRoles).map(caseOf),
    allInternalRoles: () => listItems(m.allInternalRoles).map(caseOf),
    allRoles: () => listItems(m.allRoles).map(caseOf),
    publicForkableRoles: () => listItems(m.publicForkableRoles).map(caseOf),
    /** AGENT-002: exactly 20 names. */
    requiredNames: () => listItems(m.requiredNames),
    publicForkableNames: () => listItems(m.publicForkableNames),
    orchestratorForkableNames: () => listItems(m.orchestratorForkableNames),
    inspectorToolNames: () => listItems(m.inspectorToolNames),
    coderToolNames: () => listItems(m.coderToolNames),
    /** AGENT-004: the exact bare legacy names. */
    legacyAgentNames: () => setItems(m.legacyAgentNames),
    /** AGENT-004: legacy rejection predicate (lowercase input). */
    isLegacyAgentName: (lower) => m.isLegacyAgentName(lower),
    /** AGENT-004: version-agnostic rejection prose. */
    formatLegacyNameNotSupported: (name) => m.formatLegacyNameNotSupported(name),
    formatLegacyNameInConfig: (name) => m.formatLegacyNameInConfig(name),
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
      ingestedThroughSequence: Number(state.Coverage.IngestedThroughSequence),
      cutoff: state.Coverage.CoverableTurnCutoffExclusive,
      digest: state.Coverage.CoveredPrefixDigest,
      coverableFrames: state.Coverage.CoverableFrameCount,
    }),

    /** CTX-011: the frames a probe may build FrozenRecordPrefix from, by kind. */
    coverableFrameKinds: (state) => listItems(m.coverableFrames(state)).map((f) => caseOf(f.Kind)),

    /** Rejections carry payloads; the name alone is what a test asserts on. */
    applyEntry: ({ epoch, previous, next, previousCutoff, nextCutoff, digest, frame }, state) => {
      // The record coverage is an XTrace cursor sequence: Fable compiles int64 to
      // BigInt, so the facade converts before crossing (VERIFY-008).
      const result = resultOf(
        m.applyEntry(
          frameEpochId(epoch),
          BigInt(previous),
          BigInt(next),
          previousCutoff,
          nextCutoff,
          digest,
          frame,
          state,
        ),
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
      FrozenRecordPrefixRef: blobRef(ref),
      FrozenRecordPrefixDigest: blobDigest(digest),
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

/**
 * REVIEW-006: one witnessed PERFECT verdict.
 *
 * Deliberately no `authorityRoot` parameter. REVIEW-003 forbids confirming on a
 * shared authority root and REVIEW-006's field list has no such field, so the
 * facade cannot offer one either — once a test can set it, comparing it is one
 * line away.
 */
export const verdictWitness = ({ run, call, tree, reviewer }) => ({
  ProviderRun: providerRun(run),
  ToolCallId: toolCallId(call),
  GitTreeHash: gitTreeHash(tree),
  ReviewerSessionId: sessionId(reviewer),
})

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

  /** The whole witness as comparable text, so a renamed field cannot read `undefined`. */
  read: (value) => {
    const readOne = (one) => ({
      run: idValue.providerRun(one.ProviderRun),
      call: idValue.toolCall(one.ToolCallId),
      tree: idValue.gitTree(one.GitTreeHash),
      reviewer: idValue.session(one.ReviewerSessionId),
    })
    const payload = payloadOf(value)

    switch (caseOf(value)) {
      case 'NoReview':
        return { state: 'NoReview' }
      case 'RevisionWitness':
        return { state: 'RevisionWitness', tree: idValue.gitTree(payload.GitTreeHash) }
      case 'PerfectPending':
        return { state: 'PerfectPending', first: readOne(payload) }
      case 'Confirmed':
        return {
          state: 'Confirmed',
          barrier: idValue.reviewBarrier(payload.BarrierId),
          tree: idValue.gitTree(payload.GitTreeHash),
          first: readOne(payload.First),
          second: readOne(payload.Second),
          challengeResultDigest: idValue.sealDigest(payload.ChallengeResultDigest),
          secondProviderInputDigest: idValue.sealDigest(payload.SecondProviderInputDigest),
        }
      default:
        throw new Error(`unknown ReviewWitness case '${caseOf(value)}'`)
    }
  },
}

/** REVIEW-003: the fixed challenge, its version, and its digest. */
export const reviewChallenge = (() => {
  // Resolved through `bind` rather than read off the module directly. `Text`
  // emits as `Text$` (Fable escapes a reserved name), so `Challenge.Text` was
  // `undefined` — a clause constant that silently became nothing.
  const m = bind(Challenge, 'ReviewChallenge', ['Text', 'TextVersion', 'contentDigest'])

  return {
    text: m.Text,
    textVersion: m.TextVersion,
    contentDigest: (sha256) => m.contentDigest(sha256),

    /** The `PerfectChallengeIssued` payload a first PERFECT journals. */
    issued: ({ barrier, tree, reviewer, run, call, digest, version = m.TextVersion }) => ({
      BarrierId: reviewBarrierId(barrier),
      GitTreeHash: gitTreeHash(tree),
      ReviewerSessionId: sessionId(reviewer),
      FirstProviderRun: providerRun(run),
      FirstToolCallId: toolCallId(call),
      ChallengeTextVersion: version,
      ChallengeContentDigest: digest,
    }),
  }
})()

/**
 * REVIEW-010: the canonical provider input for one run.
 *
 * `included` is an array of digest STRINGS and is converted to an `FSharpSet`
 * here. A JS array would make `Set.contains` answer `false` for everything, so
 * every confirmation would be refused while looking like fail-closed behaviour.
 */
export const providerInputSeal = ({ session, run, physical = 'msg_u1', digest, included = [], version = 1 }) => ({
  SessionId: sessionId(session),
  ProviderRun: providerRun(run),
  PhysicalUserMessageId: physicalUser(physical),
  SealDigest: sealDigest(digest),
  CanonicalVersion: version,
  IncludedToolResultDigests: stringSet(included),
})

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

  /** Rejections carry no payload, so the case name is the whole answer. */
  const decided = (result) => {
    const value = resultOf(result)
    return value.ok ? value : { ok: false, error: caseOf(value.error) }
  }

  return {
    empty: m.empty,
    startBarrier: (barrier, tree, current) => m.startBarrier(barrier, tree, current),
    applySeal: (seal, current) => m.applySeal(seal, current),
    applyChallengeIssued: (challenge, current) => m.applyChallengeIssued(challenge, current),
    applyVerdict: (attempt, value, current) => decided(m.applyVerdict(attempt, value, current)),
    applyConfirmedWitness: (barrier, challengeDigest, secondInputDigest, first, second, current) =>
      decided(m.applyConfirmedWitness(barrier, challengeDigest, secondInputDigest, first, second, current)),
    hasObservedAttempt: (attempt, current) => m.hasObservedAttempt(attempt, current),
    satisfiesGuard: (tree, current) => m.satisfiesGuard(tree, current),

    /** The guard state as plain JS. */
    read: (current) => ({
      barrier: isSome(current.CurrentBarrierId) ? idValue.reviewBarrier(current.CurrentBarrierId) : undefined,
      tree: isSome(current.LastGitTreeHash) ? idValue.gitTree(current.LastGitTreeHash) : undefined,
      witness: caseOf(current.Witness),
      hasPendingChallenge: isSome(current.PendingChallenge),
      seals: mapCount(current.Seals),
      observedAttempts: listItems(current.ObservedAttemptKeys).length,
    }),
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
  toolResultDigests: (sha256, wire) => listItems(ProviderProj.toolResultDigests(sha256, wire)).map((d) => d.fields[0]),
  fixtureKey: (semantic) => ProviderProj.fixtureKey(semantic),
  semanticallyEqual: (a, b) => ProviderProj.semanticallyEqual(a, b),
  // OpenCode/Projection: Host-assembled message view (1.18.10 `tool-<tool>`
  // parts live on assistant messages; see HOST-012 tool-part test).
  decodeMessageView: (rawMessages) => ProjectionModule.decodeMessageView(rawMessages),
}

// ── host signals (SSOT/07) ───────────────────────────────────────────────────

export const hostSignals = (() => {
  const m = bind(HostEventCodecModule, 'HostEventCodec', ['isHostSignalEvent', 'tryDecode'])

  return {
    isHostSignalEvent: (raw) => m.isHostSignalEvent(raw),
    tryDecode: (raw) => m.tryDecode(raw),
  }
})()

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
    Kind: typeof kind === 'string' ? buildCompletionKind(kind) : kind,
    CompletionRef: ref,
    CompletionDigest: digest,
  })

  return {
    empty: m.empty,
    link: (handle, child, targetAgent, role, current) => decided(m.link(handle, child, targetAgent, role, current)),
    complete: (handle, completion, current) =>
      decided(m.complete(handle, typeof completion === 'string' ? completionOf(completion) : completion, current)),
    completionOf,
    retire: (handle, current) => decided(m.retire(handle, current)),
    tryFind: (handle, current) => unwrapOption(m.tryFind(handle, current)),
    isRetired: (handle, current) => m.isRetired(handle, current),
    listable: (current) => listItems(m.listable(current)),
    joinable: (current) => listItems(m.joinable(current)),
    activeHandles: (current) => listItems(m.activeHandles(current)),
    tryFindByChildSession: (child, current) => unwrapOption(m.tryFindByChildSession(child, current)),
    linkedChildren: (current) => listItems(m.linkedChildren(current)),
    lifecycleOf: (record) => caseOf(record.Lifecycle),

    /** One handle record as comparable text. */
    read: (record) => {
      const lifecycle = caseOf(record.Lifecycle)
      let completion
      let completionRef
      let completionDigest
      if (lifecycle === 'CompletedAwaitingJoin') {
        const cell = payloadOf(record.Lifecycle)
        completion = caseOf(cell.Kind)
        completionRef = isSome(cell.CompletionRef) ? idValue.blobRef(cell.CompletionRef) : undefined
        completionDigest = isSome(cell.CompletionDigest) ? idValue.blobDigest(cell.CompletionDigest) : undefined
      }
      return {
        handle: handleId.describe(record.Handle),
        child: idValue.session(record.ChildSessionId),
        targetAgent: record.TargetAgent,
        role: caseOf(record.CanonicalRole),
        lifecycle,
        // EXEC-005: `list` must distinguish which completion landed, so the kind is
        // part of the state rather than a flag beside it.
        completion,
        completionRef,
        completionDigest,
      }
    },
  }
})()

// ── EXEC-009 consume path (SSOT/09) ──────────────────────────────────────────

/** `HostForkRuntime.Join` reads `HandleProjection.joinable` (above) as the fact
 *  source, then CAS-retires via `HandleController.consume` and materialises the
 *  completion from the durable blob via `HandleCompletionCodec.tryRead`. The
 *  mailbox is notification-only; these are the production exports C6 added.
 *  There is no `tryJoin` on the projection — reality uses `joinable` + consume. */
export const handleController = (() => {
  const m = bind(HandleControllerModule, 'HandleController', ['consume'])

  return {
    consume: (journal, parentId, handle) => {
      const value = resultOf(m.consume(journal, parentId, handle))
      return value.ok ? { ok: true, record: value.value } : { ok: false, error: caseOf(value.error) }
    },
  }
})()

export const handleCompletionCodec = (() => {
  const m = bind(HandleCompletionCodecModule, 'HandleCompletionCodec', [
    'encodeOutcome',
    'tryDecode',
    'tryRead',
  ])

  return {
    encodeOutcome: (runId, outcome) => m.encodeOutcome(runId, outcome),
    tryDecode: (record, agentId, json) => resultOf(m.tryDecode(record, agentId, json)),
    tryRead: (journal, record, agentId) => {
      const value = resultOf(m.tryRead(journal, record, agentId))
      return value.ok ? { ok: true, value: unwrapOption(value.value) } : { ok: false, error: value.error }
    },
  }
})()

// ── orchestrator (SSOT/06) ───────────────────────────────────────────────────

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
    OrchestratorRuntime.Orchestrator_$ctor_EE121F2(
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
            journal.snapshot ?? (() => Folds.empty),
          )
        : undefined,
      undefined,
    ),
  forkManager: async (runtime, { job, managerAgent, prompt, worktree }) => {
    const result = resultOf(await OrchestratorRuntime.Orchestrator__ForkManager_Z259F4770(
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

/**
 * PROMPT-006/007: the send-time `SessionPromptOptions` construction and AwaitMode,
 * as the Host sees them.
 *
 * `SendAgentOwnerRoot` and `SendContinuation` build `{ Agent = Some …;
 * Model = None; … }` inside the send body, so the only way to observe them is
 * to run a send against a port and read the `options` argument `SendPrompt`
 * receives. The Fable extension-member names carry the whole
 * `Wanxiangshu_Next_OpenCode_PromptDispatcher_Runtime__Runtime_` prefix, so
 * they are absorbed here rather than at the call site (VERIFY-008).
 */
export const promptDispatcher = (() => {
  const sendAgentOwnerRoot = PromptDispatcherSendModule
    .Wanxiangshu_Next_OpenCode_PromptDispatcher_Runtime__Runtime_SendAgentOwnerRoot
  const sendContinuation = PromptDispatcherSendModule
    .Wanxiangshu_Next_OpenCode_PromptDispatcher_Runtime__Runtime_SendContinuation
  // Instance members on Runtime: Fable may emit
  //   Runtime__ProjectionFor
  //   Runtime__ProjectionFor_<hash>   (overload hash)
  //   Wanxiangshu_Next_OpenCode_PromptDispatcher_Runtime__Runtime_ProjectionFor
  // Pick the first matching function export; fail closed if none.
  const projectionForMember = (() => {
    const keys = Object.keys(PromptDispatcherModule)
    const candidates = [
      'Wanxiangshu_Next_OpenCode_PromptDispatcher_Runtime__Runtime_ProjectionFor',
      'Runtime__ProjectionFor',
      ...keys.filter((k) => /^Runtime__ProjectionFor(_|$)/.test(k) || /Runtime__Runtime_ProjectionFor/.test(k)),
    ]
    for (const key of candidates) {
      const value = PromptDispatcherModule[key]
      if (typeof value === 'function') return value
    }
    const near = keys.filter((k) => /Projection/i.test(k)).join(', ')
    throw new Error(`PromptDispatcher.Runtime.ProjectionFor missing. Near: ${near || '(none)'}`)
  })()
  const buildSendOutcome = unionCase(Outcome.Outcome_SendOutcome, 'Outcome.SendOutcome')
  // Nested DU under PromptDispatcher: Fable may emit AwaitMode or PromptDispatcher_AwaitMode.
  const AwaitModeClass =
    PromptDispatcherModule.AwaitMode
    ?? PromptDispatcherModule.PromptDispatcher_AwaitMode
  if (typeof AwaitModeClass !== 'function') {
    const near = Object.keys(PromptDispatcherModule).filter((k) => /Await/i.test(k)).join(', ')
    throw new Error(`PromptDispatcher.AwaitMode missing. Near: ${near || '(none)'}`)
  }
  const awaitModeOf = unionCase(AwaitModeClass, 'PromptDispatcher.AwaitMode')
  const journalSnapshot = member(AgentJournalModule, 'AgentJournal', 'snapshot')

  const decode = (result) => {
    const value = resultOf(result)
    return value.ok ? { ok: true, key: idValue.promptKey(value.value) } : value
  }

  /** PROMPT-007: default Detached (fire-and-forget) unless the test asks Await. */
  const resolveAwaitMode = (mode) => {
    if (mode === undefined || mode === null) return awaitModeOf('Detached')
    if (typeof mode === 'string') return awaitModeOf(mode)
    return mode
  }

  return {
    forJournal: (journal) => PromptDispatcherModule.forJournal(journal),

    /** PROMPT-007 AwaitMode constructors. */
    awaitMode: {
      await: () => awaitModeOf('Await'),
      detached: () => awaitModeOf('Detached'),
      of: (name) => awaitModeOf(name),
    },

    /** PROMPT-006: an `AdmittedWithReceipt` outcome for a stub port to return. */
    admittedWithReceipt: (receipt) => buildSendOutcome('AdmittedWithReceipt', [receipt]),

    /**
     * PROMPT-005/007: authority projection after a send.
     * Detached success still claims/submits; no PhysicalAccepted required for caller Ok.
     */
    projectionFor: (runtime, session) => projectionForMember(runtime, sessionId(session)),

    /** Integrated journal projection (PendingClaims live under session.PromptAuthority). */
    journalSnapshot: (journal) => journalSnapshot(journal),

    /** Pending claim count for one session after Detached/Await send. */
    pendingClaimCount: (runtime, session) => {
      const projection = projectionForMember(runtime, sessionId(session))
      return mapCount(projection.PendingClaims)
    },

    sendAgentOwnerRoot: async (runtime, port, { session, text, agent, directory, awaitMode, onAccepted }) =>
      decode(
        await sendAgentOwnerRoot(
          runtime,
          port,
          sessionId(session),
          text,
          agent,
          directory,
          resolveAwaitMode(awaitMode),
          onAccepted,
        ),
      ),

    sendContinuation: async (runtime, port, { session, text, continuation, profile, effectiveAgent, directory, awaitMode, onAccepted }) =>
      decode(
        await sendContinuation(
          runtime,
          port,
          sessionId(session),
          text,
          continuation,
          profile,
          effectiveAgent,
          directory,
          resolveAwaitMode(awaitMode),
          onAccepted,
        ),
      ),
  }
})()

/**
 * HOST-010: transform → ProviderRunIdentity binding (ReviewSeal.bindableRun).
 * Messages are Host-raw objects projected via SessionSnapshotPort.projectMessages.
 */
export const reviewSeal = (() => {
  const bindableRun = member(ReviewSealModule, 'ReviewSeal', 'bindableRun')
  const projectMessages = member(SessionSnapshotPortModule, 'SessionSnapshotPort', 'projectMessages')
  const decodeRejection = (error) => {
    const name = caseOf(error)
    if (name === 'AmbiguousRun') {
      const fields = error.fields ?? []
      return { case: name, count: fields[0] }
    }
    return { case: name }
  }

  return {
    /** Project Host-shaped message objects into SessionMessage list. */
    projectMessages: (rawMessages) => projectMessages(rawMessages),

    /**
     * HOST-010 bindableRun. `physicalUser` is the last user message id.
     * `messages` may be a projected F# list or Host-raw objects (auto-projected).
     * Returns `{ ok: true, id }` or `{ ok: false, rejection: { case, count? } }`.
     */
    bindableRun: (physicalUser, messages) => {
      // Host-raw JS array → project; already-projected F# list passes through.
      const list =
        Array.isArray(messages) ? projectMessages(messages) : messages
      const result = resultOf(bindableRun(physicalUser, list))
      if (result.ok) {
        const msg = result.value
        return {
          ok: true,
          id: msg.Id,
          parentId: unwrapOption(msg.ParentId),
          completed: Boolean(msg.Completed),
        }
      }
      return { ok: false, rejection: decodeRejection(result.error) }
    },
  }
})()

/**
 * HOST-011: ToolContext decode at the adapter boundary.
 * callID + messageID must both be present; either missing → None fail-closed.
 */
export const toolHostCodec = (() => {
  const decodeContext = member(ToolHostCodecModule, 'ToolHostCodec', 'decodeContext')
  return {
    decodeContext: (raw) => {
      const ctx = decodeContext(raw)
      return {
        sessionId: ctx.SessionId,
        agent: unwrapOption(ctx.Agent),
        toolCallId: (() => {
          const id = unwrapOption(ctx.ToolCallId)
          return id === undefined ? undefined : idValue.toolCall(id)
        })(),
        providerRunId: (() => {
          const id = unwrapOption(ctx.ProviderRunId)
          return id === undefined ? undefined : idValue.providerRun(id)
        })(),
        promptText: unwrapOption(ctx.PromptText),
      }
    },
  }
})()

/**
 * PROMPT-006: an `AgentJournal` instance, for driving a real send.
 *
 * `journalStore.open` hands back the bare `JournalWriter`; `PromptDispatcher`
 * needs the full `AgentJournal` (writer + folded projection). This is the
 * `AgentJournal.create` entry (PERSIST-004), which owns the mandatory first
 * envelope exactly like `journalStore.open` does.
 *
 * `AgentJournal` is a type AND a module in the same file, so Fable emits its
 * members with the `Module` suffix (`AgentJournalModule_create`, registered
 * in guide-contract.test.mjs); `bind` resolves that spelling and throws if
 * the member disappears.
 */
const AgentJournalCreate = bind(AgentJournalModule, 'AgentJournal', ['create'])

export const agentJournal = {
  create: ({ directory, runtime = 'rt_1', pid = 4242, startedAt = '2026-01-01T00:00:00Z' } = {}) => {
    const result = resultOf(AgentJournalCreate.create(directory, runtimeId(runtime), pid, utcOffset(startedAt)))
    return result.ok
      ? { ok: true, journal: result.value, dispose: () => result.value.Dispose() }
      : result
  },
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

/** A `CancellationToken`. `cancelled()` is already-cancelled at construction. */
export const liveToken = () => new AsyncBuilder.CancellationToken(false)
export const cancelledToken = () => new AsyncBuilder.CancellationToken(true)

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

// ── SSOT/14: Strength 纯领域内核 ─────────────────────────────────────────────

export const strength = (() => {
  const types = bind(StrengthTypesModule, 'StrengthTypes', [
    'satelliteInvariantsHold',
  ])
  const predictor = bind(StrengthPredictorModule, 'StrengthPredictor', [
    'emptyRoleState',
    'observeRequest',
    'interpolatedProbability',
    'predictRead',
  ])
  const controller = bind(StrengthControllerModule, 'StrengthController', [
    'initialState',
    'hashToUnitInterval',
    'includedInTraining',
    'updateProbability',
    'ewmaAlpha',
    'onEligibleDecision',
  ])
  const value = bind(StrengthValueModule, 'StrengthValue', [
    'defaultCostModel',
    'valueK1',
    'valueK2',
    'chooseBudget',
    'batchWithinByteLimit',
    'decisionWithinByteLimit',
  ])

  const budgetOf = (b) => caseOf(b)
  const symbolOf = (s) => caseOf(s)
  const requestSymbol = (name, payload) => unionCase(StrengthTypesModule.RequestSymbol, 'RequestSymbol')(name, payload ?? [])
  const readBatch = (fields) => ({
    Tools: fields.tools ?? [],
    Parallelism: fields.parallelism ?? 1,
    ResultBytes: fields.resultBytes ?? 0,
  })

  return {
    budgetOf,
    symbolOf,
    requestSymbol,
    readBatch,

    emptyRoleState: () => predictor.emptyRoleState,
    observeRequest: (state, symbols) => predictor.observeRequest(state, toList(symbols)),
    predictRead: (state, history, features) => {
      const [p1, p2] = predictor.predictRead(state, toList(history), features)
      return { p1, p2 }
    },

    initialState: () => controller.initialState,
    hashToUnitInterval: (sha, seed) => controller.hashToUnitInterval(sha, seed),
    includedInTraining: (sha, decisionId, ordinal, p) => {
      const [included, u] = controller.includedInTraining(sha, decisionId, ordinal, p)
      return { included, u }
    },
    updateProbability: (alpha, minP, maxP, maxStep, prev, tendency) =>
      controller.updateProbability(alpha, minP, maxP, maxStep, prev, tendency),
    ewmaAlpha: (halfLife) => controller.ewmaAlpha(halfLife),
    onEligibleDecision: (state, t1, t2) => controller.onEligibleDecision(state, t1, t2),

    defaultCostModel: (tierName) => value.defaultCostModel(tier(tierName)),
    valueK1: (cost, p1, bytes, delay) => value.valueK1(cost, p1, bytes, delay),
    valueK2: (cost, p1, p2, b1, b2, d1, d2) => value.valueK2(cost, p1, p2, b1, b2, d1, d2),
    chooseBudget: (v0, v1, v2) => budgetOf(value.chooseBudget(v0, v1, v2)),
    batchWithinByteLimit: (bytes) => value.batchWithinByteLimit(bytes),
    decisionWithinByteLimit: (bytes) => value.decisionWithinByteLimit(bytes),
  }
})()

const tier = (name) => {
  if (name === 'Fast') return RolesModule.AgentTier.Fast
  if (name === 'Deep') return RolesModule.AgentTier.Deep
  throw new Error(`unknown tier: ${name}`)
}

// ── SSOT/15: Blogger as Enforcer 纯领域内核 ─────────────────────────────────

export const enforcer = (() => {
  const catalog = bind(EnforcerCatalogModule, 'EnforcerCatalogData', ['rules'])
  const codec = bind(EnforcerCodecModule, 'EnforcerCodec', [
    'CanonicalBlogCall',
    'normalizeFieldName',
    'hasEnfPrefix',
    'parseScore',
    'damerauLevenshtein',
    'resolveField',
    'decodeCall',
    'hasValidText',
  ])
  const throttle = bind(EnforcerThrottleModule, 'EnforcerThrottle', [
    'ThrottleState',
    'ThrottleTauObservations',
    'decay',
    'normalizedObservation',
    'epochStart',
    'observe',
    'shouldTrigger',
    'consume',
    'pressureAt',
    'steadyEvidence',
    'isolatedPressure',
  ])
  const nudge = bind(EnforcerNudgeModule, 'EnforcerNudge', [
    'renderLine',
    'renderEvidence',
    'renderBatch',
    'mergeEvidence',
  ])
  const cycle = bind(EnforcerCycleModule, 'EnforcerCycle', ['MergedCycle', 'mergeCalls', 'isValidCycle'])

  const catalogRules = listItems(catalog.rules)

  return {
    /** 全部 120 项规则（生成自 RFC/enforcer-nudge.md，ENFORCER-170）。 */
    rules: catalogRules,
    ruleCount: catalogRules.length,
    fieldNames: () => catalogRules.map((r) => r.FieldName),

    /** ENFORCER-022/023/024/025：解析一个 blog 调用的参数。 */
    decodeCall: (rawArgs) => {
      // Production expects Map<string, obj>; tests hand plain objects.
      const mapped = mapOf(rawArgs)
      return codec.decodeCall(
        toList(catalogRules.map((r) => [r.FieldName, r.RuleId, r.CatalogOrdinal])),
        mapped,
      )
    },

    /** ENFORCER-023：值容错。 */
    parseScore: (v) => codec.parseScore(v),

    /** ENFORCER-024：字段名规范化。 */
    normalizeFieldName: (s) => codec.normalizeFieldName(s),

    /** ENFORCER-024：DL 距离。 */
    damerauLevenshtein: (a, b) => codec.damerauLevenshtein(a, b),

    /** ENFORCER-081/083/084：throttle。 */
    epochStart: (ordinal) => throttle.epochStart(BigInt(ordinal)),
    observe: (state, score, ordinal) => {
      const [next, pressure] = throttle.observe(throttle.ThrottleTauObservations, state, score, BigInt(ordinal))
      return { state: next, pressure }
    },
    shouldTrigger: (pressure) => throttle.shouldTrigger(pressure),
    consume: (state, ordinal) => throttle.consume(state, BigInt(ordinal)),
    steadyEvidence: (score) => throttle.steadyEvidence(throttle.ThrottleTauObservations, score),
    pressureAt: (evidence, sinceConsumed) => throttle.pressureAt(throttle.ThrottleTauObservations, evidence, sinceConsumed),
    isolatedPressure: (initialEvidence, elapsed) =>
      throttle.isolatedPressure(throttle.ThrottleTauObservations, initialEvidence, elapsed),

    /** ENFORCER-100/101/102：nudge 渲染。 */
    renderLine: (key, text) => nudge.renderLine(key, text),
    renderEvidence: (e) => nudge.renderEvidence(e),
    renderBatch: (rules, evidence) =>
      nudge.renderBatch(toList(rules.map((r) => [r[0], r[1], r[2]])), evidence),
    mergeEvidence: (items) => nudge.mergeEvidence(toList(items)),

    /** ENFORCER-042/043：cycle 合并。 */
    mergeCalls: (calls) => {
      const list = toList(
        calls.map(([ordinal, call]) => [
          ordinal,
          {
            Text: call.Text ?? undefined,
            Evidence: call.Evidence ?? undefined,
            Scores: mapOf(call.Scores ?? {}),
          },
        ]),
      )
      return cycle.mergeCalls(list)
    },
    isValidCycle: (merged) => cycle.isValidCycle(merged),
  }
})()

// ── SSOT/15 ENFORCER-160/162: 挂起 transform 原语 ────────────────────────────

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
        Toml: toml ?? '[[message]]\nrole = "user"\ntext = "work"',
        PreviousIngestedThroughSequence: previousIngested,
        NextIngestedThroughSequence: nextIngested,
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
  const stateCase = unionCase(BloggerRuntimeModule.BloggerRuntimeState, 'BloggerRuntimeState')
  const m = bind(BloggerRuntimeModule, 'BloggerRuntime', [
    'empty',
    'ofState',
    'onMaterial',
    'beginRequest',
    'onCycleCommitted',
    'onSquashCommitted',
    'onFail',
    'markRepairSpent',
    'onDispose',
    'inFlightContext',
    'tryPeekInFlight',
    'tryTakeInFlight',
    'tryTakePending',
    'adoptPendingAsCurrent',
  ])

  return {
    idle: m.ofState(stateCase('Idle', [])),
    parked: m.ofState(stateCase('Parked', [])),
    disposed: m.ofState(stateCase('Disposed', [])),
    empty: m.empty,
    inFlight: (ctx) => m.ofState(stateCase('InFlight', [ctx])),
    onMaterial: (cell, ctx) => {
      const r = resultOf(m.onMaterial(cell, ctx))
      if (!r.ok) return { ok: false, error: caseOf(r.error) }
      const pair = r.value
      return {
        ok: true,
        state: pair[0],
        decision: caseOf(pair[1]),
        pending: unwrapOption(pair[0].PendingOffer),
        repairSpent: pair[0].RepairSpent,
      }
    },
    onCycleCommitted: (cell) => {
      const r = resultOf(m.onCycleCommitted(cell))
      return r.ok
        ? { ok: true, state: r.value, repairSpent: r.value.RepairSpent }
        : { ok: false, error: caseOf(r.error) }
    },
    onSquashCommitted: (cell, pendingMain) => {
      const r = resultOf(m.onSquashCommitted(cell, pendingMain === undefined ? undefined : pendingMain))
      if (!r.ok) return { ok: false, error: caseOf(r.error) }
      const pair = r.value
      return { ok: true, state: pair[0], decision: caseOf(pair[1]), repairSpent: pair[0].RepairSpent }
    },
    onFail: (cell) => {
      const r = resultOf(m.onFail(cell))
      return r.ok
        ? { ok: true, state: r.value, repairSpent: r.value.RepairSpent }
        : { ok: false, error: caseOf(r.error) }
    },
    markRepairSpent: (cell) => m.markRepairSpent(cell),
    onDispose: (cell) => m.onDispose(cell),
    inFlightContext: (cell) => unwrapOption(m.inFlightContext(cell)),
    tryTakeInFlight: (cell) => {
      const r = resultOf(m.tryTakeInFlight(cell))
      if (!r.ok) return { ok: false, error: caseOf(r.error) }
      const pair = r.value
      return { ok: true, context: pair[0], state: pair[1] }
    },
    tryTakePending: (cell) => {
      const r = resultOf(m.tryTakePending(cell))
      if (!r.ok) return { ok: false, error: caseOf(r.error) }
      const pair = r.value
      return { ok: true, pending: unwrapOption(pair[0]), state: pair[1] }
    },
    stateOf: (cell) => caseOf(cell.State),
    repairSpentOf: (cell) => cell.RepairSpent,
  }
})()

export const parkedTransform = (() => {
  const ParkedTransform = ParkedTransformModule.ParkedTransform
  const PluginRuntimeScope = PluginRuntimeScopeModule.PluginRuntimeScope

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
    scope: () => new PluginRuntimeScope(null),
    park: (scope, sessionId, lifetimeMs) => scope.ParkTransform(sessionId, lifetimeMs),
    resumeParked: (scope, sessionId) => scope.ResumeParked(sessionId),
    cancelParked: (scope, sessionId) => scope.CancelParked(sessionId),
    setPendingOffer: (scope, sessionId, context) => scope.SetPendingOffer(sessionId, context),
    // Back-compat alias used by parked-transform tests (PendingOffer path).
    offerParked: (scope, sessionId, context) => scope.SetPendingOffer(sessionId, context),
    hasParked: (scope, sessionId) => scope.HasParked(sessionId),
    consumeStaged: (scope, sessionId) => projectContext(scope.TryTakePendingOffer(sessionId)),
    setCurrentRequest: (scope, sessionId, context) => scope.SetCurrentRequest(sessionId, context),
    peekCurrentRequest: (scope, sessionId) => projectContext(scope.TryPeekCurrentRequest(sessionId)),
    clearCurrentRequest: (scope, sessionId) => scope.ClearCurrentRequest(sessionId),
    getRuntime: (scope, sessionId) => scope.GetBloggerRuntime(sessionId),
    setRuntime: (scope, sessionId, cell) => scope.SetBloggerRuntime(sessionId, cell),
    dispose: (scope) => scope.Dispose(),
  }
})()

// ── SSOT/16: Student & Teacher 纯领域内核 ────────────────────────────────────

export const studentTeacher = (() => {
  const m = bind(StudentTeacherModule, 'StudentTeacher', [
    'StudentTeacherRole',
    'StudentTool',
    'studentToolsFor',
    'isStudentRequest',
    'teacherTierFor',
    'studentAgentName',
    'teacherAgentName',
    'isIgnoredTmpPath',
    'appendEntry',
    'dedupeTail',
    'QaAppendOrder',
    'StudentRunConcurrency',
    'mayStartTeacherCall',
    'ReturnDeleteOutcome',
    'returnMayProceed',
  ])

  const toolNames = (set) => [...set].map(caseOf).sort()
  const toolOf = (name) => unionCase(StudentTeacherModule.StudentTool, 'StudentTool')(name, [])

  return {
    /** LEARN-050：Student 工具面按请求种类原子决定。 */
    toolsFor: (kindName) => toolNames(m.studentToolsFor(requestKind.of(kindName))),
    /** LEARN-051：非 Student 请求种类 → 空工具集。 */
    toolsForKind: (kind) => toolNames(m.studentToolsFor(kind)),

    /** LEARN-017：同 tier 映射。 */
    teacherTier: (tierName) => caseOf(m.teacherTierFor(tier(tierName))),

    /** LEARN-016：Agent 名。 */
    studentAgent: (tierName) => m.studentAgentName(tier(tierName)),
    teacherAgent: (tierName) => m.teacherAgentName(tier(tierName)),

    /** LEARN-032/033/035/036：QA 内容拼接。 */
    isIgnoredTmpPath: (p) => m.isIgnoredTmpPath(p),
    append: (existing, entry) => m.appendEntry(existing, entry),
    dedupeTail: (existing, entry) => m.dedupeTail(existing, entry),

    /** LEARN-075：单飞并发门。 */
    mayStartTeacherCall: (stateName) => {
      const state = unionCase(StudentTeacherModule.StudentRunConcurrency, 'StudentRunConcurrency')(stateName, [])
      return m.mayStartTeacherCall(state)
    },

    /** LEARN-024：最终 return 删除顺序。 */
    returnMayProceed: (outcomeName) => {
      const outcome = unionCase(StudentTeacherModule.ReturnDeleteOutcome, 'ReturnDeleteOutcome')(outcomeName, [])
      return m.returnMayProceed(outcome)
    },
  }
})()
