#!/usr/bin/env node
// Architecture gates (VERIFY-005). Layer 0 of the test pyramid: pure filesystem
// and regex judgements, no build artifacts, runnable at any stage.
//
// Migrated from tests-next/Gates/{ArchitectureGates,ArchitectureGates17,
// ArchitectureGateSupport}.fs. Reasons for moving out of the test suite:
//   - gates had to be compiled before they could inspect source
//   - gate red and behaviour red shared one signal, so the anneal phase could
//     not open feedback layer by layer
//   - the F# gates had to exclude themselves from their own token scan; a
//     checker living in scripts/ is simply outside the scanned tree
//
// Line-count gates are deliberately absent (VERIFY-005): line count is a
// symptom, and gating on it rewards mechanical splitting that fragments
// cohesive semantic boundaries. The mechanical-suffix allowlist below is the
// real anti-evasion gate.
//
//   node scripts/architecture-gate.mjs

import { existsSync, readFileSync } from 'node:fs'
import { basename, extname } from 'node:path'
import { walk } from './repo-scan.mjs'

const PRODUCTION_ROOT = 'src/Wanxiangshu.Next'
const TESTS_ROOT = 'tests-mjs'
const SOURCE_EXTENSIONS = ['.fs', '.fsproj']

// VERIFY-008: layers 1-3 are `.mjs` importing `build/next`. The test tree is
// scanned with its own extensions rather than the production ones, so that
// deleting `tests-next/` does not silently empty the test-side scan — an empty
// scan makes every gate over it vacuously pass.
const TEST_EXTENSIONS = ['.mjs']

// ── forbidden vocabulary ────────────────────────────────────────────────────

// Program-counter and framework-ceremony names (ARCH-001, ARCH-008).
// C8: bare CurrentStage / StepIndex banned as program counters. Standalone
// Phase is NOT listed — word-boundary match would false-positive on comments
// that cite the ban itself (e.g. PromptRecovery "no Stage/Phase counter").
// Compound forms (FallbackPhase, ReviewPhase, SessionStage, …) already cover
// historical stage-like type names.
const FORBIDDEN_TOKENS = [
  'idleProposals',
  'callOnce',
  'CurrentStage',
  'StepIndex',
  'FallbackPhase',
  'FallbackState',
  'ContinuationStage',
  'ReviewPhase',
  'ReviewStages',
  'SessionStage',
  'JoinOwner',
  'NudgeLease',
  'CompactionGeneration',
  'SessionActor',
  'SubsessionActor',
  'WorkflowRegistry',
  'JournalDrivenWorkflow',
  'TodoState',
  'Methodology',
  'SquadWave',
  'EventStore',
  'SessionDriverRegistry',
  'EventBus',
  'MailboxProcessor',
  'workspace lockfile',
  'Wait(predicate)',
  'sleepJs',
  'type ReviewState',
  'recordFailureForTests',
  'Advisor',
]

// Fragment events that must die at the earliest boundary (ARCH-002, HOST-002).
const FORBIDDEN_SSE_TOKENS = [
  'message.part.delta',
  'message.part.updated',
  'message.updated',
  'session.diff',
  'session.updated',
]

const SESSION_STATUS_ALLOWLIST = [
  'src/Wanxiangshu.Next/Infrastructure/OpenCode/Codec/HostEventCodec.fs',
  'src/Wanxiangshu.Next/Infrastructure/OpenCode/Signals/HostSignalAdapter.fs',
  'src/Wanxiangshu.Next/Infrastructure/OpenCode/Signals/HostSignalSubscribe.fs',
]

const SLEEP_TOKENS = ['sleepJs', 'sleep']

// ── file naming ─────────────────────────────────────────────────────────────

const MECHANICAL_SUFFIXES = ['Helpers', 'Primitives', 'Fields', 'Emit', 'Service', 'Core']

const MECHANICAL_ALLOWLIST = new Map([
  ['src/Wanxiangshu.Next/Session/AgentRoleIdentity.fs', 'semantic boundary pending deeper split'],
  ['src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/PluginHostInterop.fs', 'semantic boundary pending deeper split'],
  ['src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/TerminalPolicy.fs', 'semantic boundary pending deeper split'],
])

// ── Host boundary ───────────────────────────────────────────────────────────

const HOST_INTEROP_MARKERS = ['Fable.Core.JsInterop', 'jsNative', 'createObj', 'unbox']
const DYNAMIC_ACCESS = /[\w)]\?[a-zA-Z]/
const HOST_INTEROP_NAME =
  /(Host|Port|Codec|Adapter|Boot|Runtime|Writer|Node|Plugin|Supervisor|Backend|Projection|Transform|Signal|Json|Git|Flow|Pty|Tool|Subscribe|Canonical|Process)/i

