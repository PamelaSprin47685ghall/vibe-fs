#!/usr/bin/env node
// Static residue auditor. Replaces compiler feedback during the shock phase:
// it cannot prove correctness, only that no legacy entrypoint survives.
//
//   node scripts/shock-audit.mjs          report residue
//   node scripts/shock-audit.mjs --gate   fail when residue exceeds target

import { walk, countLiteral, readLines } from './repo-scan.mjs'

const SCOPES = {
  next: { root: 'next', extensions: ['.fs'] },
  // VERIFY-008: layers 1-3 are `.mjs`. Left pointing at the deleted `tests-next`
  // this scope would return 0 for every symbol, which reads as "extinct" — the
  // most dangerous possible failure for an extinction audit.
  tests: { root: 'tests-mjs', extensions: ['.mjs'] },
  testkit: { root: 'testkit', extensions: ['.mjs', '.js'] },
  scripts: { root: 'testkit/opencode/scripts', extensions: ['.json', '.toml'] },
}

// target: allowed residue per scope. Absent scope means 0.
//
// scopedTo: count only inside these path fragments. Some symbols are legitimate
// where SSOT sanctions them and forbidden elsewhere — a whole-repo count would
// then be a gate that can never reach zero, which eventually forces a wrong
// deletion. The scope IS the violation.

// The files where an extinct fact NAME must survive as a string literal.
//
// PERSIST-004/005 require a pre-0.5.0 journal to stop startup with a precise
// diagnosis, and the only way to recognise one is by the case names it contains.
// Counting those literals as residue makes every migrated fact permanently
// non-zero, so the gate would eventually force deleting the very check that
// tells an operator to archive the old file.
//
// Two entries, for the same reason on both sides of the boundary: the codec holds
// the rejection list, and the layer 1 test asserts each name still produces the
// migration message rather than an opaque union error. Exempting only the codec
// would make writing that test impossible — the assertion IS the literal.
//
// The tradeoff is that a genuine violation inside these two files goes unseen
// here. Acceptable because each holds nothing but the codec and its rejection
// list, or the test over it; the architecture and ssot gates still read both.
const LEGACY_NAME_SENTINELS = ['next/Journal/FactCodec.fs', 'tests-mjs/Journal/envelope.test.mjs']

