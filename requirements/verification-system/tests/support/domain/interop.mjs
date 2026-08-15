// tests/unit/support/domain/interop.mjs — the ONLY file that knows Fable's
// output shape: module loading, emitted-name resolution, and the Fable-mechanics
// helpers every family adapter builds on.
//
// VERIFY-008. Production is .fs; layers 1-3 tests are .mjs consuming dist.
// Fable's emitted names and container shapes are compiler artifacts, not domain
// concepts, so they are confined here — exactly as VERIFY-005 confines dynamic
// Host access to adapters and codecs.
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
//
// Split from domain.mjs (Wave 1): this is the loading/mechanics half; the
// family adapters live in the sibling files and import the module references
// and helpers exported here.

import { existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'


export const BUILD_ROOT = new URL('../../../../../dist/', import.meta.url).pathname

// ── locating the emitted library ─────────────────────────────────────────────
// The fable-library directory carries its version (fable-library-js.5.13.0).
// Hardcoding it would make a Fable upgrade look like a test failure.

export const FABLE_MODULES = join(BUILD_ROOT, 'fable_modules')

export const fableLibraryDir = (() => {
  const candidates = readdirSync(FABLE_MODULES).filter((entry) => entry.startsWith('fable-library-js.'))
  if (candidates.length !== 1) {
    throw new Error(
      `expected exactly one fable-library-js.* in ${FABLE_MODULES}, found: ${candidates.join(', ') || '(none)'}`,
    )
  }
  return join(FABLE_MODULES, candidates[0])
})()

export const lib = (name) => import(join(fableLibraryDir, name))
export const prod = (name) => import(join(BUILD_ROOT, `${name}.js`))

export const [DateOffset, FsMap, FsList, FsResult, FsSet, FsUtil, AsyncBuilder] = await Promise.all([
  lib('DateOffset.js'),
  lib('Map.js'),
  lib('List.js'),
  lib('Result.js'),
  lib('Set.js'),
  lib('Util.js'),
  lib('AsyncBuilder.js'),
])

export const [
  Identity,
  RolesModule,
  FactModule,
  Outcome,
  EnvelopeModule,
  FoldModule,
  FactCodec,
  WriterModule,
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
  ProviderWireDecode,
  ProviderWireCapture,
  ProjectionMessageEdit,
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
  ProjectionIntentModule,
  ProjectionPlannerModule,
  ProjectionRendererModule,
  AttemptPlannerModule,
  Distillation,
  Authority,
  AuthorityRun,
  Witness,
  Challenge,
  ProviderProj,
  XTraceModule,
  MagicTodoModule,
  MagicTodoAdmissionModule,
  MagicTodoFactsModule,
  MagicTodoProjectionModule,
  MagicTodoFactCodecModule,
  LifecycleWorkRecordModule,
  ManagedAgentCatalogModule,
  ProviderLanguageModule,
  XTraceCaptureModule,
  LifecycleWorkRecordProjectionModule,
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
  ProviderResourcesModule,
  EnforcerCatalogDomainModule,
  EnforcerCodecModule,
  EnforcerCycleModule,
  BloggerRequestContextModule,
  BloggerRuntimeModule,
  ParkedTransformModule,
  PluginRuntimeScopeModule,
  SharedStateModule,
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
  MagicTodoLocalityModule,
  MagicTodoMembraneModule,
  ToolHostCodecModule,
  MagicTodoHostCodecModule,
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
  prod('Foundation/Identity'),
  prod('Foundation/Roles'),
  prod('Composition/Durable/Fact'),
  prod('Foundation/Outcome'),
  prod('Persistence/Journal/Envelope'),
  prod('Composition/Durable/Fold'),
  prod('Persistence/Journal/FactCodec'),
  prod('Persistence/Journal/Writer'),
  prod('Context/Companion/Blogger/Projection'),
  prod('Enforcer/Projection'),
  prod('Context/Prefix/Epoch'),
  prod('Participant/Provider/Attempt/Fallback/Projection'),
  prod('Mission/Review/Barrier/Projection'),
  prod('Execution/Delegation/LinkageProjection'),
  prod('Change/Projection'),
  prod('Execution/Session/Association'),
  prod('Participant/Provider/Attempt/Cursor'),
  prod('Participant/Provider/Attempt/TerminalValidity'),
  prod('Context/Prefix/Candidate'),
  prod('Participant/Provider/Attempt/RecoverySlot'),
  prod('Host/CompactionPolicy'),
  prod('OpenCode/Host/Diagnostic'),
  prod('OpenCode/Codec/ProviderWireDecode'),
  prod('OpenCode/Codec/ProviderWireCapture'),
  prod('OpenCode/Codec/ProjectionMessageEdit'),
  prod('Foundation/SyntheticToml'),
  prod('Host/Contract/ToolResultBound'),
  prod('Execution/Delegation/Fork/Payload'),
  prod('Context/Companion/Blogger/Toml'),
  prod('Context/Companion/Blogger/Delta'),
  prod('Context/Companion/Prompt'),
  prod('Context/Companion/Identity'),
  prod('Context/Companion/Builder'),
  prod('Context/Prefix/ProbeSelection'),
  prod('Context/Prefix/Projection'),
  prod('Participant/Provider/Projection/Intent'),
  prod('Participant/Provider/Projection/Planner'),
  prod('Participant/Provider/Projection/Renderer'),
  prod('Participant/Provider/Attempt/Planner'),
  prod('OpenCode/Tools/Distillation'),
  prod('Interaction/Authority/Model'),
  prod('Interaction/Authority/Run'),
  prod('Mission/Review/Judgement/Witness'),
  prod('Mission/Review/Judgement/Challenge'),
  prod('Participant/Provider/Projection/Model'),
  prod('Context/Trace/Model'),
  prod('Mission/Obligation/Todo/Model'),
  prod('Mission/Obligation/Todo/Admission'),
  prod('Mission/Obligation/Todo/Facts'),
  prod('Mission/Obligation/Todo/Projection'),
  prod('Mission/Obligation/Todo/MagicTodoFactCodec'),
  prod('Mission/WorkRecord/Model'),
  prod('Participant/Persona/ManagedCatalog'),
  Promise.all([prod('Participant/Provider/Language'), prod('Participant/Provider/SessionLanguage')]).then(([p, s]) => ({ ...p, ...s })),
  prod('Context/Trace/Capture'),
  prod('Mission/WorkRecord/Materialize'),
  prod('OpenCode/Codec/HostMessageCodec'),
  prod('Process/Deadline'),
  prod('Process/ProcessRequest'),
  prod('Foundation/Parallel'),
  prod('Change/Runtime'),
  prod('Change/Types'),
  prod('Resources/RuntimeResources'),
  prod('Resources/EnforcerCatalogResource'),
  prod('Resources/PackageResources'),
  prod('Resources/PromptResources'),
  prod('Resources/ProviderResources'),
  prod('Enforcer/Catalog'),
  prod('Enforcer/Codec'),
  prod('Enforcer/Cycle/Model'),
  prod('Context/Companion/Blogger/Request'),
  prod('Context/Companion/Blogger/Runtime/State'),
  prod('Context/Companion/Blogger/Runtime/ParkedTransform'),
  prod('OpenCode/Host/PluginRuntimeScope'),
  prod('OpenCode/Host/SharedState'),
  prod('Persistence/Journal/AgentJournal'),
  prod('Interaction/Dispatch/Dispatcher'),
  prod('Interaction/Dispatch/Send'),
  prod('OpenCode/Codec/HostEventCodec'),
  prod('Execution/Session/LoopDetector'),
  prod('OpenCode/Codec/LoopEventCodec'),
  prod('OpenCode/Host/LoopSensor'),
  prod('Interaction/Dispatch/Nudge'),
  prod('Participant/Provider/Attempt/Fallback/Ledger'),
  prod('Execution/Delegation/Handle/Controller'),
  prod('Execution/Delegation/Handle/CompletionCodec'),
  prod('Execution/Delegation/Handle/JoinDrain'),
  prod('Mission/Review/Assurance/Seal'),
  prod('OpenCode/Host/SessionSnapshotPort'),
  prod('Mission/Obligation/Todo/MagicTodoLocality'),
  prod('Mission/Obligation/Todo/MagicTodoMembrane'),
  prod('OpenCode/Codec/ToolHostCodec'),
  prod('Mission/Obligation/Todo/OpenCode/HostCodec'),
  prod('Execution/Session/Wait/CompletionMailbox'),
  prod('Execution/Session/AgentCompletion'),
  prod('Execution/Delegation/Fork/Host/RunLifecycle'),
  prod('Execution/Delegation/Fork/Host/PendingRun'),
  prod('OpenCode/Host/Events'),
  prod('Composition/Turn/Supervisor'),
  prod('Composition/Turn/Binding'),
  prod('Execution/Delegation/Fork/Runtime'),
  prod('Execution/Delegation/Fork/Model'),
  prod('OpenCode/Signals/HostSignalSubscribe'),
  prod('OpenCode/Host/ManagedAgentConfig'),
])

