#!/usr/bin/env node
// Static residue auditor. Replaces compiler feedback during the shock phase:
// it cannot prove correctness, only that no legacy entrypoint survives.
//
//   node scripts/shock-audit.mjs          report residue
//   node scripts/shock-audit.mjs --gate   fail when residue exceeds target

import { walk, countLiteral, readLines } from './repo-scan.mjs'

const SCOPES = {
  next: { root: 'next', extensions: ['.fs'] },
  tests: { root: 'tests-next', extensions: ['.fs'] },
  testkit: { root: 'testkit', extensions: ['.mjs', '.js'] },
  scripts: { root: 'testkit/opencode/scripts', extensions: ['.json', '.toml'] },
}

// target: allowed residue per scope. Absent scope means 0.
const EXTINCTION = [
  { symbol: 'PostPromptFireAndForget', clause: 'PROMPT-007' },
  { symbol: 'prompt_async', clause: 'PROMPT-005', target: { next: 1, testkit: Infinity } },
  { symbol: 'PluginPromptAccepted', clause: 'PROMPT-005' },
  { symbol: 'recordDurableAdvance', clause: 'FALLBACK-003' },
  { symbol: 'ProviderFailureContinuation', clause: 'FALLBACK-003' },
  { symbol: 'ProviderFailureWakeup', clause: 'FALLBACK-003' },
  { symbol: 'ConfirmationPhysicalMessageId', clause: 'REVIEW-010' },
  { symbol: 'samePhysicalRootReevaluation', clause: 'REVIEW-003' },
  { symbol: 'AcceptedContinuationIds', clause: 'REVIEW-003' },
  { symbol: 'AcceptedContinuationRoots', clause: 'REVIEW-003' },
  { symbol: 'RecentProviderRunIds', clause: 'REVIEW-004' },
  { symbol: 'ReviewConfirmedIdle', clause: 'REVIEW-006' },
  { symbol: 'GuardPromptAccepted', clause: 'PROMPT-005' },
  { symbol: 'InteractionRepairClaimed', clause: 'PROMPT-005' },
  { symbol: 'HumanPromptAccepted', clause: 'PROMPT-004' },
  { symbol: 'shouldCreateCompanion', clause: 'COMPANION-002' },
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
// Two independent checks are needed. Counting the fact name alone is not
// enough: an append helper hides the real writer count behind one call, which
// is exactly how FALLBACK-003 is currently violated.
//
//   declarationFiles — type/codec/fold/projection files that name the fact
//                      without appending it
//   appendHelpers    — functions that perform the append; their call sites
//                      outside the defining module are the real writers
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
    // Kernel/Outcome.fs carries an unrelated terminal-outcome case of the same
    // name. Until the journal fact exists this row reports `absent`.
    unrelatedNames: ['next/Kernel/Outcome.fs'],
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

console.log('shock-audit: legacy symbol residue\n')
console.log(`${pad('SYMBOL', 40)}${pad('CLAUSE', 14)}${scopeNames.map((s) => pad(s, 9)).join('')}`)

for (const entry of EXTINCTION) {
  const counts = scopeNames.map((scope) => countLiteral(files[scope], entry.symbol).length)
  const cells = counts.map((count, index) => {
    const target = targetFor(entry, scopeNames[index])
    const flag = count > target ? '!' : ' '
    return pad(`${count}${flag}`, 9)
  })
  console.log(`${pad(entry.symbol, 40)}${pad(entry.clause, 14)}${cells.join('')}`)

  counts.forEach((count, index) => {
    const scope = scopeNames[index]
    const target = targetFor(entry, scope)
    if (count > target) {
      failures.push(`${entry.clause} ${entry.symbol}: ${scope} residue ${count} > target ${target}`)
    }
  })
}

console.log('\nshock-audit: single production writer\n')

const endsWithAny = (file, suffixes) => {
  const normalized = file.replace(/\\/g, '/')
  return suffixes.some((suffix) => normalized.endsWith(suffix))
}

for (const entry of SINGLE_WRITER) {
  const { fact, clause, declarationFiles, appendHelpers, unrelatedNames = [] } = entry

  const declared = files.next.some(
    (file) => endsWithAny(file, declarationFiles) && countLiteral([file], fact).length > 0,
  )

  if (!declared) {
    console.log(`${pad(fact, 40)}${pad(clause, 14)}absent (fact not defined yet)`)
    for (const file of unrelatedNames) {
      console.log(`${' '.repeat(54)}unrelated same-name symbol in ${file}`)
    }
    continue
  }

  // Direct append sites: the fact is named in a module that is neither a
  // declaration file nor a known unrelated-name carrier.
  const directWriters = [...new Set(countLiteral(files.next, fact).map((hit) => hit.file))].filter(
    (file) => !endsWithAny(file, [...declarationFiles, ...unrelatedNames]),
  )

  // Indirect append sites: callers of an append helper, excluding the module
  // that defines the helper.
  const indirectWriters = new Set()
  for (const helper of appendHelpers) {
    const hits = countLiteral(files.next, helper)
    const definingFiles = new Set(
      hits.filter((hit) => /^\s*let\s+/.test(hit.text.replace(/^\s*/, (m) => m))).map((hit) => hit.file),
    )
    for (const hit of hits) {
      if (!definingFiles.has(hit.file)) indirectWriters.add(`${hit.file}:${hit.line} (via ${helper})`)
    }
  }

  const writers = [...directWriters, ...indirectWriters]
  const verdict = writers.length <= 1 ? `ok (${writers.length})` : `${writers.length} writers`
  console.log(`${pad(fact, 40)}${pad(clause, 14)}${verdict}`)
  for (const writer of writers) console.log(`${' '.repeat(54)}${writer}`)
  if (writers.length > 1) {
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