const EXTINCTION = [
  { symbol: 'PostPromptFireAndForget', clause: 'PROMPT-007' },
  { symbol: 'prompt_async', clause: 'PROMPT-005', target: { next: 1, testkit: Infinity } },
  { symbol: 'PluginPromptAccepted', clause: 'PROMPT-005' },
  { symbol: 'recordDurableAdvance', clause: 'FALLBACK-003' },
  { symbol: 'ProviderFailureContinuation', clause: 'FALLBACK-003' },
  { symbol: 'ProviderFailureWakeup', clause: 'FALLBACK-003' },
  { symbol: 'ConfirmationPhysicalMessageId', clause: 'REVIEW-010' },
  { symbol: 'samePhysicalRootReevaluation', clause: 'REVIEW-003' },

  // AcceptedContinuationIds is legitimate for PROMPT-003/PROMPT-009 (was this
  // message a continuation, and of what kind). REVIEW-003 forbids it only as
  // review-confirmation evidence, so the violation is its presence in the
  // review path — not the symbol.
  {
    symbol: 'AcceptedContinuationIds',
    clause: 'REVIEW-003',
    scopedTo: ['Journal/ReviewConfirmation', 'Journal/ReviewProjection', 'Review/', 'Session/ReviewerHost'],
  },
  // AcceptedContinuationRoots existed only to let a witness infer confirmation
  // from a shared authority root. No sanctioned use remains.
  { symbol: 'AcceptedContinuationRoots', clause: 'REVIEW-003' },

  { symbol: 'RecentProviderRunIds', clause: 'REVIEW-004' },
  { symbol: 'ReviewConfirmedIdle', clause: 'REVIEW-006' },
  { symbol: 'GuardPromptAccepted', clause: 'PROMPT-005' },
  { symbol: 'InteractionRepairClaimed', clause: 'PROMPT-005' },
  { symbol: 'HumanPromptAccepted', clause: 'PROMPT-004' },
  { symbol: 'shouldCreateCompanion', clause: 'COMPANION-002' },
  { symbol: 'ProjectionSnapshot', clause: 'COMPANION-005' },
  { symbol: 'CompanionDelta', clause: 'CTX-013' },
  { symbol: 'jsonDelta', clause: 'CTX-013' },
  { symbol: 'userMessageID', clause: 'HOST-011' },
  { symbol: 'AgentLinked', clause: 'EXEC-009' },
  { symbol: 'AgentForked', clause: 'EXEC-009' },
  { symbol: 'AgentUnlinked', clause: 'EXEC-009' },
  { symbol: 'OrchestratorCandidateRegistered', clause: 'ORCH-006' },
  { symbol: 'OrchestratorRebased', clause: 'ORCH-006' },
  { symbol: 'OrchestratorRejected', clause: 'ORCH-006' },
  { symbol: 'OrchestratorPreRebaseReviewConfirmed', clause: 'ORCH-006' },
  { symbol: 'OrchestratorPostRebaseReviewConfirmed', clause: 'ORCH-006' },

  // Script forest (work package K). See STATUS/design-script-forest.md.
  // Content must be a pure function of (lane, turn, step); these are the
  // mechanisms that made it stateful, ambiguous, or identity-guessing.
  { symbol: 'specificity', clause: 'VERIFY-003' },
  { symbol: 'pathCursor', clause: 'VERIFY-003' },
  { symbol: 'sealToEdgeId', clause: 'VERIFY-003' },
  { symbol: 'templateFingerprint', clause: 'VERIFY-003' },
  { symbol: 'aliasToEdge', clause: 'REVIEW-003' },
  { symbol: 'claimCount', clause: 'VERIFY-003' },
  { symbol: 'requestRoleOf', clause: 'PROMPT-008' },
  { symbol: 'NUDGE_MARKERS', clause: 'VERIFY-003' },
  { symbol: '__testkitHeaders', clause: 'VERIFY-003' },
  { symbol: 'epochCold', clause: 'COMPANION-009' },
  { symbol: 'modelSideCold', clause: 'FALLBACK-004' },
  { symbol: 'loadScripts', clause: 'VERIFY-003' },
  { symbol: '"reusable"', clause: 'VERIFY-003' },
  { symbol: '"pathless"', clause: 'VERIFY-003' },
  { symbol: '"turn"', clause: 'VERIFY-003' },
]