const HOST_INTEROP_ALLOWLIST = new Map([
  ['src/Wanxiangshu.Next/Infrastructure/Git/Orchestrator.IntegrationGate.fs', 'external lockfile host adapter'],
  ['src/Wanxiangshu.Next/Infrastructure/Git/Orchestrator.WorktreeResource.fs', 'external worktree/ValueTask adapter'],
  ['src/Wanxiangshu.Next/Tools/PromptAssets.fs', 'prompt asset construction at the Host boundary'],
  ['src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/ManagerConfig.fs', 'Host configuration adapter'],
  ['src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/ManagedAgentConfig.fs', 'Host-final opencode.json adapter'],
  ['src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/Diagnostic.fs', 'HOST-007 diagnostic emit at the console boundary (CTX-014 field whitelist)'],
  [
    'src/Wanxiangshu.Next/Kernel/Flow.fs',
    'JS runtime primitives (ValueTask await, deferred Task, Promise.all) — not OpenCode Host objects',
  ],
  [
    'src/Wanxiangshu.Next/Session/BloggerCoordinator.fs',
    'C5: createObj context payload for durable request materialization blob (CanonicalJson boundary)',
  ],
  // BloggerCrashRecovery no longer scrapes JSON; it calls EnforcerHost.tryReloadRequestContext.
])

// Kernel and Domain are the pure core. VERIFY-005 hard-blocks "Kernel 引用 Host
// raw obj", so these directories never earn the filename-pattern excuse — an
// entry must be explicit and reasoned. Without this, next/Kernel/Flow.fs passed
// only because "Flow" happens to appear in HOST_INTEROP_NAME.
const PURE_CORE_DIRS = ['src/Wanxiangshu.Next/Kernel/', 'src/Wanxiangshu.Next/Domain/']

// ── single writer ───────────────────────────────────────────────────────────
//
// `allowed` must list the fold, the codec and Kernel/Fact.fs alongside the real
// writer: the check matches `AgentFact.<Name>`, and a fold's match pattern looks
// exactly like a constructor application. Narrowing it to true construction
// would need to parse F#, so the allowlist carries the distinction instead.
//
// An entry naming a fact that no longer exists is worse than no entry: it can
// never fire, so it reads as an enforced boundary while enforcing nothing.

