// tests/unit/domain.mjs — the ONLY file allowed to know Fable's output shape.
//
// VERIFY-008. Production is .fs; layers 1-3 tests are .mjs consuming
// dist. Fable's emitted names and container shapes are compiler
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

const BUILD_ROOT = new URL('../../../dist/', import.meta.url).pathname

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
  EnforcementProj,
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
  TddPhaseModule,
  BloggerTomlModule,
  BloggerDeltaModule,
  CompanionPromptModule,
  CompanionIdentityModule,
  CompanionBuilderModule,
  ProbeSelectionModule,
  XPrefixModule,
  ProjectionAlgebraModule,
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
  RuntimeResourcesModule,
  EnforcerCatalogResourceModule,
  PackageResourcesModule,
  PromptResourcesModule,
  EnforcerCatalogDomainModule,
  EnforcerCodecModule,
  EnforcerCycleModule,
  BloggerRequestContextModule,
  BloggerRuntimeModule,
  ParkedTransformModule,
  PluginRuntimeScopeModule,
  AgentJournalModule,
  PromptDispatcherModule,
  PromptDispatcherSendModule,
  HostEventCodecModule,
  LoopDetectorModule,
  LoopEventCodecModule,
  LoopSensorModule,
  RuntimeNudgeModule,
  FallbackControllerModule,
  HandleControllerModule,
  HandleCompletionCodecModule,
  JoinDrainModule,
  ReviewSealModule,
  SessionSnapshotPortModule,
  ToolHostCodecModule,
  CompletionMailboxModule,
  AgentCompletionModuleEarly,
  HostForkRunLifecycleModule,
  HostPendingRunModule,
  EventsModule,
  ReconcileSupervisorModule,
  TurnBindingModule,
  ForkRuntimeModule,
  ForkTypesModule,
  HostSignalSubscribeModule,
  ManagedAgentConfigModule,
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
  prod('Journal/EnforcementProjection'),
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
  prod('Domain/TddPhase'),
  prod('Domain/BloggerToml'),
  prod('Domain/BloggerDelta'),
  prod('Domain/CompanionPrompt'),
  prod('Domain/CompanionIdentity'),
  prod('Domain/CompanionProjectionBuilder'),
  prod('Domain/PrefixProbeSelection'),
  prod('Domain/XPrefixProjection'),
  prod('Domain/ProjectionAlgebra'),
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
  prod('Kernel/Parallel'),
  prod('Application/Orchestration/Runtime'),
  prod('Application/Orchestration/Types'),
  prod('Infrastructure/Resources/RuntimeResources'),
  prod('Infrastructure/Resources/EnforcerCatalogResource'),
  prod('Infrastructure/Resources/PackageResources'),
  prod('Infrastructure/Resources/PromptResources'),
  prod('Domain/EnforcerCatalog'),
  prod('Domain/EnforcerCodec'),
  prod('Domain/EnforcerCycle'),
  prod('Domain/BloggerRequestContext'),
  prod('Session/BloggerRuntimeState'),
  prod('Session/ParkedTransform'),
  prod('Infrastructure/OpenCode/Host/PluginRuntimeScope'),
  prod('Journal/AgentJournal'),
  prod('Application/Prompting/PromptDispatcher'),
  prod('Application/Prompting/PromptDispatcherSend'),
  prod('Infrastructure/OpenCode/Codec/HostEventCodec'),
  prod('Domain/LoopDetector'),
  prod('Infrastructure/OpenCode/Codec/LoopEventCodec'),
  prod('Infrastructure/OpenCode/Host/LoopSensor'),
  prod('Domain/RuntimeNudge'),
  prod('Session/FallbackController'),
  prod('Session/HandleController'),
  prod('Session/HandleCompletionCodec'),
  prod('Session/JoinDrain'),
  prod('Application/Reconciliation/ReviewSeal'),
  prod('Infrastructure/OpenCode/Host/SessionSnapshotPort'),
  prod('Infrastructure/OpenCode/Codec/ToolHostCodec'),
  prod('Session/CompletionMailbox'),
  prod('Session/AgentCompletion'),
  prod('Session/HostForkRunLifecycle'),
  prod('Session/HostPendingRun'),
  prod('Infrastructure/OpenCode/Host/Events'),
  prod('Application/Reconciliation/ReconcileSupervisor'),
  prod('Application/Reconciliation/TurnBinding'),
  prod('Session/ForkRuntime'),
  prod('Session/ForkTypes'),
  prod('Infrastructure/OpenCode/Signals/HostSignalSubscribe'),
  prod('Infrastructure/OpenCode/Host/ManagedAgentConfig'),
])