// Facts whose production append site must be unique.
//
// Two independent signals are needed, and neither alone is sufficient:
//
//   constructor sites — `AgentFact.<Name>` applied in a module that is not a
//                       declaration/codec/fold file. Counting bare mentions
//                       instead would score doc comments as writers, which is
//                       how this check first reported "ok (1)" for a fact that
//                       had no append site at all.
//   append helpers    — a function that performs the append hides its callers
//                       behind one constructor site, which is exactly how
//                       FALLBACK-003 is violated today.
const SINGLE_WRITER = [
  {
    fact: 'FallbackCursorAdvanced',
    clause: 'FALLBACK-003',
    declarationFiles: [
      'next/Kernel/Fact.fs',
      'next/Journal/FactCodec.fs',
      'next/Journal/Fold.fs',
      'next/Journal/AgentJournal.fs',
      'next/Journal/FallbackProjection.fs',
    ],
    appendHelpers: ['recordFallbackFailure'],
  },
  {
    fact: 'FallbackExhausted',
    clause: 'FALLBACK-005',
    declarationFiles: [
      'next/Kernel/Fact.fs',
      'next/Journal/FactCodec.fs',
      'next/Journal/Fold.fs',
      'next/Journal/AgentJournal.fs',
      'next/Journal/FallbackProjection.fs',
    ],
    appendHelpers: [],
  },
  {
    fact: 'ConfirmedReviewWitness',
    clause: 'REVIEW-006',
    declarationFiles: [
      'next/Kernel/Fact.fs',
      'next/Journal/FactCodec.fs',
      'next/Journal/Fold.fs',
      'next/Journal/AgentJournal.fs',
      'next/Journal/ReviewProjection.fs',
    ],
    appendHelpers: [],
  },
  {
    fact: 'ProviderInputSealed',
    clause: 'REVIEW-010',
    declarationFiles: [
      'next/Kernel/Fact.fs',
      'next/Journal/FactCodec.fs',
      'next/Journal/Fold.fs',
      'next/Journal/AgentJournal.fs',
      'next/Journal/ReviewProjection.fs',
    ],
    appendHelpers: [],
  },
  {
    fact: 'PluginPromptClaimed',
    clause: 'PROMPT-005',
    declarationFiles: [
      'next/Kernel/Fact.fs',
      'next/Journal/FactCodec.fs',
      'next/Journal/Fold.fs',
      'next/Journal/AgentJournal.fs',
      'next/Journal/PromptAuthorityLedger.fs',
    ],
    appendHelpers: [],
  },
  {
    fact: 'PluginPromptPhysicalAccepted',
    clause: 'PROMPT-005',
    declarationFiles: [
      'next/Kernel/Fact.fs',
      'next/Journal/FactCodec.fs',
      'next/Journal/Fold.fs',
      'next/Journal/AgentJournal.fs',
      'next/Journal/PromptAuthorityLedger.fs',
    ],
    appendHelpers: [],
  },
  {
    fact: 'HandleRetired',
    clause: 'EXEC-009',
    declarationFiles: [
      'next/Kernel/Fact.fs',
      'next/Journal/FactCodec.fs',
      'next/Journal/Fold.fs',
      'next/Journal/AgentJournal.fs',
      'next/Journal/LinkageProjection.fs',
    ],
    appendHelpers: [],
  },
  {
    fact: 'PublishClaimed',
    clause: 'ORCH-005',
    declarationFiles: [
      'next/Kernel/Fact.fs',
      'next/Journal/FactCodec.fs',
      'next/Journal/Fold.fs',
      'next/Journal/AgentJournal.fs',
      'next/Journal/OrchestratorProjection.fs',
    ],
    appendHelpers: [],
  },
]

const MARKER = 'SHOCK-UNMIGRATED'
const CLAUSE_RE = /SHOCK-UNMIGRATED\[([A-Z]+-\d{3})\]/

const gate = process.argv.includes('--gate')
const files = Object.fromEntries(
  Object.entries(SCOPES).map(([name, { root, extensions }]) => [name, walk(root, extensions)]),
)

const scopeNames = Object.keys(SCOPES)
const failures = []

const targetFor = (entry, scope) => entry.target?.[scope] ?? 0
const pad = (value, width) => String(value).padEnd(width)

/** Restrict a scope's file list to the paths where the symbol is a violation. */
const filesFor = (entry, scope) => {
  const all = files[scope].filter((file) => !LEGACY_NAME_SENTINELS.includes(file.replace(/\\/g, '/')))
  if (!entry.scopedTo) return all
  return all.filter((file) => entry.scopedTo.some((fragment) => file.replace(/\\/g, '/').includes(fragment)))
}

console.log('shock-audit: legacy symbol residue\n')
console.log(`${pad('SYMBOL', 40)}${pad('CLAUSE', 14)}${scopeNames.map((s) => pad(s, 9)).join('')}`)

for (const entry of EXTINCTION) {
  const counts = scopeNames.map((scope) => countLiteral(filesFor(entry, scope), entry.symbol).length)
  const cells = counts.map((count, index) => {
    const target = targetFor(entry, scopeNames[index])
    const flag = count > target ? '!' : ' '
    return pad(`${count}${flag}`, 9)
  })
  const label = entry.scopedTo ? `${entry.symbol} (scoped)` : entry.symbol
  console.log(`${pad(label, 40)}${pad(entry.clause, 14)}${cells.join('')}`)

  counts.forEach((count, index) => {
    const scope = scopeNames[index]
    const target = targetFor(entry, scope)
    if (count > target) {
      const where = entry.scopedTo ? ` within ${entry.scopedTo.join(', ')}` : ''
      failures.push(
        `${entry.clause} ${entry.symbol}: ${scope} residue ${count} > target ${target}${where}`,
      )
    }
  })
}