// ProjectionAlgebra split into three owner files (Wave 1). Tests keep one
// aggregate handle so the Fable-module naming boundary stays in one place.
export const ProjectionAlgebraModule = {
  ...ProjectionIntentModule,
  ...ProjectionPlannerModule,
  ...ProjectionRendererModule,
}

// Codec/Projection split into three owner files (Wave 3). Tests keep one
// aggregate handle so the Fable-module naming boundary stays in one place.
export const ProjectionModule = {
  ...ProviderWireDecode,
  ...ProviderWireCapture,
  ...ProjectionMessageEdit,
}

export const [NodeProcessWaitModule, NodeProcessHostModule, PtyTimingModule, FableTask, FableTypes] =
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
// a JS built-in or reserved word. A past case was `ReviewChallenge.Text` emitting
// as `Text$`; reading `.Text` off the module gave `undefined`. Trying the `$`
// spelling here is what makes that a resolution rule instead of a per-call-site
// accident.

export const member = (mod, moduleName, name) => {
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
export const bind = (mod, moduleName, names) =>
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

/** Fable structural comparer, kept behind the test anti-corruption boundary. */
export const structuralComparer = { Compare: (left, right) => FsUtil.compare(left, right) }

/**
 * Fable's uncurry2 — converts a curried function into an uncurried pair.
 *
 * Production call sites do `curry2(context.AttachAbort)(callback)`; the
 * curry/uncurry round-trip goes through the fable-library `curried` WeakMap,
 * so a locally-written lookalike would defer the callback to the detach call.
 * Re-exporting the real function keeps the registration immediate and keeps
 * compiler-runtime imports inside this boundary.
 */
export const uncurry2 = FsUtil.uncurry2

/** [key,value][] → FSharpMap without exposing compiler-runtime imports to tests. */
export const mapOfEntries = (entries, comparer = ordinalComparer) => FsMap.ofArray(entries, comparer)

/** FSharpList → array. */
export const listItems = (list) => FsList.toArray(list)

// F# `Set<string>` needs a comparer object; Fable does not infer one from the
// element type. `REVIEW-010`'s `IncludedToolResultDigests` is such a set, and it
// is the causal evidence a confirmation rests on — so a test that built it wrong
// would be proving the wrong thing about the most load-bearing check in docs/what/review.md.
export const ordinalComparer = { Compare: (left, right) => (left < right ? -1 : left > right ? 1 : 0) }

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
export const requireList = (value, label) => {
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

export const caseNames = (unionClass) => Object.create(unionClass.prototype).cases()

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
export const unionCase = (unionClass, label) => {
  const names = caseNames(unionClass)
  return (caseName, fields = []) => {
    const index = names.indexOf(caseName)
    if (index < 0) throw new Error(`${label} has no case '${caseName}'. Available: ${names.join(', ')}`)
    return names.length === 1 ? new unionClass(fields[0]) : new unionClass(index, fields)
  }
}

// ── fallback (docs/what/fallback.md) ───────────────────────────────────────────────────────

/** FALLBACK-002: the facade keeps the historical numeric offset signature; the
 * closed DU lives only inside the domain. Declared in declaration order, so the
 * numeric tag is the case index.
 */
const FALLBACK_OFFSET_NAMES = ['Fork0', 'Fork1', 'Fork2', 'Fork3']
export const offsetOf = (n) => {
  // Already a Fable union instance (from `cursor.initial`, `recordFailure`, ...).
  if (n && typeof n === 'object') return n
  const idx = Number(n)
  if (!Number.isInteger(idx) || idx < 0 || idx > 3) {
    throw new Error(`FallbackOffset 0..3 has no case for ${n}`)
  }
  return unionCase(Cursor.FallbackOffset, 'FallbackOffset')(FALLBACK_OFFSET_NAMES[idx], [])
}
export const offsetValue = (offset) => (offset === undefined ? undefined : offset.tag)

export const fableInstanceMethod = (mod, typeName, methodName) => {
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