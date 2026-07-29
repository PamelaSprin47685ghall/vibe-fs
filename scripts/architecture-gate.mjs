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

const PRODUCTION_ROOT = 'next'
const TESTS_ROOT = 'tests-next'
const SOURCE_EXTENSIONS = ['.fs', '.fsproj']

// ── forbidden vocabulary ────────────────────────────────────────────────────

// Program-counter and framework-ceremony names (ARCH-001, ARCH-008).
const FORBIDDEN_TOKENS = [
  'idleProposals',
  'callOnce',
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
  'next/OpenCode/HostEventCodec.fs',
  'next/OpenCode/HostSignalAdapter.fs',
  'next/OpenCode/RetrySignalHandler.fs',
  'next/OpenCode/HostSignalSubscribe.fs',
]

const SLEEP_TOKENS = ['sleepJs', 'sleep']

// ── file naming ─────────────────────────────────────────────────────────────

const MECHANICAL_SUFFIXES = ['Helpers', 'Primitives', 'Fields', 'Emit', 'Service', 'Core']

const MECHANICAL_ALLOWLIST = new Map([
  ['next/Session/AgentRoleIdentity.fs', 'semantic boundary pending deeper split'],
  ['next/OpenCode/PluginHostInterop.fs', 'semantic boundary pending deeper split'],
  ['next/OpenCode/TerminalPolicy.fs', 'semantic boundary pending deeper split'],
])

// ── Host boundary ───────────────────────────────────────────────────────────

const HOST_INTEROP_MARKERS = ['Fable.Core.JsInterop', 'jsNative', 'createObj', 'unbox']
const DYNAMIC_ACCESS = /[\w)]\?[a-zA-Z]/
const HOST_INTEROP_NAME =
  /(Host|Port|Codec|Adapter|Boot|Runtime|Writer|Node|Plugin|Supervisor|Backend|Projection|Transform|Signal|Json|Git|Flow|Pty|Tool|Subscribe|Canonical|Process)/i

const HOST_INTEROP_ALLOWLIST = new Map([
  ['next/Session/CompanionDelta.fs', 'companion canonical hash and projection delta'],
  ['next/Orchestrator.IntegrationGate.fs', 'external lockfile host adapter'],
  ['next/Orchestrator.WorktreeResource.fs', 'external worktree/ValueTask adapter'],
  ['next/Tools/PromptAssets.fs', 'prompt asset construction at the Host boundary'],
  ['next/OpenCode/ManagerConfig.fs', 'Host configuration adapter'],
  ['next/OpenCode/ManagedAgentConfig.fs', 'Host-final opencode.json adapter'],
  ['next/OpenCode/ExecutorSummarize.fs', 'Executor summarization Host adapter'],
])

// ── single writer ───────────────────────────────────────────────────────────