const [NodeProcessWaitModule, NodeProcessHostModule, PtyTimingModule, FableTask, FableTypes] =
  await Promise.all([
    prod('Process/NodeProcessWait'),
    prod('Process/NodeProcessHost'),
    prod('Process/PtyTiming'),
    lib('Task.js'),
    lib('Types.js'),
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
// would be proving the wrong thing about the most load-bearing check in docs/what/review.md.
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

const buildAgentFactDispatch = unionCase(FactModule.AgentFact, 'AgentFact')

// DSL-003: AgentFact is a 7-case dispatch union over per-bounded-context
// *FactCases families. The facade keeps the flat construction surface — a test
// names the business case, the family lookup wraps it — so no test learns the
// nesting, and the wire shape (case name + payload) is unchanged.
const AGENT_FACT_FAMILIES = [
  ['Prompt', FactModule.PromptFactCases],
  ['Fallback', FactModule.FallbackFactCases],
  ['Review', FactModule.ReviewFactCases],
  ['Execution', FactModule.ExecutionFactCases],
  ['Orchestrator', FactModule.OrchestratorFactCases],
  ['Companion', FactModule.CompanionFactCases],
  ['Context', FactModule.ContextFactCases],
]

const buildAgentFact = (() => {
  const familyBuilders = AGENT_FACT_FAMILIES.map(([dispatchCase, unionClass]) => {
    const build = unionCase(unionClass, `${dispatchCase}FactCases`)
    return [dispatchCase, caseNames(unionClass), build]
  })
  return (caseName, fields) => {
    for (const [dispatchCase, names, build] of familyBuilders) {
      if (names.includes(caseName)) return buildAgentFactDispatch(dispatchCase, [build(caseName, fields)])
    }
    throw new Error(
      `no AgentFact family has case '${caseName}'. Available: ${familyBuilders.flatMap(([, names]) => names).join(', ')}`,
    )
  }
})()
const buildFact = unionCase(FactModule.Fact, 'Fact')
const buildRuntimeFact = unionCase(FactModule.RuntimeFact, 'RuntimeFact')
const buildStream = unionCase(EnvelopeModule.StreamId, 'StreamId')
const buildVerdict = unionCase(FactModule.ReviewGuardVerdict, 'ReviewGuardVerdict')
const buildAbandonReason = unionCase(FactModule.PromptAbandonReason, 'PromptAbandonReason')
const buildCompletionKind = unionCase(FactModule.HandleCompletionKind, 'HandleCompletionKind')
const buildHandleAbandonReason = unionCase(FactModule.HandleAbandonReason, 'HandleAbandonReason')
const buildHandleOwnership = unionCase(FactModule.HandleOwnership, 'HandleOwnership')

/** Flat case-name catalogue across all AgentFact families (DSL-003). */
export const agentFactCaseNames = () => AGENT_FACT_FAMILIES.flatMap(([, unionClass]) => caseNames(unionClass))

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
  managerLife: idModule('ManagerLifeId'),
  finalityRequest: idModule('FinalityRequestId'),
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
export const managerLifeId = (v) => Ids.managerLife.create(v)
export const finalityRequestId = (v) => Ids.finalityRequest.create(v)

// Epoch ids wrap int64, so Fable represents them as BigInt. Taking a JS number
// here and converting once keeps `1` out of every call site — passing a plain
// number where F# expects int64 does not throw, it silently compares unequal.
export const frameEpochId = (value) => Identity.FrameEpochIdModule_create(BigInt(value))
export const prefixEpochId = (value) => Identity.PrefixEpochIdModule_create(BigInt(value))

export const localSeq = (value) => Identity.LocalSeqModule_create(BigInt(value))

export const journalRevision = {
  create: (value) => Identity.JournalRevisionModule_create(BigInt(value)),
  value: (rev) => Number(Identity.JournalRevisionModule_value(rev)),
  /** Prefer create(0): Fable may emit `initial` as a value or getter. */
  initial: () =>
    Identity.JournalRevisionModule_initial ??
    Identity.JournalRevisionModule_create(0n),
  next: (rev) => Identity.JournalRevisionModule_next(rev),
  isAfter: (a, b) => Identity.JournalRevisionModule_isAfter(a, b),
}

export const idValue = Object.fromEntries(
  Object.entries(Ids).map(([name, module]) => [name, module.value]),
)
idValue.localSeq = (id) => Identity.LocalSeqModule_value(id)
idValue.frameEpoch = (id) => Identity.FrameEpochIdModule_value(id)
idValue.prefixEpoch = (id) => Identity.PrefixEpochIdModule_value(id)
idValue.journalRevision = (id) => Number(Identity.JournalRevisionModule_value(id))

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

/** EXEC-009 HandleAbandoned reason (fieldless DU cases). */
export const handleAbandonReason = {
  of: (name) => buildHandleAbandonReason(name),
  parentCancelled: () => buildHandleAbandonReason('ParentCancelled'),
  deadlineExceeded: () => buildHandleAbandonReason('DeadlineExceeded'),
  hostSessionGone: () => buildHandleAbandonReason('HostSessionGone'),
}

/** HandleOwnership: which side of the boundary owns the physical resource. */
export const handleOwnership = {
  of: (name) => buildHandleOwnership(name),
  durableParentHandle: () => buildHandleOwnership('DurableParentHandle'),
  hostOwnedHidden: () => buildHandleOwnership('HostOwnedHidden'),
}

/** Build an AgentFact by case name with an anonymous-record payload. */
export const agentFact = (caseName, payload) => buildAgentFact(caseName, [payload])

/**
 * The business case name of an AgentFact, with the DSL-003 family dispatch
 * peeled. `agentFact('FallbackCursorAdvanced', ...)` round-trips to
 * 'FallbackCursorAdvanced' — tests never learn the family nesting.
 */
export const agentFactCaseOf = (value) => {
  const dispatch = caseOf(value)
  if (dispatch === undefined) return undefined
  const family = AGENT_FACT_FAMILIES.find(([name]) => name === dispatch)
  if (!family) throw new TypeError(`agentFactCaseOf: '${dispatch}' is not an AgentFact family dispatch`)
  return caseOf(payloadOf(value))
}

/** Wrap an AgentFact as the top-level Fact union. */
export const asFact = (inner) => buildFact('Agent', [inner])

/** Convenience: build and wrap in one step. */
export const fact = (caseName, payload) => asFact(agentFact(caseName, payload))

const buildManagerLifecycleFact = unionCase(FactModule.ManagerLifecycleFact, 'ManagerLifecycleFact')

/** Build a ManagerLifecycleFact by case name (GLORY-010). */
export const managerLifecycleFact = (caseName, payload) =>
  buildFact('ManagerLifecycle', [buildManagerLifecycleFact(caseName, [payload])])

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
  /** ENFORCER-072: ScoreVectorRef-era BlogEntryCommitted (no max-score migration). */
  containsLegacyScoreVectorEntry: (json) => FactCodec.containsLegacyScoreVectorEntry(json),
  tipV2CleanBreakMessage: FactCodec.tipV2CleanBreakMessage,
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

// ── fallback (docs/what/fallback.md) ───────────────────────────────────────────────────────

/** FALLBACK-002: the facade keeps the historical numeric offset signature; the
 * closed DU lives only inside the domain. Declared in declaration order, so the
 * numeric tag is the case index.
 */
const FALLBACK_OFFSET_NAMES = ['Fork0', 'Fork1', 'Fork2', 'Fork3']
const offsetOf = (n) => {
  // Already a Fable union instance (from `cursor.initial`, `recordFailure`, ...).
  if (n && typeof n === 'object') return n
  const idx = Number(n)
  if (!Number.isInteger(idx) || idx < 0 || idx > 3) {
    throw new Error(`FallbackOffset 0..3 has no case for ${n}`)
  }
  return unionCase(Cursor.FallbackOffset, 'FallbackOffset')(FALLBACK_OFFSET_NAMES[idx], [])
}
const offsetValue = (offset) => (offset === undefined ? undefined : offset.tag)

/** A cursor as the tests build it — plain object with a NUMERIC offset — or an
 * F# record — gets normalised to the F# record shape the domain expects.
 */
const cursorOf = (value) => ({
  Offset: offsetOf(value.Offset),
  ConsecutiveFailureCount: value.ConsecutiveFailureCount,
})

export const cursor = {
  initial: Cursor.initial,
  atOffset: (offset) => Cursor.atOffset(offsetOf(offset)),
  advance: (offset) => offsetValue(Cursor.advance(offsetOf(offset))),
  recordFailure: (value) => Cursor.recordFailure(cursorOf(value)),
  recordSuccess: (value) => Cursor.recordSuccess(cursorOf(value)),
  side: (offset) => caseOf(Cursor.side(offsetOf(offset))),
  sideSequence: (count) => listItems(Cursor.sideSequence(count)).map(caseOf),
  effectiveAgent: (pair, value) => Cursor.effectiveAgent(pair, cursorOf(value)),
  isValidAdvance: (prevOffset, nextOffset, prevCount, nextCount) =>
    Cursor.isValidAdvance(offsetOf(prevOffset), offsetOf(nextOffset), prevCount, nextCount),

  /** CTX-006: is this one of the primed slots (A′ / B′). */
  isRecoverySlot: (offset) => Cursor.isRecoverySlot(offsetOf(offset)),
  attemptIdentity: (session, run, root, providerRunId) => Cursor.attemptIdentity(session, run, root, providerRunId),

  /** FALLBACK-005: `MayContinue` | `Exhausted`, with the cursor as payload. */
  recoveryVerdict: (budget, value) => caseOf(Cursor.recoveryVerdict(budget, cursorOf(value))),

  defaultBudget: Cursor.DefaultAutoRecoveryBudget,

  /**
   * The cursor's two quantities as a plain object.
   *
   * `assert.deepEqual` compares prototypes, and every cursor coming out of the
   * domain is an F# record instance — so comparing one against `{ Offset, ... }`
   * fails on the class, not on the values, and the diff blames the wrong thing.
   */
  read: (value) => ({ offset: offsetValue(value.Offset), failures: value.ConsecutiveFailureCount }),
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
      const result = resultOf(m.applyAdvance(identity, offsetOf(prevOffset), offsetOf(nextOffset), count, current))
      return result.ok ? result : { ok: false, error: caseOf(result.error) }
    },

    applyExhausted: (current) => m.applyExhausted(current),
    recordSuccess: (current) => m.recordSuccess(current),
    mayContinue: (budget, current) => m.mayContinue(budget, current),

    /** The durable state as plain JS, so a renamed field cannot read `undefined`. */
    read: (current) => ({
      logicalRun: idValue.logicalRun(current.LogicalRunId),
      authorityRoot: idValue.authorityRoot(current.AuthorityRootUserMessageId),
      offset: offsetValue(current.Cursor.Offset),
      failures: current.Cursor.ConsecutiveFailureCount,
      dedupeKeys: listItems(current.RecentFailureKeys).length,
      exhausted: current.Exhausted,
    }),
  }
})()

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
    'ParentWorkRecordInstruction',
    'RequirementsInstruction',
    'render',
    'relay',
  ])

  return {
    baseInstructions: listItems(m.BaseInstructions),
    parentWorkRecordInstruction: m.ParentWorkRecordInstruction,
    requirementsInstruction: m.RequirementsInstruction,

    render: ({ assignment, parentWorkRecord, originalUserRequirements = [], payload, tdd }) =>
      m.render(
        new ForkChildPayloadModule.ForkChildAssignment(
          assignment,
          parentWorkRecord,
          toList(originalUserRequirements),
          payload,
          tdd === undefined ? undefined : tddPhase.parse(tdd).value,
        ),
      ),

    relay: (assignment, parentWorkRecord, requirements = [], payload) =>
      m.relay(assignment, parentWorkRecord, toList(requirements), payload),
  }
})()