const SINGLE_WRITER_FACTS = [
  {
    fact: 'FallbackCursorAdvanced',
    allowed: ['Session/FallbackController.fs', 'Journal/AgentJournal.fs', 'Journal/Fold.fs', 'Kernel/Fact.fs'],
    reason: 'only the FallbackController may build the durable fallback cursor fact (FALLBACK-003)',
  },
  {
    fact: 'FallbackExhausted',
    allowed: ['Session/FallbackController.fs', 'Journal/AgentJournal.fs', 'Journal/Fold.fs', 'Kernel/Fact.fs'],
    reason: 'the budget verdict belongs to the same writer that advanced the cursor (FALLBACK-005)',
  },
  {
    fact: 'PluginPromptClaimed',
    allowed: [
      'Application/Prompting/PromptDispatcherSend.fs',
      'Application/Prompting/PromptDispatcher.fs',
      'Journal/PromptAuthorityLedger.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'only PromptDispatcher may claim a plugin prompt (PROMPT-005)',
  },
  {
    fact: 'PluginPromptSubmitted',
    allowed: [
      'Application/Prompting/PromptDispatcherSend.fs',
      'Application/Prompting/PromptDispatcher.fs',
      'Journal/PromptAuthorityLedger.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'a transport receipt is recorded only by the sender that received it (PROMPT-005)',
  },
  {
    fact: 'PluginPromptPhysicalAccepted',
    allowed: [
      'Application/Prompting/PromptDispatcher.fs',
      'Application/Prompting/PromptDispatcherSend.fs',
      'Journal/PromptAuthorityLedger.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'only PromptDispatcher may prove physical acceptance (PROMPT-005)',
  },
  {
    fact: 'PluginPromptAbandoned',
    allowed: [
      'Application/Prompting/PromptDispatcher.fs',
      'Journal/PromptAuthorityLedger.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'only PromptDispatcher may abandon a plugin prompt (PROMPT-005)',
  },
  {
    fact: 'BlogEntryCommitted',
    allowed: [
      'Session/EnforcerHost.fs',
      'Journal/BlogProjection.fs',
      'Journal/EnforcementProjection.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'only EnforcerHost commitCycle may build BlogEntryCommitted (ENFORCER-045)',
  },
  {
    fact: 'BlogSquashCommitted',
    allowed: [
      'Session/EnforcerHost.fs',
      'Session/CompanionJournalPort.fs',
      'Journal/BlogProjection.fs',
      'Journal/BloggerCycleProjection.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'BlogSquashCommitted sole constructors: EnforcerHost.commitSquash (tool loop) and CompanionJournalPort.AppendSquash (legacy port); fold/codec only',
  },
  {
    fact: 'BloggerRequestMaterialized',
    allowed: [
      'Session/BloggerCoordinator.fs',
      'Journal/BloggerCycleProjection.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'only BloggerCoordinator materializes request context before send (C5)',
  },
  {
    fact: 'BloggerRequestAbandoned',
    allowed: [
      'Session/BloggerAbandon.fs',
      'Journal/BloggerCycleProjection.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'BloggerAbandon is the sole production constructor; coordinator/enforcer/crash-recovery call it',
  },
  {
    fact: 'WorktreeCreateRequested',
    allowed: [
      'Application/Orchestration/Orchestrator.fs',
      'Journal/OrchestratorProjection.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'PERSIST-009: only Orchestrator.forkManagerCore claims worktree create before git worktree add',
  },
  {
    fact: 'WorktreeCreated',
    allowed: [
      'Application/Orchestration/Orchestrator.fs',
      'Journal/OrchestratorProjection.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'PERSIST-009: only Orchestrator.forkManagerCore accepts worktree create after git succeeds',
  },
]

// ── DSL programs ────────────────────────────────────────────────────────────

const DSL_PROGRAMS = [
  { builder: 'agent', file: 'Agent/AgentProgram.fs', names: ['forkAgent', 'validateSession', 'runAgentFlow'] },
  {
    builder: 'companion',
    file: 'Session/CompanionProgram.fs',
    names: ['buildDelta', 'runCompanionFlow'],
  },
  { builder: 'orchestrator', file: 'Application/Orchestration/OrchestratorProgram.fs', names: ['run'] },
  { builder: 'process', file: 'Process/ProcessRunner.fs', names: ['run', 'runWithHost'] },
]

// VERIFY-008: the mjs contract that replaced `tests-next/GuideContract/Signatures.fs`.
//
// The F# version asserted types exist at COMPILE time; this one asserts each
// entrypoint is a callable export in `build/next` at RUN time. Weaker about
// signatures, stronger about reachability — a function the build drops now fails.
const GUIDE_CONTRACT_PATH = 'tests-mjs/guide-contract.test.mjs'

// ── layering ────────────────────────────────────────────────────────────────

const LOWER_LAYER_DIRS = ['src/Wanxiangshu.Next/Kernel/', 'src/Wanxiangshu.Next/Domain/']

const UPPER_LAYER_NAMESPACES = [
  'Wanxiangshu.Next.OpenCode',
  'Wanxiangshu.Next.Session',
  'Wanxiangshu.Next.Process',
  'Wanxiangshu.Next.Journal',
  'Wanxiangshu.Next.Orchestrator',
  'Wanxiangshu.Next.Review',
  'Wanxiangshu.Next.Agent',
  'Wanxiangshu.Next.Tools',
]

const DUPLICATE_ALGORITHM_OWNERS = [
  { symbol: 'advance', owners: ['Domain/AgentPairCursor.fs'] },
  { symbol: 'effectiveAgent', owners: ['Domain/AgentPairCursor.fs', 'Domain/PromptAuthority.fs'] },
  { symbol: 'peerAgent', owners: ['Domain/PromptAuthority.fs'] },
  // The single Host crypto adapter. Pure domains take `sha256: string -> string`
  // as a parameter, so Domain/ owns no hash implementation at all.
  { symbol: 'sha256Hex', owners: ['Host/HostDigest.fs'] },
  { symbol: 'reviewWitness', owners: ['Domain/ReviewWitness.fs'] },
  // REVIEW-003's confirmation decision. Domain/ReviewWitness.fs owns it because
  // the decision is a pure function of the two verdict witnesses and the seal;
  // the previous owner was a Flow program that compared tree hash strings.
  { symbol: 'confirm', owners: ['Domain/ReviewWitness.fs'] }, // REVIEW-003

  // Each clause below names ONE source of truth, so each function has one owner.
  { symbol: 'buildAttemptExecutionProfile', owners: ['Domain/PromptAuthority.fs'] }, // PROMPT-008
  { symbol: 'derivePromptKey', owners: ['Domain/PromptAuthority.fs'] }, // PROMPT-011
  { symbol: 'claimScopeDigest', owners: ['Domain/PromptAuthority.fs'] }, // PROMPT-011
  // COMPANION-001/002: "is this a Companion" is a Session-kind fact, not a role
  // predicate. The registry names the association projection because a role-keyed
  // `hasCompanion` is exactly what the clause deleted — a whitelist cannot answer a
  // question role is not an input to.
  { symbol: 'isCompanion', owners: ['Journal/SessionAssociation.fs'] }, // COMPANION-002
  { symbol: 'systemPromptIdFor', owners: ['Domain/PromptAuthority.fs'] }, // AGENT-001
  { symbol: 'toSemantic', owners: ['Domain/ProviderProjection.fs'] }, // VERIFY-007
  { symbol: 'sealDigest', owners: ['Domain/ProviderProjection.fs'] }, // REVIEW-010
  { symbol: 'renderWire', owners: ['Domain/ProviderProjection.fs'] }, // VERIFY-007
  { symbol: 'renderSemantic', owners: ['Domain/ProviderProjection.fs'] }, // VERIFY-007
  { symbol: 'isAppendOnlyPrefix', owners: ['Domain/ProviderProjection.fs'] }, // ARCH-004
]

/// Types whose construction is restricted to one module.
///
/// PROMPT-008 forbids assembling an AttemptExecutionProfile field by field: it
/// must come from `buildAttemptExecutionProfile`, which derives everything
/// derivable so a caller cannot supply a role that disagrees with the agent name.
///
/// F# record construction is structural, so there is no type name to grep for.
/// The `fields` below exist on no other type in the codebase, so a file that
/// assigns all of them IS building one — a real signal, not a heuristic.
///
/// `builder` checks the opposite failure. A constructor with zero call sites
/// satisfies "nobody bypasses me" trivially, because there is nothing to bypass —
/// and that is the state PROMPT-008 was actually in for the whole of packages 0d
/// through X7: the function existed, the gate was green, and every provider request
/// still assembled its own fields from `ActiveLogicalRun`.
const SINGLE_CONSTRUCTOR_TYPES = [
  {
    type: 'AttemptExecutionProfile',
    clause: 'PROMPT-008',
    owner: 'src/Wanxiangshu.Next/Domain/PromptAuthority.fs',
    fields: ['SystemPromptId =', 'ToolCapabilitySet ='],
    builder: 'buildAttemptExecutionProfile',
  },
]

// The active test runner, now two tiers (W4). VERIFY-008 replaced the Fable runner with a plain
// mjs one; VERIFY-004 then split it, because node:test's per-test timeout is a verdict line rather
// than an abort line and a hung test holding a handle parks the whole suite.
//
// Each tier gets the criterion that belongs to it — see the gate below for why one criterion over
// both files would now be satisfiable by the wrong file.
const RUNNER_TIERS = [
  { path: 'tests-mjs/runner.mjs', budget: 'UNIT_VERDICT_SILENCE_MS', enforcement: ['Watchdog'] },
  {
    path: 'tests-mjs/run-inner.mjs',
    budget: 'PER_TEST_TIMEOUT_MS',
    enforcement: ['AbortSignal.timeout', 'timeout:'],
  },
]

// ── bounded concurrency (ARCH-009) ──────────────────────────────────────────
//
// The business layer gets one fan-out primitive: `Parallel.mapBounded`. Direct
// unbounded fan-out makes failure depend on machine load rather than logic, so
// "slow" and "hung" stop being distinguishable — the exact signal VERIFY-004's
// causal-progress gate exists to preserve.
//
// The owner is allowed to use `Promise.all` because that is HOW a bounded
// primitive is built: the semaphore admits `maxConcurrency` at a time and
// `Promise.all` awaits the already-bounded set. Without naming the owner this gate
// would flag the implementation of the very thing it mandates.
const UNBOUNDED_FANOUT = {
  clause: 'ARCH-009',
  owner: 'src/Wanxiangshu.Next/Kernel/Flow.fs',
  patterns: ['Promise.all', 'Task.WhenAll', 'Task.WaitAll'],
}

// ── helpers ─────────────────────────────────────────────────────────────────

const violations = []
const fail = (gate, message) => violations.push({ gate, message })

const norm = (path) => path.replace(/\\/g, '/')
const isFs = (path) => path.endsWith('.fs')
const escapeRegExp = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

/** Loose tokens (parens or spaces) match as substrings; identifiers match on word boundaries. */
const containsToken = (text, token) =>
  /[() ]/.test(token)
    ? text.includes(token)
    : new RegExp(`\\b${escapeRegExp(token)}\\b`, 'i').test(text)

const endsWithAny = (path, suffixes) => suffixes.some((suffix) => norm(path).endsWith(suffix))

const sources = new Map()
const read = (path) => {
  if (!sources.has(path)) sources.set(path, readFileSync(path, 'utf8'))
  return sources.get(path)
}

// The violation is a reference to THIS repo's pre-0.5.0 `src/` tree, which is gone.
//
// Host source paths are not that. AGENTS.md §0 and the SSOT exception protocol both
// REQUIRE citing `../opencode/packages/**/src/**` with line numbers before claiming a
// Host capability gap — and a bare `/src/` substring test forbids exactly that. It
// fired on a comment citing `packages/opencode/src/plugin/index.ts:290`, i.e. on the
// evidence the discipline demands.
const HOST_SOURCE_PATH = /(?:\.\.\/opencode|packages)\/[\w.-]+(?:\/[\w.-]+)*\/src\//

const referencesLegacySrc = (text) => {
  if (text.includes('open Wanxiangshu.') && !text.includes('open Wanxiangshu.Next')) return true

  const withoutHostCitations = text.replace(new RegExp(HOST_SOURCE_PATH.source, 'g'), '')
  return (
    withoutHostCitations.includes('../src') ||
    withoutHostCitations.includes('..\\src') ||
    withoutHostCitations.includes('/src/') ||
    withoutHostCitations.includes('\\src\\')
  )
}

for (const root of [PRODUCTION_ROOT, TESTS_ROOT]) {
  if (!existsSync(root)) {
    console.error(`architecture-gate: required directory '${root}' does not exist`)
    process.exit(1)
  }
}

const productionFiles = walk(PRODUCTION_ROOT, SOURCE_EXTENSIONS)
const testFiles = walk(TESTS_ROOT, TEST_EXTENSIONS)
const allFiles = [...productionFiles, ...testFiles]

// ── gate: legacy vocabulary and src imports ─────────────────────────────────

for (const file of allFiles) {
  const text = read(file)

  for (const token of FORBIDDEN_TOKENS) {
    if (containsToken(text, token)) {
      fail('legacy-vocabulary', `${file}: forbidden legacy workflow token '${token}'`)
    }
  }

  if (referencesLegacySrc(text)) {
    fail('legacy-vocabulary', `${file}: forbidden reference or import to 'src'`)
  }
}

// ── gate: project references ────────────────────────────────────────────────

const PRODUCTION_FSPROJ = 'src/Wanxiangshu.Next/Wanxiangshu.Next.fsproj'

if (!existsSync(PRODUCTION_FSPROJ)) {
  fail('project-reference', `${PRODUCTION_FSPROJ} does not exist`)
} else {
  const text = read(PRODUCTION_FSPROJ)
  for (const forbidden of ['ProjectReference', 'wanxiangshu.fsproj', '../src']) {
    if (text.includes(forbidden)) {
      fail('project-reference', `${PRODUCTION_FSPROJ}: production project must not contain '${forbidden}'`)
    }
  }
}

for (const file of productionFiles) {
  if (file.endsWith('.fsproj') && norm(file) !== PRODUCTION_FSPROJ && read(file).includes('ProjectReference')) {
    fail('project-reference', `${file}: forbidden ProjectReference in production`)
  }
}

// No test-project reference gate. The test tree has no `.fsproj` to reference
// anything with (VERIFY-008), so that violation class is unrepresentable rather
// than unchecked. The general form still applies: `referencesLegacySrc` runs over
// `allFiles`, which includes every `.mjs` test, so a test importing `../src`
// still fails above.

// ── gate: fragment SSE events must not reach the business layer ─────────────

for (const file of productionFiles) {
  const text = read(file)

  for (const token of FORBIDDEN_SSE_TOKENS) {
    if (containsToken(text, token)) {
      fail(
        'sse-boundary',
        `${file}: forbidden fragment event token '${token}' (ARCH-002: use typed HostSignal)`,
      )
    }
  }

  if (containsToken(text, 'session.status') && !endsWithAny(file, SESSION_STATUS_ALLOWLIST)) {
    fail(
      'sse-boundary',
      `${file}: 'session.status' is permitted only in HostSignal transport files (HOST-003)`,
    )
  }
}

// ── gate: no sleeping ───────────────────────────────────────────────────────
// `*.gen.fs` files are generated DATA (e.g. the 120-rule Enforcer catalog, whose
// prose legitimately mentions "sleep-based-synchronization" as a rule name).
// They contain no executable logic, so the sleep-token and interop scans do not
// apply to them.

const isGeneratedFs = (path) => path.endsWith('.gen.fs')

for (const file of allFiles.filter(isFs)) {
  if (isGeneratedFs(file)) continue
  const text = read(file)
  for (const token of SLEEP_TOKENS) {
    if (containsToken(text, token)) {
      fail('no-sleep', `${file}: forbidden sleep token '${token}' (VERIFY-002: no fixed-delay waits)`)
    }
  }
}

// ── gate: mechanical filename suffixes ──────────────────────────────────────

for (const file of allFiles) {
  const leaf = basename(file, extname(file))
  const suffix = MECHANICAL_SUFFIXES.find((candidate) => leaf.endsWith(candidate))
  if (suffix && !MECHANICAL_ALLOWLIST.has(norm(file))) {
    fail('mechanical-suffix', `${file}: mechanical suffix '${suffix}'; add to allowlist with a reason`)
  }
}

// ── gate: Host/Fable interop confined to adapters and codecs ────────────────

for (const file of productionFiles.filter(isFs)) {
  if (isGeneratedFs(file)) continue
  const text = read(file)
  const hasInterop = HOST_INTEROP_MARKERS.some((marker) => text.includes(marker)) || DYNAMIC_ACCESS.test(text)
  if (!hasInterop) continue

  const path = norm(file)
  const explicitlyAllowed = HOST_INTEROP_ALLOWLIST.has(path)
  const isPureCore = PURE_CORE_DIRS.some((dir) => path.startsWith(dir))

  if (isPureCore && !explicitlyAllowed) {
    fail(
      'host-boundary',
      `${file}: pure core (Kernel/Domain) must not touch Host/Fable interop; an explicit reasoned allowlist entry is required (VERIFY-005 hard block)`,
    )
    continue
  }

  if (!explicitlyAllowed && !HOST_INTEROP_NAME.test(basename(file))) {
    fail('host-boundary', `${file}: raw Host/Fable dynamic access outside an adapter/codec (VERIFY-005)`)
  }
}

// ── gate: fsproj matches the tree ───────────────────────────────────────────
// F# compiles in declared order, so a file present on disk but absent from the
// project is silently dead, and a declared-but-missing file breaks the build.
// Both are invisible to every text gate above.

const declaredCompileItems = (fsprojPath, root) => {
  const text = read(fsprojPath)
  return [...text.matchAll(/Include="([^"]+\.fs)"/g)].map((match) => `${root}/${norm(match[1])}`)
}

// Only the production project. The test tree is `.mjs` with no project file, so
// `node:test` owns registration and there is no declaration to drift from — which
// is the point: `fsproj-drift` existed because five test files were dropped from
// the tests `.fsproj` at `c3c35756` and kept passing as dead code for months.
const fsprojDrift = [{ project: PRODUCTION_FSPROJ, root: PRODUCTION_ROOT, files: productionFiles }]

for (const { project, root, files } of fsprojDrift) {
  if (!existsSync(project)) continue

  const declared = new Set(declaredCompileItems(project, root))
  const onDisk = new Set(files.filter(isFs).map(norm))

  for (const path of declared) {
    if (!onDisk.has(path)) fail('fsproj-drift', `${project}: declares '${path}' which does not exist`)
  }
  for (const path of onDisk) {
    if (!declared.has(path)) fail('fsproj-drift', `${path}: on disk but not compiled by ${project} (dead file)`)
  }
}

// ── gate: single durable writer per fact ────────────────────────────────────

for (const { fact, allowed, reason } of SINGLE_WRITER_FACTS) {
  const constructor = new RegExp(`AgentFact\\.${escapeRegExp(fact)}\\b`)
  for (const file of productionFiles.filter(isFs)) {
    // Folds and type declarations name facts; only a constructor is a write.
    if (constructor.test(read(file)) && !endsWithAny(file, allowed)) {
      fail('single-writer', `${file}: constructs '${fact}' outside its writer boundary — ${reason}`)
    }
  }
}

// ── gate: bounded concurrency only (ARCH-009) ───────────────────────────────

{
  const { clause, owner, patterns } = UNBOUNDED_FANOUT
  const ownerText = existsSync(owner) ? read(owner) : null

  if (ownerText === null) {
    fail('bounded-concurrency', `${owner} is missing: ARCH-009's bounded primitive has no owner`)
  } else if (!ownerText.includes('mapBounded')) {
    // Without this the gate would still pass after the primitive was deleted:
    // every file would be clean because nobody fans out at all.
    fail('bounded-concurrency', `${owner}: must define the bounded primitive ARCH-009 mandates (mapBounded)`)
  }

  for (const file of productionFiles.filter(isFs)) {
    if (norm(file) === owner) continue
    const text = read(file)
    for (const pattern of patterns) {
      if (text.includes(pattern)) {
        fail(
          'bounded-concurrency',
          `${file}: unbounded fan-out '${pattern}'; ${clause} admits only Parallel.mapBounded outside ${owner}`,
        )
      }
    }
  }
}

// ── gate: DSL programs are real production entrypoints ──────────────────────

const guideText = existsSync(GUIDE_CONTRACT_PATH) ? read(GUIDE_CONTRACT_PATH) : null

if (guideText === null) {
  fail(
    'dsl-program',
    `${GUIDE_CONTRACT_PATH} is missing: work package T must designate its replacement before deleting it`,
  )
}

for (const { builder, file, names } of DSL_PROGRAMS) {
  const programPath = `${PRODUCTION_ROOT}/${file}`
  const programText = existsSync(programPath) ? read(programPath) : ''
  const programModule = basename(file, extname(file))

  const usesBuilder =
    programText.includes(`${builder} {`) ||
    programText.includes(`\`\`${builder}\`\` {`) ||
    programText.includes(`FlowBuilder<${builder}`)

  if (!usesBuilder) {
    fail('dsl-program', `${file}: does not use the '${builder} { ... }' builder`)
  }

  if (builder === 'process') {
    const hasCallsite = productionFiles.some(
      (candidate) =>
        norm(candidate).startsWith('src/Wanxiangshu.Next/Process/') &&
        (read(candidate).includes('process {') || read(candidate).includes('``process`` {')),
    )
    if (!hasCallsite) {
      fail('dsl-program', "no production 'process { ... }' callsite in next/Process")
    }
    if (guideText !== null && !(guideText.includes('ProcessRunner') || guideText.includes('process {'))) {
      fail('dsl-program', `${GUIDE_CONTRACT_PATH}: does not reference the production process program`)
    }
    continue
  }

  const referenced = productionFiles.some(
    (candidate) =>
      isFs(candidate) &&
      !norm(candidate).endsWith(file) &&
      names.some((name) => read(candidate).includes(`${programModule}.${name}`)),
  )
  if (!referenced) {
    fail('dsl-program', `${file}: orphan — no production entrypoint calls ${names.join('/')}`)
  }

  if (guideText !== null && !guideText.includes(programModule)) {
    fail('dsl-program', `${GUIDE_CONTRACT_PATH}: does not reference DSL program '${programModule}'`)
  }
}

// ── gate: dependency direction ──────────────────────────────────────────────

for (const file of allFiles.filter(isFs)) {
  if (!LOWER_LAYER_DIRS.some((dir) => norm(file).startsWith(dir))) continue
  const text = read(file)
  for (const upper of UPPER_LAYER_NAMESPACES) {
    if (text.includes(upper)) {
      fail('dependency-direction', `${file}: lower layer references upper layer '${upper}'`)
    }
  }
}

// ── gate: one owner per algorithm ───────────────────────────────────────────
//
// Two things are checked, and the second was missing: a symbol defined ONCE but
// in the wrong file is also a violation. The previous version guarded on
// `hits.length > 1`, so a lone definition in a non-owner file passed silently —
// exactly the state sha256Hex was in.
//
// Only MODULE-LEVEL definitions count. F# indents module members by four spaces
// and local bindings deeper, so a `let peerAgent =` inside a function body is a
// local variable that happens to share a name, not a second definition of the
// algorithm. Matching those would push authors toward contorted local names to
// appease a gate that misread the code.
//
// Modifiers must be admitted: `let private sha256Hex` is still a second
// definition of the same knowledge. Hiding it changes who may call it, not
// whether the knowledge exists twice.

const MODULE_LEVEL_INDENT = 4
const DEFINITION_MODIFIERS = String.raw`(?:private\s+|internal\s+|public\s+|inline\s+|rec\s+|mutable\s+)*`

const definesAtModuleLevel = (text, symbol) => {
  const pattern = new RegExp(
    String.raw`^( {0,${MODULE_LEVEL_INDENT}})(?:let|member)\s+${DEFINITION_MODIFIERS}${escapeRegExp(symbol)}\b`,
  )
  return text.split('\n').some((line) => pattern.test(line))
}

for (const { symbol, owners } of DUPLICATE_ALGORITHM_OWNERS) {
  const hits = allFiles.filter((file) => isFs(file) && definesAtModuleLevel(read(file), symbol)).map(norm)

  if (hits.length === 0) continue

  const strays = hits.filter((hit) => !owners.some((owner) => hit.endsWith(owner)))

  if (strays.length > 0) {
    fail(
      'duplicate-algorithm',
      `'${symbol}' defined outside its owner in ${strays.join(', ')}; owners: ${owners.join(', ')}`,
    )
  } else if (hits.length > owners.length) {
    fail(
      'duplicate-algorithm',
      `'${symbol}' defined in ${hits.length} places (${hits.join(', ')}); owners: ${owners.join(', ')}`,
    )
  }
}

// ── gate: one constructor per restricted type ───────────────────────────────

for (const { type, clause, owner, fields, builder } of SINGLE_CONSTRUCTOR_TYPES) {
  const assemblers = productionFiles.filter((file) => {
    if (!isFs(file) || norm(file) === owner) return false
    const text = read(file)
    return fields.every((field) => text.includes(field))
  })

  for (const assembler of assemblers) {
    fail(
      'single-constructor',
      `${assembler}: assembles ${type} field by field; ${clause} requires construction through ${owner}`,
    )
  }

  if (!builder) continue

  const callers = productionFiles.filter(
    (file) => isFs(file) && norm(file) !== owner && read(file).includes(builder),
  )

  if (callers.length === 0) {
    fail(
      'single-constructor',
      `${owner}: ${builder} has no call site; ${clause} means every provider request is built from it, ` +
        `so an unused constructor means every request is still assembled elsewhere`,
    )
  }
}

// ── gate: each test-runner tier enforces the bound that belongs to it ───────
//
// This criterion has now been wrong twice, in two different ways, and the second is the more
// instructive.
//
// First it was "the file contains a 3-to-5-digit number" — the best available test while the bound
// was a literal. Package W1 centralized every budget, so that check would have FAILED on the
// correct tree while passing on any file that happened to mention 1024: it matched the presence of
// digits, not the presence of a bound.
//
// Then it was "`tests-mjs/runner.mjs` names `PER_TEST_TIMEOUT_MS`". W4 split the runner in two and
// the per-test bound moved to the tier that can enforce it; the parent now enforces a
// verdict-silence window, which is the PRIMARY criterion VERIFY-004 demands. The gate failed it for
// not carrying a bound that is no longer its job — the criterion had quietly become a claim about
// file layout rather than about enforcement.
//
// So the criterion is per tier, and each names both its budget and the mechanism that applies it.
// One criterion spanning both files would be satisfiable by either alone, which is how a gate stops
// distinguishing a two-tier design from a one-tier design that merely mentions the right words.

for (const { path, budget, enforcement } of RUNNER_TIERS) {
  if (!existsSync(path)) {
    fail('test-runner', `${path}: missing; VERIFY-004's unit-runner gate needs both tiers`)
    continue
  }

  const source = read(path)

  if (!source.includes(budget)) {
    fail(
      'test-runner',
      `${path}: must consume ${budget} from testkit/opencode/time-budget.js ` +
        `(VERIFY-004: 兜底值必须集中定义)`,
    )
  }

  if (!enforcement.some((token) => source.includes(token))) {
    fail(
      'test-runner',
      `${path}: names ${budget} but applies nothing; expected one of ${enforcement.join(' / ')}. ` +
        `A budget with no enforcement is the shape VERIFY-004 calls 声明了但未接线`,
    )
  }
}

// ── gate: blogger vertical-slice anti-regression (SSOT/15 convergence) ──────
//
// Pins the Definition of Done items that static analysis can prove without Host
// canaries. Fail closed if production reintroduces deleted bypasses.

{
  const bloggerFiles = productionFiles.filter(
    (file) =>
      isFs(file) &&
      /Blogger|Companion|Enforcer|ParkedTransform|SpikePlugin/.test(norm(file)),
  )

  const productionCallers = (pattern) =>
    productionFiles.filter((file) => isFs(file) && pattern.test(read(file))).map(norm)

  // BloggerRuntime must have production call sites beyond its own module.
  const runtimeCallers = productionCallers(/BloggerRuntime\.(onMaterial|onCycleCommitted|onSquashCommitted|onFail)\b/).filter(
    (path) => !path.endsWith('src/Wanxiangshu.Next/Session/BloggerRuntimeState.fs'),
  )
  if (runtimeCallers.length === 0) {
    fail('blogger-convergence', 'BloggerRuntime transitions have no production call site')
  }

  // Single coordinator entry.
  const coordinatorCallers = productionCallers(/BloggerCoordinator\.onMainMaterial\b/)
  if (coordinatorCallers.length === 0) {
    fail('blogger-convergence', 'BloggerCoordinator.onMainMaterial has no production call site')
  }

  // Forbidden bypasses.
  for (const file of bloggerFiles) {
    const text = read(file)
    if (containsToken(text, 'BloggerNeedsReset')) {
      fail('blogger-convergence', `${file}: BloggerNeedsReset must stay deleted`)
    }
    if (/SubscribeTerminal/.test(text) && norm(file).endsWith('src/Wanxiangshu.Next/Session/CompanionHostBlogger.fs')) {
      fail('blogger-convergence', `${file}: Squash/Normal path must not SubscribeTerminal`)
    }
    if (/Extract the TOML from the raw messages/.test(text) || /"first"; toml/.test(text)) {
      fail('blogger-convergence', `${file}: raw user TOML extraction is forbidden`)
    }
    if (/\| EnforcementCycleCommitted\b/.test(text) && norm(file).endsWith('src/Wanxiangshu.Next/Kernel/Fact.fs')) {
      fail('blogger-convergence', `${file}: EnforcementCycleCommitted fact must stay deleted`)
    }
  }

  // Dual slots without dual storage:
  // PendingOffer = dictionary; CurrentRequest = InFlight payload (no currentRequest dict).
  const scopeText = existsSync('src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs')
    ? read('src/Wanxiangshu.Next/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs')
    : ''
  if (scopeText.includes('parkedOffer')) {
    fail('blogger-convergence', 'PluginRuntimeScope must not use a single parkedOffer dictionary')
  }
  if (!scopeText.includes('pendingOffer')) {
    fail('blogger-convergence', 'PluginRuntimeScope must hold a pendingOffer slot')
  }
  if (/let currentRequest\b/.test(scopeText) || /Dictionary<string, BloggerRequestContext>\(\)\s*\n\s*let pendingOffer/.test(scopeText)) {
    fail(
      'blogger-convergence',
      'PluginRuntimeScope must not dual-write CurrentRequest in a dictionary; InFlight payload is sole authority',
    )
  }
  if (!/TryPeekCurrentRequest[\s\S]{0,400}inFlightContext/.test(scopeText) && !/BloggerRuntime\.inFlightContext/.test(scopeText)) {
    fail('blogger-convergence', 'TryPeekCurrentRequest must read BloggerRuntime.inFlightContext')
  }

  // ADOPTED motion must not remain active PENDING.
  if (existsSync('PENDING/blogger-prompt-shape-and-parking.md')) {
    fail('blogger-convergence', 'ADOPTED motion still in PENDING/; archive it')
  }

  // offerToBlogger dual entry must stay gone.
  const offerSites = productionCallers(/offerToBlogger\b/)
  if (offerSites.length > 0) {
    fail('blogger-convergence', `offerToBlogger parallel entry remains: ${offerSites.join(', ')}`)
  }
}

// ── gate: the scanner itself sees the tree ──────────────────────────────────
// A silently empty scan would make every gate above vacuously pass.

const SCANNER_WITNESSES = [
  'src/Wanxiangshu.Next/Kernel/Flow.fs',
  'src/Wanxiangshu.Next/Journal/Writer.fs',
  'src/Wanxiangshu.Next/Infrastructure/OpenCode/Plugin/Plugin.fs',
  'src/Wanxiangshu.Next/Tools/StaticTools.fs',
]

// The test side needs its own witnesses now that it is scanned with a different
// extension list. `TEST_EXTENSIONS` drifting to `.fs`, or the tree moving, would
// otherwise yield an empty `testFiles` — and every gate over `allFiles` would
// silently stop covering tests while still reporting OK.
const TEST_SCANNER_WITNESSES = ['tests-mjs/runner.mjs', 'tests-mjs/domain.mjs', GUIDE_CONTRACT_PATH]

if (productionFiles.length < 10) {
  fail('scanner', `recursive scan returned only ${productionFiles.length} production files`)
}

for (const witness of SCANNER_WITNESSES) {
  if (!productionFiles.some((file) => norm(file) === witness)) {
    fail('scanner', `recursive scan missed ${witness}`)
  }
}

if (testFiles.length < 5) {
  fail('scanner', `recursive scan returned only ${testFiles.length} test files under ${TESTS_ROOT}`)
}

for (const witness of TEST_SCANNER_WITNESSES) {
  if (!testFiles.some((file) => norm(file) === witness)) {
    fail('scanner', `recursive scan missed ${witness}`)
  }
}

// ── report ──────────────────────────────────────────────────────────────────

const scanned = `${productionFiles.length} production + ${testFiles.length} test files`

if (violations.length === 0) {
  console.log(`architecture-gate: OK — ${scanned}`)
  process.exit(0)
}

const byGate = new Map()
for (const { gate, message } of violations) {
  if (!byGate.has(gate)) byGate.set(gate, [])
  byGate.get(gate).push(message)
}

console.error(`architecture-gate: ${violations.length} violation(s) — ${scanned}\n`)
for (const [gate, messages] of byGate) {
  console.error(`${gate} (${messages.length})`)
  for (const message of messages) console.error(`  ${message}`)
  console.error('')
}
process.exit(1)