console.log('\nshock-audit: single production writer\n')

const endsWithAny = (file, suffixes) => {
  const normalized = file.replace(/\\/g, '/')
  return suffixes.some((suffix) => normalized.endsWith(suffix))
}

for (const entry of SINGLE_WRITER) {
  const { fact, clause, declarationFiles, appendHelpers } = entry

  // A write is a CONSTRUCTOR APPLICATION, not a mention. Counting mentions
  // scored a doc comment as a writer and reported "ok (1)" for a fact with no
  // append site whatsoever — a gate that answers "fine" for both zero and one
  // writer cannot detect the transition it exists to protect.
  const constructorPattern = `AgentFact.${fact}`

  const declared = files.next.some(
    (file) => endsWithAny(file, declarationFiles) && countLiteral([file], fact).length > 0,
  )

  const directWriters = [
    ...new Set(countLiteral(files.next, constructorPattern).map((hit) => hit.file)),
  ].filter((file) => !endsWithAny(file, declarationFiles))

  const indirectWriters = new Set()
  for (const helper of appendHelpers) {
    const hits = countLiteral(files.next, helper)
    const definingFiles = new Set(hits.filter((hit) => /^let\s/.test(hit.text)).map((hit) => hit.file))
    for (const hit of hits) {
      if (!definingFiles.has(hit.file)) indirectWriters.add(`${hit.file}:${hit.line} (via ${helper})`)
    }
  }

  const writers = [...directWriters, ...indirectWriters]

  // Zero writers is reported separately from one. During the shock phase a
  // declared-but-unwritten fact is expected; at --gate time it means the
  // migration declared a type and never wired it.
  let verdict
  if (!declared) verdict = 'absent (not declared)'
  else if (writers.length === 0) verdict = 'declared, no writer yet'
  else if (writers.length === 1) verdict = 'ok (1)'
  else verdict = `${writers.length} writers`

  console.log(`${pad(fact, 40)}${pad(clause, 14)}${verdict}`)
  for (const writer of writers) console.log(`${' '.repeat(54)}${writer}`)

  if (!declared) {
    failures.push(`${clause} ${fact}: fact is not declared in ${declarationFiles[0]}`)
  } else if (writers.length === 0) {
    failures.push(`${clause} ${fact}: declared but no production writer constructs it`)
  } else if (writers.length > 1) {
    failures.push(`${clause} ${fact}: ${writers.length} production writers, expected 1`)
  }
}

const markers = []
for (const scope of scopeNames) {
  for (const file of files[scope]) {
    readLines(file).forEach((text, index) => {
      if (!text.includes(MARKER)) return
      const match = CLAUSE_RE.exec(text)
      markers.push({ file, line: index + 1, clause: match?.[1] })
    })
  }
}

console.log(`\nshock-audit: ${MARKER} markers: ${markers.length}`)
for (const marker of markers) {
  const label = marker.clause ?? 'MISSING-CLAUSE-ID'
  console.log(`  ${marker.file}:${marker.line}  ${label}`)
  if (!marker.clause) {
    failures.push(`${marker.file}:${marker.line}: ${MARKER} without an SSOT clause id`)
  }
}
if (gate && markers.length > 0) {
  failures.push(`${markers.length} ${MARKER} markers must be zero before the first compile`)
}

if (!gate) {
  console.log('\nreport only (pass --gate to enforce)')
  process.exit(0)
}

if (failures.length === 0) {
  console.log('\nshock-audit: gate OK')
  process.exit(0)
}

console.error(`\nshock-audit: ${failures.length} gate failures`)
for (const failure of failures) console.error(`  ${failure}`)
process.exit(1)