/**
 * Coder TDD phase (closed Red | Green). Wire codec + child assignment text.
 * Used by named `coder` (required tdd) and Manager `fork` (optional tdd).
 * Obtain DU values via `parse("red"|"green")` — no ordinal construction.
 */
export const tddPhase = (() => {
  const m = bind(TddPhaseModule, 'TddPhase', [
    'wireName',
    'parseTddPhase',
    'RedAssignment',
    'GreenAssignment',
    'assignmentText',
    'composeAssignment',
  ])

  return {
    wireName: (phase) => m.wireName(phase),
    parse: (raw) => resultOf(m.parseTddPhase(raw)),
    redAssignment: m.RedAssignment,
    greenAssignment: m.GreenAssignment,
    assignmentText: (phase) => m.assignmentText(phase),
    composeAssignment: (phase, prompt) => m.composeAssignment(phase, prompt),
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
  /** ENFORCER-071: previous tip as low-trust assistant body. */
  previousTip: (tipField, cycleId) => CompanionPromptModule.previousTipMessage(tipField, cycleId),
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

  /** ENFORCER-071: stable id for one previous_enforcer_tip message. */
  previousTipMessageId: (sha256, { blogger, cycleId }) =>
    CompanionIdentityModule.previousTipMessageId(sha256, sessionId(blogger), cycleId),
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
     * `previousTips` is `[{ field, cycleId }]` (oldest → newest); default empty.
     *
     * The tuple lists are converted here: an F# tuple is a JS array, and a `list` of
     * them still needs `toList` or it folds as empty.
     */
    build: (sha256, { blogger, epoch, kind, frames, delta, previousTips = [] }) => {
      const plan = m.build(
        sha256,
        sessionId(blogger),
        frameEpochId(epoch),
        kind,
        toList(frames.map((f) => [blobDigest(f.digest), f.body])),
        delta === undefined ? undefined : [delta.messageId, delta.toml],
        toList(previousTips.map((t) => [t.field, t.cycleId])),
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
    all: ['WorkMain', 'BloggerMain', 'BloggerSquash', 'InteractionRepair'].map(of),

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
    mayRecover: (arming, offset, hasMaterial) => m.mayRecover(arming, offsetOf(offset), hasMaterial),

    /** `{ name, clearsFailureCount, advancesCursor, nextArming }`. */
    onSquash: (outcome) => decisionOf(m.onSquashOutcome(buildOutcome(outcome, []))),

    onMain: ({ kind, aabbConsumed = false, outcome }) =>
      decisionOf(m.onMainOutcome(kind, aabbConsumed, buildOutcome(outcome, []))),
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
    'isTeacher',
    'isSatellite',
    'tryMainSessionOf',
    'tryBloggerOf',
    'tryTeacherOf',
    'link',
    'unlink',
    'describe',
  ])

  return {
    empty: m.empty,

    isCompanion: (id, current) => m.isCompanion(sessionId(id), current),
    isTeacher: (id, current) => m.isTeacher(sessionId(id), current),
    isSatellite: (id, current) => m.isSatellite(sessionId(id), current),

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
        mainSessionId: kind === 'SatelliteSession' ? idValue.session(found.Kind.fields[0]) : undefined,
        satelliteKind: kind === 'SatelliteSession' ? caseOf(found.Kind.fields[1]) : undefined,
        blogger: isNone(found.BloggerSessionId) ? undefined : idValue.session(found.BloggerSessionId),
        teacher: isNone(found.TeacherSessionId) ? undefined : idValue.session(found.TeacherSessionId),
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
    'managerForkableRoles',
    'managerForkableNames',
    'requiredNames',
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
    managerForkableRoles: () => listItems(m.managerForkableRoles).map(caseOf),
    managerForkableNames: () => listItems(m.managerForkableNames),
    /** AGENT-002: exactly 20 names. */
    requiredNames: () => listItems(m.requiredNames),
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
 * COMPANION-009 / CTX-010: which prefix X sends, as a `ProjectionIntent` (PROJ-005).
 *
 * `frozenBBody` is supplied by the caller because the snapshot carries a `BlobRef`,
 * never the body (PERSIST-007). Passing it here is the same split `ResolvedPrefixMemory`
 * makes in production: the journal records where the body is, and only a resolved copy
 * reaches the transform boundary.
 *
 * The facade flattens the intent into the plan-shaped view the legacy `XPrefixPlan`
 * exposed, so tests keep asserting the same business facts (drop leading count,
 * synthetic id reuse, low-trust memory block, replace-or-not) without learning the
 * union wire shape.
 */
export const xPrefix = (() => {
  const m = bind(XPrefixModule, 'XPrefixProjection', ['forSnapshot', 'forChoice', 'requiredBlob'])

  const intentOf = (intent) => {
    const name = caseOf(intent)

    if (name === 'KeepPhysicalPrefix') {
      return {
        intent: name,
        replacesPrefix: false,
        dropLeading: 0,
        memoryId: undefined,
        memoryText: undefined,
      }
    }

    const activation = payloadOf(intent)

    return {
      intent: name,
      replacesPrefix: true,
      dropLeading: activation.DropLeading,
      memoryId: activation.SyntheticMessageId,
      memoryText: activation.Memory,
    }
  }

  return {
    forSnapshot: (snapshot, frozenBBody = '') => intentOf(m.forSnapshot(snapshot, frozenBBody)),
    forChoice: (choice, committed, frozenBBody = '') => intentOf(m.forChoice(choice, committed, frozenBBody)),

    /** `undefined` when the plan needs no blob read. */
    requiredBlob: (choice, committed) => {
      const ref = unwrapOption(m.requiredBlob(choice, committed))
      return isNone(ref) ? undefined : idValue.blobRef(ref)
    },
  }
})()

/** PROJ-005: a `ProjectionIntent`, built by case name. */
export const projectionIntent = (() => {
  // Resolved at call time so a missing step-3a case fails the test that needs it,
  // not the whole facade import for stage 1–2 tests.
  const build = (caseName, fields = []) =>
    unionCase(ProjectionAlgebraModule.ProjectionIntent, 'ProjectionIntent')(caseName, fields)

  return {
    get keepPhysicalPrefix() {
      return build('KeepPhysicalPrefix', [])
    },
    activatePrefixEpoch: (activation) => build('ActivatePrefixEpoch', [activation]),
    /**
     * PROJ-008 step 3: Y frames from Snapshot.BlogFrames + Companion rebuild payload.
     * Defaults keep step-3a algebra smokes working (empty session/tips/delta → frames only
     * or empty no-op when Snapshot.BlogFrames is empty).
     */
    insertBlogFrames: (
      intent = {
        RequestKind: 'normal',
        SquashFrameCount: 0,
        BloggerSessionId: 'ses_blogger',
        FrameEpoch: 0,
        PhysicalDelta: undefined,
        PreviousTips: [],
      },
    ) => {
      const payload = {
        RequestKind: intent.RequestKind ?? 'normal',
        SquashFrameCount: intent.SquashFrameCount ?? 0,
        BloggerSessionId: intent.BloggerSessionId ?? 'ses_blogger',
        FrameEpoch: intent.FrameEpoch ?? 0,
        PhysicalDelta:
          intent.PhysicalDelta === undefined || intent.PhysicalDelta === null
            ? undefined
            : Array.isArray(intent.PhysicalDelta)
              ? intent.PhysicalDelta
              : [intent.PhysicalDelta.messageId ?? intent.PhysicalDelta[0], intent.PhysicalDelta.toml ?? intent.PhysicalDelta[1]],
        PreviousTips: toList(
          (intent.PreviousTips ?? []).map((t) =>
            Array.isArray(t) ? t : [t.field ?? t[0], t.cycleId ?? t[1]],
          ),
        ),
      }
      return build('InsertBlogFrames', [payload])
    },
    /** PROJ-008 step 4: InteractionRepair instruction. */
    insertRepair: (intent) => build('InsertRepair', [intent]),
    /** COMPANION-012: drop message ids listed in Snapshot.TransportMessages. */
    get suppressTransportOnly() {
      return build('SuppressTransportOnly', [])
    },
    /** PROJ-008 step 5: REVIEW-003 skeptical challenge. */
    appendReviewChallenge: (intent = { TextVersion: 1 }) => build('AppendReviewChallenge', [intent]),
    /** PROJ-008 step 5 / HOST-013: permanent guideline pair history + next. */
    insertPairProgrammingThought: (intent = { History: [], Next: undefined }) =>
      build('InsertPairProgrammingThought', [
        {
          History: toList(intent.History ?? []),
          Next: intent.Next ?? { CallId: 'pair-marker-1', MarkerText: '' },
        },
      ]),
    /** PROJ-008 step 6: Host compaction reanchor (renderer no-op on wire bytes). */
    get reanchorAfterCompaction() {
      return build('ReanchorAfterCompaction', [])
    },
    nameOf: (intent) => caseOf(intent),
  }
})()
/**
 * PROJ-004/006: the pure planner and canonical renderer of the projection DSL.
 *
 * The renderer's wire view (`renderMessages`) is what a digest is computed from —
 * byte-equal to the Host's decode of the written-back message list, so tests can
 * assert the DSL's bytes without touching Host objects.
 */
export const projectionAlgebra = (() => {
  const planner = bind(ProjectionAlgebraModule, 'ProjectionPlanner', ['plan'])
  // Stage 1–2 members only at load: step-3a APIs resolve lazily so a missing
  // production export fails the step-3a tests rather than the whole facade import.
  const renderer = bind(ProjectionAlgebraModule, 'ProjectionRenderer', ['renderPrefix', 'renderMessages', 'cutoffDigest'])

  const wireViewOf = (messages) =>
    listItems(messages).map((message) => ({
      role: message.Role,
      parts: listItems(message.Parts).map((part) => {
        const kind = caseOf(part)
        const payload = payloadOf(part)
        if (kind === 'WireText' || kind === 'WireReasoning') {
          return { kind, text: payload }
        }
        if (kind === 'WireToolResult') {
          const [callId, result] = part.fields ?? (Array.isArray(payload) ? payload : [undefined, payload])
          return { kind, callId, text: result }
        }
        if (kind === 'WireToolCall') {
          const [callId, name, args] = Array.isArray(payload) ? payload : [undefined, undefined, payload]
          return { kind, callId, name, text: args }
        }
        return { kind, payload }
      }),
    }))

  const renderOf = (rendered) => {
    const name = caseOf(rendered)
    if (name === 'PhysicalPrefix') return { name, activation: undefined }
    return { name, activation: payloadOf(rendered) }
  }

  return {
    /** Result<ProjectionIntent list, ProjectionConflict>. */
    plan: (intents) => {
      const result = resultOf(planner.plan(toList(intents)))

      if (result.ok) {
        return { ok: true, intents: listItems(result.value).map((intent) => caseOf(intent)) }
      }

      const error = result.error
      const conflict = caseOf(error)
      const payload = payloadOf(error)

      // Prefix conflicts carry two intents; other conflicts may be unit-like.
      if (Array.isArray(payload) && payload.length === 2 && payload[0]?.cases) {
        return {
          ok: false,
          conflict,
          first: caseOf(payload[0]),
          second: caseOf(payload[1]),
        }
      }

      return { ok: false, conflict }
    },

    renderPrefix: (intents) => renderOf(renderer.renderPrefix(toList(intents))),

    /** A `RenderedPrefix`, built by case name (for write-back tests). */
    rendered: (() => {
      const build = unionCase(ProjectionAlgebraModule.RenderedPrefix, 'RenderedPrefix')

      return {
        physical: build('PhysicalPrefix', []),
        synthetic: (activation) => build('SyntheticPrefix', [activation]),
        nameOf: (rendered) => caseOf(rendered),
      }
    })(),

    /** wire view: digest-ready description of the rendered bytes. */
    renderMessages: (messages, rendered) => wireViewOf(renderer.renderMessages(messages, rendered)),

    /**
     * PROJ-008 step 3a: fold ordered intents over base wire messages against a
     * ProjectionSnapshot. Lazy: missing production export fails only callers.
     * Production injects real sha256 via renderMessagesWithHostIds; this facade
     * keeps wire-only shape (default identity sha256 inside F#).
     */
    renderMessagesWithIntents: (snapshot, baseWireMessages, orderedIntents) => {
      const render = member(ProjectionAlgebraModule, 'ProjectionRenderer', 'renderMessagesWithIntents')
      const toWirePart = (p) => {
        if (p.kind === 'WireText') return new ProviderProj.WirePart(0, [p.text])
        if (p.kind === 'WireReasoning') return new ProviderProj.WirePart(1, [p.text])
        if (p.kind === 'WireToolCall') return new ProviderProj.WirePart(2, [p.callId, p.name, p.text])
        if (p.kind === 'WireToolResult') return new ProviderProj.WirePart(3, [p.callId, p.text])
        return p
      }
      const toWireMsg = (m) => {
        if (m.Role !== undefined) return m
        const parts = toList((m.parts || []).map(toWirePart))
        return new ProviderProj.WireMessage(m.role, parts)
      }
      const items = Array.isArray(baseWireMessages) ? baseWireMessages : listItems(baseWireMessages)
      const encoded = toList(items.map(toWireMsg))
      return wireViewOf(render(snapshot, encoded, toList(orderedIntents)))
    },

    /**
     * PROJ-004: wire + Host MessageId / IsPhysical side-channel (injected sha256).
     * Lazy: only callers that need ids bind this export.
     */
    renderMessagesWithHostIds: (sha256, snapshot, baseWireMessages, orderedIntents) => {
      const render = member(ProjectionAlgebraModule, 'ProjectionRenderer', 'renderMessagesWithHostIds')
      const toWirePart = (p) => {
        if (p.kind === 'WireText') return new ProviderProj.WirePart(0, [p.text])
        if (p.kind === 'WireReasoning') return new ProviderProj.WirePart(1, [p.text])
        if (p.kind === 'WireToolCall') return new ProviderProj.WirePart(2, [p.callId, p.name, p.text])
        if (p.kind === 'WireToolResult') return new ProviderProj.WirePart(3, [p.callId, p.text])
        return p
      }
      const toWireMsg = (m) => {
        if (m.Role !== undefined) return m
        const parts = toList((m.parts || []).map(toWirePart))
        return new ProviderProj.WireMessage(m.role, parts)
      }
      const items = Array.isArray(baseWireMessages) ? baseWireMessages : listItems(baseWireMessages)
      const encoded = toList(items.map(toWireMsg))
      const rendered = render(sha256, snapshot, encoded, toList(orderedIntents))
      const optionString = (id) => {
        if (isNone(id)) return null
        // Fable may box Some as value or as union with fields[0].
        if (typeof id === 'object' && id !== null && 'fields' in id) {
          const fields = id.fields
          return Array.isArray(fields) && fields.length > 0 ? fields[0] : null
        }
        return id
      }
      return {
        messages: wireViewOf(rendered.Messages),
        hostMessageIds: listItems(rendered.HostMessageIds).map(optionString),
        hostIsPhysical: listItems(rendered.HostIsPhysical),
      }
    },

    renderedOf: renderOf,

    /**
     * CTX-011 step 5: the digest proof of X's current prefix at the candidate
     * cutoff (PROJ-008 stage 2 — attempt-local probe projection).
     */
    cutoffDigest: (sha256, snapshot, cutoff) => renderer.cutoffDigest(sha256, snapshot, cutoff),
  }
})()

/**
 * PROJ-002 step 3a snapshot fields (consumer-driven): BlogFrames,
 * TransportMessages, HostReanchor. Stage-1 fields remain CurrentProjection /
 * CommittedPrefix. Domain mirrors frame kinds as ProjectionBlogFrameKind
 * (not Journal.BlogFrameKind) without Journal dependency.
 *
 * Kind resolution is lazy: missing Domain cases fail step-3a tests only.
 */
/**
 * PROJ-008 Domain constants (ProjectionConstants). Single source for repair /
 * pair / challenge text; Host modules must reference these rather than literals.
 */
export const projectionConstants = (() => {
  const names = ['RepairInstruction', 'PairProgrammingGuidelineText', 'ReviewChallengeText', 'ReviewChallengePrompt']
  const out = {}
  for (const name of names) {
    try {
      out[name] = ProjectionAlgebraModule['ProjectionConstants_' + name] ?? member(ProjectionAlgebraModule, 'ProjectionConstants', name)
    } catch {
      out[name] = undefined
    }
  }
  return out
})()

export const projectionSnapshot = {
  /** Domain ResolvedBlogFrame (digest as hex string). */
  blogFrame: ({ kind = 'Entry', digest = 'frame-digest', body = 'frame body' } = {}) => {
    const kindUnion =
      ProjectionAlgebraModule.ProjectionBlogFrameKind ??
      ProjectionAlgebraModule.BlogFrameKind ??
      ProjectionAlgebraModule.ResolvedBlogFrameKind
    if (kindUnion === undefined) {
      throw new Error(
        'ProjectionAlgebra exports neither ProjectionBlogFrameKind nor BlogFrameKind (PROJ-008 step 3a)',
      )
    }
    const resolvedKind =
      typeof kind === 'string' ? unionCase(kindUnion, 'ProjectionBlogFrameKind')(kind, []) : kind
    return { Kind: resolvedKind, Digest: digest, Body: body }
  },
  hostReanchor: ({ previous = 'epoch-0', next = 'epoch-1', run = 'compact-1' } = {}) => ({
    PreviousEpochId: previous,
    NextEpochId: next,
    ObservedCompactionRunId: run,
  }),
  of: ({
    currentProjection,
    committedPrefix = undefined,
    blogFrames = [],
    transportMessages = [],
    hostReanchor = undefined,
  }) => ({
    CurrentProjection: currentProjection,
    CommittedPrefix: committedPrefix,
    BlogFrames: toList(blogFrames),
    TransportMessages: stringSet(transportMessages),
    HostReanchor: hostReanchor,
  }),
}/** CTX-010: the two prefix choices, built by case name. */
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

/**
 * ENFORCER-045/070/154: enforcement half of BlogEntryCommitted + bounded RecentTips.
 * VERIFY-008: tip RuleId / FieldName / CycleId only via this facade.
 */
export const enforcementProjection = (() => {
  const m = bind(EnforcementProj, 'EnforcementProjection', [
    'empty',
    'applyFromEntry',
    'tryFindByProviderRun',
    'recentTips',
  ])

  return {
    empty: m.empty,
    RecentTipLimit: EnforcementProj.RecentTipLimit ?? 8,

    /** Build an EnforcementCycleRecord (tip v2). */
    cycleRecord: ({
      mainSessionId,
      bloggerSessionId,
      run,
      toolCallIds = [],
      textRef,
      textDigest,
      tipRuleId,
      fieldNameAtCommit,
      evidenceRef,
      prefixEpoch = 0,
    }) => ({
      MainSessionId: typeof mainSessionId === 'string' ? sessionId(mainSessionId) : mainSessionId,
      BloggerSessionId: typeof bloggerSessionId === 'string' ? sessionId(bloggerSessionId) : bloggerSessionId,
      ProviderRun: typeof run === 'string' ? providerRun(run) : run,
      ToolCallIds: toList(toolCallIds.map((id) => (typeof id === 'string' ? toolCallId(id) : id))),
      CycleTextRef: typeof textRef === 'string' ? blobRef(textRef) : textRef,
      CycleTextDigest: typeof textDigest === 'string' ? blobDigest(textDigest) : textDigest,
      TipRuleId: tipRuleId,
      FieldNameAtCommit: fieldNameAtCommit,
      CycleEvidenceRef: evidenceRef == null ? undefined : typeof evidenceRef === 'string' ? blobRef(evidenceRef) : evidenceRef,
      ObservedPrefixEpochId: prefixEpochId(prefixEpoch),
    }),

    applyFromEntry: (state, record) => resultOf(m.applyFromEntry(state, record)),

    tryFindByProviderRun: (run, state) => {
      const key = typeof run === 'string' ? providerRun(run) : run
      return unwrapOption(m.tryFindByProviderRun(key, state))
    },

    /** Oldest → newest RecentTip list (plain objects). */
    recentTips: (state) =>
      listItems(m.recentTips(state)).map((t) => ({
        ruleId: t.RuleId,
        fieldName: t.FieldName,
        cycleId: t.CycleId,
      })),

    tipRuleIdOf: (record) => record?.TipRuleId,
    fieldNameAtCommitOf: (record) => record?.FieldNameAtCommit,
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

// ── review (docs/what/review.md) ─────────────────────────────────────────────────────────

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
  const m = bind(Challenge, 'ReviewChallenge', ['Text', 'TextVersion', 'Prompt', 'contentDigest'])

  return {
    text: m.Text,
    /** ARCH-010 instruction form (`# Text\\n`); seal / nudge / algebra AppendReviewChallenge. */
    prompt: m.Prompt,
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
  // PROJ-004: the one write-back adapter of the projection DSL's prefix stage.
  applyRenderedPrefix: (rawMessages, rendered) =>
    listItems(ProjectionModule.applyRenderedPrefix(rawMessages, rendered)),
}

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
    Kind: typeof kind === 'string' ? buildCompletionKind(kind) : kind,
    CompletionRef: ref,
    CompletionDigest: digest,
  })

  return {
    empty: m.empty,
    link: (handle, child, targetAgent, role, current, ownership = handleOwnership.durableParentHandle()) =>
      decided(m.link(handle, child, targetAgent, role, ownership, current)),
    complete: (handle, completion, current) =>
      decided(m.complete(handle, typeof completion === 'string' ? completionOf(completion) : completion, current)),
    completionOf,
    abandon: (handle, reason, current) =>
      decided(m.abandon(handle, typeof reason === 'string' ? buildHandleAbandonReason(reason) : reason, current)),
    retire: (handle, current) => decided(m.retire(handle, current)),
    tryFind: (handle, current) => unwrapOption(m.tryFind(handle, current)),
    isRetired: (handle, current) => m.isRetired(handle, current),
    isAbandoned: (handle, current) => m.isAbandoned(handle, current),
    listable: (current) => listItems(m.listable(current)),
    joinable: (current) => listItems(m.joinable(current)),
    reportableAbandoned: (current) => listItems(m.reportableAbandoned(current)),
    activeHandles: (current) => listItems(m.activeHandles(current)),
    tryFindByChildSession: (child, current) => unwrapOption(m.tryFindByChildSession(child, current)),
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
const ChildRecoveryModule = await prod('Domain/ChildRecovery')
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
        typeof reason === 'string' ? buildHandleAbandonReason(reason) : reason,
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
    link: (journal, parentId, agentId, childSessionId, targetAgent, role, ownership = handleOwnership.durableParentHandle()) =>
      resultOf(m.link(journal, parentId, agentId, childSessionId, targetAgent, role, ownership)),
    recordCompletion: (journal, parentId, agentId, kind, body, childSessionId) => {
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
      return resultOf(m.recordCompletion(journal, parentId, proof.value))
    },
    recordAbandon: (journal, parentId, agentId, reason, abandonedAt) =>
      resultOf(
        m.recordAbandon(
          journal,
          parentId,
          agentId,
          typeof reason === 'string' ? buildHandleAbandonReason(reason) : reason,
          abandonedAt,
        ),
      ),
    retire: (journal, parentId, agentId) => resultOf(m.retire(journal, parentId, agentId)),
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
    'tryReadBody',
    'decodeBody',
    'tryMaterialiseRunCompletion',
  ])

  return {
    encodeOutcome: (runId, outcome) => m.encodeOutcome(runId, outcome),
    tryDecode: (record, agentId, json) => resultOf(m.tryDecode(record, agentId, json)),
    tryRead: (journal, record, agentId) => {
      const value = resultOf(m.tryRead(journal, record, agentId))
      return value.ok ? { ok: true, value: unwrapOption(value.value) } : { ok: false, error: value.error }
    },
    tryReadBody: (journal, record) => resultOf(m.tryReadBody(journal, record)),
    decodeBody: (json) => m.decodeBody(json),
    tryMaterialiseRunCompletion: (record, agentId, decoded) =>
      m.tryMaterialiseRunCompletion(record, agentId, decoded),
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
      agentId: c.AgentId,
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
    drainFromJournal: (journal, parentId, maxCount) => {
      const value = resultOf(m.drainFromJournal(journal, parentId, maxCount))
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
            journal.snapshot ?? (() => Folds.empty),
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

// ── prompt authority (docs/what/prompt.md) ───────────────────────────────────────────────

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
 * `Wanxiangshu_OpenCode_PromptDispatcher_Runtime__Runtime_` prefix, so
 * they are absorbed here rather than at the call site (VERIFY-008).
 */
export const promptDispatcher = (() => {
  const sendAgentOwnerRoot = PromptDispatcherSendModule
    .Wanxiangshu_OpenCode_PromptDispatcher_Runtime__Runtime_SendAgentOwnerRoot
  const sendContinuation = PromptDispatcherSendModule
    .Wanxiangshu_OpenCode_PromptDispatcher_Runtime__Runtime_SendContinuation
  // Instance members on Runtime: Fable may emit
  //   Runtime__ProjectionFor
  //   Runtime__ProjectionFor_<hash>   (overload hash)
  //   Wanxiangshu_OpenCode_PromptDispatcher_Runtime__Runtime_ProjectionFor
  // Pick the first matching function export; fail closed if none.
  const projectionForMember = (() => {
    const keys = Object.keys(PromptDispatcherModule)
    const candidates = [
      'Wanxiangshu_OpenCode_PromptDispatcher_Runtime__Runtime_ProjectionFor',
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
    /** Explicit transport failure for InteractionRepair hard-fail paths. */
    retryable: (reason) => buildSendOutcome('Retryable', [reason]),
    fatal: (reason) => buildSendOutcome('Fatal', [reason]),

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
const AgentJournalCreate = bind(AgentJournalModule, 'AgentJournal', [
  'create',
  'createFromBoot',
  'appendAgent',
  'snapshot',
  'revision',
  'snapshotWithRevision',
  'awaitChangeFrom',
  'handleProjection',
  'writeBlob',
])

export const agentJournal = {
  create: ({ directory, runtime = 'rt_1', pid = 4242, startedAt = '2026-01-01T00:00:00Z' } = {}) => {
    const result = resultOf(AgentJournalCreate.create(directory, runtimeId(runtime), pid, utcOffset(startedAt)))
    return result.ok
      ? { ok: true, journal: result.value, dispose: () => result.value.Dispose() }
      : result
  },
  /**
   * PERSIST-004 restart: fold BootSnapshot then open a fresh writer RuntimeId.
   * BootSnapshot must be the F# value from `bootSnapshot.load(directory)`.
   */
  createFromBoot: ({
    directory,
    boot,
    runtime = 'rt_restart',
    pid = 4243,
    startedAt = '2026-01-01T01:00:00Z',
  } = {}) => {
    if (boot === undefined || boot === null) throw new Error('createFromBoot requires boot snapshot')
    const result = resultOf(
      AgentJournalCreate.createFromBoot(
        directory,
        runtimeId(runtime),
        pid,
        utcOffset(startedAt),
        boot,
      ),
    )
    return result.ok
      ? { ok: true, journal: result.value, dispose: () => result.value.Dispose() }
      : result
  },
  appendAgent: (streamId, providerRun, agentFactValue, journal) =>
    resultOf(AgentJournalCreate.appendAgent(streamId, providerRun, agentFactValue, journal)),
  snapshot: (journal) => AgentJournalCreate.snapshot(journal),
  /** Module-level revision (AgentJournal.revision). */
  revision: (journal) => AgentJournalCreate.revision(journal),
  snapshotWithRevision: (journal) => AgentJournalCreate.snapshotWithRevision(journal),
  /** Module-level awaitChangeFrom (fromRevision, journal) → Task/Promise. */
  awaitChangeFrom: (fromRevision, journal) => AgentJournalCreate.awaitChangeFrom(fromRevision, journal),
  handleProjection: (journal, parentId) => AgentJournalCreate.handleProjection(journal, parentId),
  /** Blob write receipt: { BlobRef, BlobDigest } after Ok. */
  writeBlob: (content, journal) => resultOf(AgentJournalCreate.writeBlob(content, journal)),
}

/** F# Boot.boot(directory) — raw BootSnapshot for createFromBoot (not plain-JS projection). */
export const bootSnapshot = {
  load: (directory) => Boots.boot(directory),
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

// ── join completion reliability (Part 1 hang fix) ────────────────────────────

/**
 * Discover a Fable instance-method export without hardcoding hash suffixes.
 * Prefer `TypeName__Method…`; fall back to keys ending with `__Method` / `_Method`.
 */
const fableInstanceMethod = (mod, typeName, methodName) => {
  const keys = Object.keys(mod)
  const preferred = `${typeName}__${methodName}`
  const found =
    keys.find((k) => k === preferred || k.startsWith(`${preferred}_`) || k.startsWith(`${preferred}`)) ??
    keys.find((k) => k.endsWith(`__${methodName}`) || k.endsWith(`_${methodName}`))
  if (found === undefined || typeof mod[found] !== 'function') {
    const near = keys.filter((k) => k.includes(typeName) || k.includes(methodName)).join(', ')
    throw new Error(
      `${typeName}__${methodName} missing (static form). Near: ${near || '(none)'}`,
    )
  }
  return mod[found]
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
    const id = completionOrId.AgentId ?? completionOrId.RunId ?? 'pty'
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
 * ExecutorSummarize map/reduce: summarizeSpool cancels owned children on failure.
 * Fake IExecutorRuntime: Fork / JoinWithPermit / AwaitAgentWithPermit / CancelAgent.
 * Permit-gated in production (requirePermit → HostForkRuntime).
 */
export const executorSummarizeRuntime = (() => {
  const summarizeSpool = member(ExecutorSummarize, 'ExecutorSummarize', 'summarizeSpool')
  const ForkResult = ForkTypesModule.ForkResult
  const ForkError = ForkTypesModule.ForkError
  if (ForkResult === undefined || ForkError === undefined) {
    throw new Error('ForkTypes ForkResult/ForkError missing')
  }

  return {
    summarizeSpool: (runtime, spoolPath) => summarizeSpool(runtime, spoolPath),
    /** Ok(ForkResult.Created agentId) */
    forkOk: (agentId) => okResult(new ForkResult(0, [agentId])),
    timedOut: () => errorResult(ForkError.TimedOut),
    /** Hard fail: FamilyBlocked / real join timeout → ForkError.NotFound (no Waiting retry). */
    notFound: (agentId = 'missing') => errorResult(new ForkError(4, [agentId])),
    /**
     * Fake IExecutorRuntime. JoinWithPermit / AwaitAgentWithPermit return Promise of Result.
     * Default → TimedOut so await fails after fork.
     */
    fake: ({ fork, join, awaitAgent, cancel } = {}) => {
      const cancelled = []
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
        AwaitAgentWithPermit: (agentId, timeoutMs) => Promise.resolve(joinOrAwait(timeoutMs, agentId)),
        CancelAgent: (agentId) => {
          cancelled.push(agentId)
          if (typeof cancel === 'function') cancel(agentId)
        },
      }
      return { runtime, cancelled }
    },
  }
})()

/** Structural markers for HostSignalSubscribe reconnect + heartbeat (emitJsExpr body). */
export const hostSignalSubscribe = (() => {
  const sourcePath = join(BUILD_ROOT, 'Infrastructure/OpenCode/Signals/HostSignalSubscribe.js')
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
 * LOOP-003…005: pure O(1) exponential-mixture 4-gram diversity detector.
 * Whitespace is ignored; physical gate is N_eff. Host abort stays out.
 */
export const loopDetector = (() => {
  const m = bind(LoopDetectorModule, 'LoopDetector', [
    'NgramSize',
    'HashBuckets',
    'K',
    'NormalEffectiveCount',
    'NormalHhi',
    'GarbageEffectiveCount',
    'LoopHhi',
    'LoopEffectiveThreshold',
    'create',
    'pushCharacter',
    'pushText',
    'evaluate',
  ])

  const read = (evaluation) => ({
    state: caseOf(evaluation.State),
    isLoop: Boolean(evaluation.IsLoop),
    effective: evaluation.EffectiveCharacterCount,
    hhi: evaluation.Hhi,
    step: evaluation.Step,
  })

  return {
    ngramSize: m.NgramSize,
    hashBuckets: m.HashBuckets,
    k: m.K,
    normalEffectiveCount: m.NormalEffectiveCount,
    normalHhi: m.NormalHhi,
    garbageEffectiveCount: m.GarbageEffectiveCount,
    loopHhi: m.LoopHhi,
    loopEffectiveThreshold: m.LoopEffectiveThreshold,
    create: () => m.create(),
    pushCharacter: (detector, character) => read(m.pushCharacter(detector, character)),
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


/** LOOP-006 continuation text + FALLBACK-003 writer surface for bridge tests. */
export const runtimeNudge = (() => {
  const m = bind(RuntimeNudgeModule, 'RuntimeNudge', [
    'providerRetry',
    'loopContinue',
    'backgroundJoinGuard',
    'ProviderRetryInstructions',
    'LoopContinueInstructions',
    'BackgroundJoinGuardInstructions',
  ])
  return {
    providerRetry: m.providerRetry,
    loopContinue: m.loopContinue,
    backgroundJoinGuard: m.backgroundJoinGuard,
    providerRetryInstructions: listItems(m.ProviderRetryInstructions),
    loopContinueInstructions: listItems(m.LoopContinueInstructions),
    backgroundJoinGuardInstructions: listItems(m.BackgroundJoinGuardInstructions),
  }
})()

export const fallbackController = (() => {
  const recordConfirmedFailure = member(FallbackControllerModule, 'FallbackController', 'recordConfirmedFailure')
  const mayContinue = member(FallbackControllerModule, 'FallbackController', 'mayContinue')

  return {
    /**
     * LOOP-006 bridge half: after LoopKillArmed is observed, the completion path
     * records one confirmed failure. Tests drive that single writer directly.
     */
    recordConfirmedFailure: (journal, budget, session, run, reason) => {
      const result = resultOf(
        recordConfirmedFailure(journal, budget, sessionId(session), providerRun(run), reason),
      )
      if (!result.ok) return result
      return { ok: true, outcome: caseOf(result.value) }
    },
    mayContinue: (outcomeUnion) => Boolean(mayContinue(outcomeUnion)),
  }
})()


// ── Runtime package resources (install once before EnforcerHost / BlogTool / StaticTools) ──

/** Process-local holder: same contract as SpikePlugin init (RuntimeResources.install load). */
export const runtimeResources = (() => {
  const api = bind(RuntimeResourcesModule, 'RuntimeResources', ['load', 'install', 'current'])
  return {
    load: () => api.load(),
    install: (resources) => api.install(resources),
    /** Plugin-init equivalent for unit tests that drive EnforcerHost without SpikePlugin. */
    installFromPackage: () => api.install(api.load()),
    current: () => api.current(),
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

/** Fixed package-relative read: `resources/<relative>` via import.meta.url. */
export const packageResources = (() => {
  const api = bind(PackageResourcesModule, 'PackageResources', ['readText'])
  return {
    readText: (relativeResourcePath) => api.readText(relativeResourcePath),
  }
})()

/** Explicit prompt catalog load (10 role system prompts). */
export const promptResources = (() => {
  const api = bind(PromptResourcesModule, 'PromptResources', ['load'])
  return {
    load: () => api.load(),
  }
})()

/** Package enforcer catalog.json load + domain validation fail-fast. */
export const enforcerCatalogResource = (() => {
  const api = bind(EnforcerCatalogResourceModule, 'EnforcerCatalogResource', ['load'])
  return {
    load: () => listItems(api.load()),
  }
})()

/**
 * ENFORCER-170 pure catalog validation + EnforcerRule construction.
 * Domain never reads files; tests hand rules via `rule(...)`.
 */
export const enforcerCatalog = (() => {
  // ENFORCER-170: validate + field lookup only. `triples` was a facade ghost —
  // production never exported it; binding it fail-closed every unit import.
  const api = bind(EnforcerCatalogDomainModule, 'EnforcerCatalog', [
    'validate',
    'tryFindByField',
    'fieldNames',
  ])
  const Rule = EnforcerCatalogDomainModule.EnforcerRule
  if (typeof Rule !== 'function') {
    throw new Error('Domain/EnforcerCatalog exports no EnforcerRule constructor')
  }
  return {
    /** Construct one EnforcerRule record (Fable class). */
    rule: ({
      ruleId = 'enforcement-x01',
      fieldName = 'sample-field',
      family = 'X',
      scoreWhen = 'score when',
      nudge = 'nudge text',
      catalogOrdinal = 1,
    } = {}) => new Rule(ruleId, fieldName, family, scoreWhen, nudge, catalogOrdinal),
    /**
     * Result over schemaVersion + rules list.
     * Ok value is a JS array of EnforcerRule (listItems on F# list).
     */
    validate: (schemaVersion, rules) => {
      const result = resultOf(api.validate(schemaVersion, toList(rules)))
      return result.ok ? { ok: true, value: listItems(result.value) } : result
    },
    tryFindByField: (field, rules) => {
      const found = api.tryFindByField(field, toList(rules))
      return isNone(found) ? undefined : found
    },
    fieldNames: (rules) => listItems(api.fieldNames(toList(rules))),
  }
})()

// ── docs/what/enforcer.md: Blogger as Enforcer 纯领域内核 ─────────────────────────────────

export const enforcer = (() => {
  const catalog = bind(EnforcerCatalogResourceModule, 'EnforcerCatalogResource', ['load'])
  const catalogDomain = bind(EnforcerCatalogDomainModule, 'EnforcerCatalog', [
    'tryFindByField',
    'fieldNames',
  ])
  // MissingTipError is a codec string literal export (Fable: EnforcerCodecModule.MissingTipError).
  const codec = bind(EnforcerCodecModule, 'EnforcerCodec', [
    'decodeCall',
    'hasValidText',
    'unknownTipError',
  ])
  const cycle = bind(EnforcerCycleModule, 'EnforcerCycle', ['mergeCalls', 'isValidCycle'])

  // Explicit load: module import no longer reads package resources (0.5.3).
  const catalogRules = listItems(catalog.load())
  const tipOf = (call) => {
    const tip = call?.Tip
    if (!tip) return undefined
    return {
      ruleId: tip.RuleId,
      fieldName: tip.FieldName,
      catalogOrdinal: tip.CatalogOrdinal,
    }
  }

  const missingTipError =
    EnforcerCodecModule.MissingTipError ??
    EnforcerCodecModule.EnforcerCodec_MissingTipError ??
    'missing required argument: tip'

  return {
    /** Packaged catalog rules (resources/enforcer/catalog.json, ENFORCER-170). */
    rules: catalogRules,
    ruleCount: catalogRules.length,
    fieldNames: () => listItems(catalogDomain.fieldNames(toList(catalogRules))),
    MissingTipError: missingTipError,
    unknownTipError: (tipValue) => codec.unknownTipError(tipValue),

    /** ENFORCER-021: exact field → rule (no fuzzy match). */
    tryFindByField: (field) => {
      const rule = unwrapOption(catalogDomain.tryFindByField(field, toList(catalogRules)))
      if (!rule) return undefined
      return {
        ruleId: rule.RuleId,
        fieldName: rule.FieldName,
        catalogOrdinal: rule.CatalogOrdinal,
      }
    },

    /**
     * ENFORCER-020..026 tip codec.
     * Returns `{ ok: true, value }` or `{ ok: false, error }` (VERIFY-008 full structure).
     */
    decodeCall: (rawArgs) => {
      const result = resultOf(codec.decodeCall(toList(catalogRules), mapOf(rawArgs ?? {})))
      if (!result.ok) return result
      const call = result.value
      return {
        ok: true,
        value: {
          text: call.Text,
          evidence: call.Evidence,
          tip: tipOf(call),
        },
      }
    },

    hasValidText: (decoded) => {
      // Accept facade shape or raw CanonicalBlogCall.
      if (decoded && typeof decoded === 'object' && 'text' in decoded && !('Text' in decoded)) {
        return decoded.text != null && String(decoded.text).trim().length > 0
      }
      return codec.hasValidText(decoded)
    },

    /**
     * ENFORCER-042/025: multi-call merge → single CanonicalTip (first by PartOrdinal).
     * `calls` is `[[ordinal, { text, tipField, evidence? }], ...]`.
     */
    mergeCalls: (calls) => {
      const list = toList(
        calls.map(([ordinal, call]) => {
          const tipField = call.tipField ?? call.tip ?? call.Tip?.FieldName
          const decoded = resultOf(
            codec.decodeCall(
              toList(catalogRules),
              mapOf({
                text: call.text ?? call.Text ?? '',
                tip: tipField,
                ...(call.evidence != null || call.Evidence != null
                  ? { evidence: call.evidence ?? call.Evidence }
                  : {}),
              }),
            ),
          )
          if (!decoded.ok) {
            throw new Error(`mergeCalls fixture tip decode failed: ${decoded.error}`)
          }
          return [ordinal, decoded.value]
        }),
      )
      const merged = cycle.mergeCalls(list)
      const tip = {
        ruleId: merged.CanonicalTip.RuleId,
        fieldName: merged.CanonicalTip.FieldName,
        catalogOrdinal: merged.CanonicalTip.CatalogOrdinal,
      }
      return {
        mergedText: merged.MergedText,
        tip,
        mergedEvidence: merged.MergedEvidence,
        multiCall: merged.MultiCall,
      }
    },
    isValidCycle: (merged) => {
      if (merged && typeof merged === 'object' && 'mergedText' in merged) {
        return String(merged.mergedText ?? '').trim().length > 0
      }
      return cycle.isValidCycle(merged)
    },
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

/**
 * EnforcerHost.ContinuationOutcome facade (VERIFY-008).
 * ProjectMessages = continue with non-empty provider view.
 * StopPhysicalRun = project non-empty messages then AbortSession.
 */
export const enforcerContinuation = (() => {
  return {
    tag: (outcome) => caseOf(outcome),
    isProject: (outcome) => caseOf(outcome) === 'ProjectMessages',
    isStop: (outcome) => caseOf(outcome) === 'StopPhysicalRun',
    messages: (outcome) => {
      const tag = caseOf(outcome)
      if (tag === 'ProjectMessages' || tag === 'StopPhysicalRun') {
        return listItems(outcome.fields[0])
      }
      throw new Error(`enforcerContinuation.messages: unexpected '${tag}'`)
    },
    reason: (outcome) => {
      if (caseOf(outcome) !== 'StopPhysicalRun') return undefined
      return outcome.fields[1]
    },
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
    hasFlight: (scope, sessionId) => scope.HasFlight(sessionId),
    tryGetFlight: (scope, sessionId) => projectContext(scope.TryGetFlight(sessionId)),
    consumeStaged: (scope, sessionId) => projectContext(scope.TryTakePendingOffer(sessionId)),
    setCurrentRequest: (scope, sessionId, context) => scope.SetCurrentRequest(sessionId, context),
    peekCurrentRequest: (scope, sessionId) => projectContext(scope.TryPeekCurrentRequest(sessionId)),
    clearCurrentRequest: (scope, sessionId) => scope.ClearCurrentRequest(sessionId),
    // Physical drain-window slot (PR7 Slice 4 D6: cell dual-write removed).
    getDrainWindow: (scope, sessionId) => scope.GetDrainWindow(sessionId),
    setDrainWindow: (scope, sessionId, window) => scope.SetDrainWindow(sessionId, window),
    isDrainOpen: (scope, sessionId) => scope.IsDrainOpen(sessionId),
    dispose: (scope) => scope.Dispose(),
  }
})()
const SessionRecoveryModule = await prod('Domain/SessionRecovery')

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
                const a = Ids.session.value(left)
                const b = Ids.session.value(right)
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

// ── Orchestrator direct-CE workflow (FLOW-001 / PR3) ─────────────────────────
// Application/Orchestration/Program.fs is the sole production entrypoint.
// Domain AST + OrchestratorInterpreter were deleted; tests use fake ports via
// orchestratorRuntime, not reply-bearing Program trees.
export const orchestratorProgram = (() => {
  let cached
  const load = async () => {
    if (cached) return cached
    cached = await prod('Application/Orchestration/Program')
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
const JoinModule = await prod('Application/Reconciliation/Join')
// AgentCompletion loaded early (AgentCompletionModuleEarly) for mailbox dual-channel.
const AgentCompletionModule = AgentCompletionModuleEarly
const JoinResultRendererModule = await prod('Infrastructure/OpenCode/Codec/JoinResultRenderer')
const ManagerJobModule = await prod('Application/Orchestration/ManagerJob')

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
    agentId,
    agentName = '',
    role = 'Coder',
    outcome,
    completedAt = new Date(),
  }) => ({
    RunId: runId,
    AgentId: agentId,
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
 * EXEC-004 rev.2 / docs/how/synthetic-toml.md §9.6 JoinResultRenderer — LLM-facing join wire only.
 * `runtime` for agent/pty batch is a minimal { IsPtyCompletion, TryFindAgent } surface.
 */
export const joinResultRenderer = (() => {
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
    renderInterrupted: () => renderInterruptedFn(),
    renderCompletedBatch: (runtime, batch) => {
      const isPty = (runId) =>
        typeof runtime?.IsPtyCompletion === 'function' ? !!runtime.IsPtyCompletion(runId) : false
      const resolve = (agentId) => {
        if (typeof runtime?.TryFindAgent !== 'function') return ''
        const rec = runtime.TryFindAgent(agentId)
        if (!rec) return ''
        return rec.Agent ?? rec.agent ?? ''
      }
      return renderCompletedBatchFn(isPty, resolve, batch)
    },
    /** Production JoinTool path: NonEmptyBatch<JoinItem> with PtyAborted intact. */
    renderJoinItemBatch: (resolveAgentName, batch) => {
      const resolve =
        typeof resolveAgentName === 'function'
          ? resolveAgentName
          : (agentId) => {
              if (typeof resolveAgentName?.TryFindAgent !== 'function') return ''
              const rec = resolveAgentName.TryFindAgent(agentId)
              if (!rec) return ''
              return rec.Agent ?? rec.agent ?? ''
            }
      return renderJoinItemBatchFn(resolve, batch)
    },
    renderOrchestratorBatch: (batch) => renderOrchestratorBatchFn(batch),
    renderForkError: (error) => renderForkErrorFn(error),
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
const ReconcileProgramModule = await prod('Domain/ReconcileProgram')

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
      return (rereadsRemaining, evidence) => applyArgs(fn, [rereadsRemaining, evidence])
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