const SINGLE_WRITER_FACTS = [
  {
    fact: 'FallbackCursorAdvanced',
    allowed: ['OpenCode/FallbackDetect.fs', 'Journal/AgentJournal.fs', 'Journal/Fold.fs', 'Kernel/Fact.fs'],
    reason: 'only the FallbackController may build the durable fallback cursor fact (FALLBACK-003)',
  },
  {
    fact: 'ReviewConfirmedIdle',
    allowed: [
      'Journal/AgentJournal.fs',
      'OpenCode/TurnCompletionProgram.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'only TurnCompletionProgram may record a confirmed reviewer idle (REVIEW-006)',
  },
  {
    fact: 'PluginPromptClaimed',
    allowed: [
      'OpenCode/PromptDispatcherSend.fs',
      'OpenCode/PromptDispatcher.fs',
      'Journal/PromptAuthorityLedger.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'only PromptDispatcher may claim a plugin prompt (PROMPT-005)',
  },
  {
    fact: 'PluginPromptAccepted',
    allowed: [
      'OpenCode/PromptDispatcher.fs',
      'Journal/PromptAuthorityLedger.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'only PromptDispatcher may accept a plugin prompt (PROMPT-005)',
  },
  {
    fact: 'PluginPromptAbandoned',
    allowed: [
      'OpenCode/PromptDispatcherSend.fs',
      'OpenCode/PromptDispatcher.fs',
      'Journal/PromptAuthorityLedger.fs',
      'Journal/Fold.fs',
      'Kernel/Fact.fs',
    ],
    reason: 'only PromptDispatcher may abandon a plugin prompt (PROMPT-005)',
  },
]

// ── DSL programs ────────────────────────────────────────────────────────────

const DSL_PROGRAMS = [
  { builder: 'agent', file: 'Agent/AgentProgram.fs', names: ['forkAgent', 'validateSession', 'runAgentFlow'] },
  {
    builder: 'companion',
    file: 'Session/CompanionProgram.fs',
    names: ['buildDelta', 'shouldReplacePrefix', 'runCompanionFlow'],
  },
  { builder: 'review', file: 'Review/ReviewProgram.fs', names: ['recordVerdict', 'confirmPerfect', 'runReviewFlow'] },
  { builder: 'orchestrator', file: 'Orchestrator/OrchestratorProgram.fs', names: ['run'] },
  { builder: 'process', file: 'Process/ProcessRunner.fs', names: ['run', 'runWithHost'] },
]

const GUIDE_CONTRACT_PATH = 'tests-next/GuideContract/Signatures.fs'

// ── layering ────────────────────────────────────────────────────────────────

const LOWER_LAYER_DIRS = ['next/Kernel/', 'next/Domain/']

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
  { symbol: 'sha256Hex', owners: ['Domain/PromptAuthority.fs'] },
  { symbol: 'reviewWitness', owners: ['Domain/ReviewWitness.fs'] },
  { symbol: 'confirmPerfect', owners: ['Review/ReviewProgram.fs'] },
]

// The active test runner. VERIFY-008 replaces the Fable runner with a plain
// mjs one; whichever exists must still enforce a hard per-test timeout.
const RUNNER_CANDIDATES = ['tests-mjs/runner.mjs', 'tests-next/runner.js']

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

const referencesLegacySrc = (text) =>
  text.includes('../src') ||
  text.includes('..\\src') ||
  text.includes('/src/') ||
  text.includes('\\src\\') ||
  (text.includes('open Wanxiangshu.') && !text.includes('open Wanxiangshu.Next'))

for (const root of [PRODUCTION_ROOT, TESTS_ROOT]) {
  if (!existsSync(root)) {
    console.error(`architecture-gate: required directory '${root}' does not exist`)
    process.exit(1)
  }
}

const productionFiles = walk(PRODUCTION_ROOT, SOURCE_EXTENSIONS)
const testFiles = walk(TESTS_ROOT, SOURCE_EXTENSIONS)
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

const PRODUCTION_FSPROJ = 'next/Wanxiangshu.Next.fsproj'

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

for (const file of testFiles.filter((path) => path.endsWith('.fsproj'))) {
  const text = read(file)
  if (!text.includes('ProjectReference')) continue

  const referencesProduction =
    text.includes('../next/Wanxiangshu.Next.fsproj') || text.includes('..\\next\\Wanxiangshu.Next.fsproj')
  const referencesLegacy = text.includes('wanxiangshu.fsproj') && !text.includes('Wanxiangshu.Next.fsproj')
  const referencesSrc = text.includes('../src') || text.includes('..\\src') || text.includes('\\src')

  if (referencesLegacy || referencesSrc || !referencesProduction) {
    fail('project-reference', `${file}: test project may only reference next/Wanxiangshu.Next.fsproj`)
  }
}

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

for (const file of allFiles.filter(isFs)) {
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
  const text = read(file)
  const hasInterop = HOST_INTEROP_MARKERS.some((marker) => text.includes(marker)) || DYNAMIC_ACCESS.test(text)
  if (!hasInterop) continue

  const allowed = HOST_INTEROP_ALLOWLIST.has(norm(file)) || HOST_INTEROP_NAME.test(basename(file))
  if (!allowed) {
    fail(
      'host-boundary',
      `${file}: raw Host/Fable dynamic access outside an adapter/codec (VERIFY-005)`,
    )
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
        norm(candidate).startsWith('next/Process/') &&
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

for (const { symbol, owners } of DUPLICATE_ALGORITHM_OWNERS) {
  const definition = new RegExp(`\\b(let|member|let\\s+rec)\\s+${escapeRegExp(symbol)}\\b`)
  const hits = allFiles.filter((file) => isFs(file) && definition.test(read(file))).map(norm)

  if (hits.length > 1 && !hits.every((hit) => owners.some((owner) => hit.endsWith(owner)))) {
    fail(
      'duplicate-algorithm',
      `'${symbol}' defined in ${hits.length} places (${hits.join(', ')}); owners: ${owners.join(', ')}`,
    )
  }
}

// ── gate: the test runner enforces a hard per-test timeout ──────────────────

const runnerPath = RUNNER_CANDIDATES.find((candidate) => existsSync(candidate))

if (!runnerPath) {
  fail('test-runner', `no test runner found; expected one of ${RUNNER_CANDIDATES.join(', ')}`)
} else {
  const runner = read(runnerPath)
  if (!/\b\d{3,5}\b/.test(runner)) {
    fail('test-runner', `${runnerPath}: must declare an explicit per-test timeout in milliseconds`)
  }
  if (!runner.includes('Promise.race') && !runner.includes('AbortSignal.timeout')) {
    fail('test-runner', `${runnerPath}: must enforce the timeout in-process (Promise.race or AbortSignal.timeout)`)
  }
}

// ── gate: the scanner itself sees the tree ──────────────────────────────────
// A silently empty scan would make every gate above vacuously pass.

const SCANNER_WITNESSES = [
  'next/Kernel/Flow.fs',
  'next/Journal/Writer.fs',
  'next/OpenCode/Plugin.fs',
  'next/Tools/StaticTools.fs',
]

if (productionFiles.length < 10) {
  fail('scanner', `recursive scan returned only ${productionFiles.length} production files`)
}

for (const witness of SCANNER_WITNESSES) {
  if (!productionFiles.some((file) => norm(file) === witness)) {
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
