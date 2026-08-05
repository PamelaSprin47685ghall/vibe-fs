#!/usr/bin/env node
// DSL ownership gate (VERIFY-001 layer 0).
// Checks that Program/Domain files do not contain forbidden control-flow escape hatches.
//
// Modes:
//   node scripts/checks/dsl-ownership.mjs                  fail-closed on any violation
//   node scripts/checks/dsl-ownership.mjs --threshold=N    fail-closed on violations > N
//
// CI calls with --threshold to freeze the current backlog while preventing new violations.

import { readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const PRODUCTION_ROOT = 'src/Wanxiangshu'
export const PROGRAM_DIRS = [
  `${PRODUCTION_ROOT}/Agent/`,
  `${PRODUCTION_ROOT}/Application/`,
  `${PRODUCTION_ROOT}/Domain/`,
  `${PRODUCTION_ROOT}/Kernel/`,
  `${PRODUCTION_ROOT}/Session/`,
]

export const FORBIDDEN = [
  { gate: 'raw-task', pattern: /(?<!\/\/\s*)\btask\s*\{/, label: 'raw task { }' },
  { gate: 'mutable', pattern: /(?<!\/\/\s*)\blet mutable\b/, label: 'let mutable' },
  { gate: 'flow-lift', pattern: /\bFlow\.(?:lift|create)\b/, label: 'Flow.lift / Flow.create' },
  {
    gate: 'infrastructure-leak',
    pattern:
      /\b(?:open Wanxiangshu\.Infrastructure|open Wanxiangshu\.OpenCode|open Wanxiangshu\.Process)\b/,
    label: 'infrastructure namespace open',
  },
  {
    gate: 'program-counter',
    pattern:
      /\b(?:Dirty|Running|RepairSpent|ReactivatedAfterSeal|injectRepair|commitUnknown|abandonThenCatchUp|forceConfirmedReviewer|isContinuation|publishToMailbox|openReviewBarrier)\b/,
    label: 'program counter field/parameter',
  },
  {
    gate: 'behaviour-bool',
    pattern:
      /\b[a-zA-Z]+(?:Stage|Phase|Next|Running|Pending|Spent|Already|Should)\b|\b(HasPendingCompletion|LastCompletionStatus|bloggerTask|bloggerFailed)\b/,
    label: 'behaviour bool or stage field',
  },
]

export const GATE_NAMES = FORBIDDEN.map((item) => item.gate)

const norm = (p) => p.replace(/\\/g, '/')

export const isProgramFile = (path) => {
  const rel = norm(relative('.', path))
  return PROGRAM_DIRS.some((dir) => rel.startsWith(dir)) && rel.endsWith('.fs')
}

/** Scan one source text. Returns [{gate, file, line, text}, ...]. */
export const scanText = (text, file = '<synthetic>') => {
  const violations = []
  const lines = text.split('\n')
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const code = line.replace(/\/\/.*/g, '').trim()
    if (!code) continue

    for (const { gate, pattern } of FORBIDDEN) {
      if (pattern.exec(code)) {
        violations.push({ gate, file, line: i + 1, text: line.trim() })
      }
    }
  }
  return violations
}

/** Scan {file, text} entries. */
export const scanFiles = (entries) => {
  const violations = []
  for (const entry of entries) {
    const file = entry.file
    const text = entry.text
    for (const v of scanText(text, file)) violations.push(v)
  }
  return violations
}

export const groupByGate = (violations) => {
  const byGate = new Map()
  for (const v of violations) {
    if (!byGate.has(v.gate)) byGate.set(v.gate, [])
    byGate.get(v.gate).push(v)
  }
  return byGate
}

/** Exit decision: { ok, reason }. threshold < 0 means zero-tolerance. */
export const evaluateThreshold = (violationCount, threshold) => {
  if (violationCount === 0) return { ok: true, reason: 'clean' }
  if (threshold >= 0 && violationCount <= threshold) {
    return { ok: true, reason: 'within-threshold' }
  }
  if (threshold >= 0) return { ok: false, reason: 'exceeds-threshold' }
  return { ok: false, reason: 'fail-closed' }
}

const runCli = () => {
  const thresholdArg = process.argv.find((arg) => arg.startsWith('--threshold='))
  const threshold = thresholdArg ? Number(thresholdArg.split('=')[1]) : -1

  const productionFiles = walk(PRODUCTION_ROOT, ['.fs']).map(norm).filter(isProgramFile)
  const entries = productionFiles.map((file) => ({
    file,
    text: readFileSync(file, 'utf8'),
  }))
  const violations = scanFiles(entries)
  const byGate = groupByGate(violations)
  const write = threshold >= 0 ? console.log : console.error

  if (violations.length === 0) {
    write(`dsl-ownership: OK — ${productionFiles.length} Program/Domain files`)
    process.exit(0)
  }

  write(`dsl-ownership: ${violations.length} violation(s) — ${productionFiles.length} files\n`)
  for (const [gate, items] of byGate) {
    write(`${gate} (${items.length})`)
    for (const v of items) {
      write(`  ${v.file}:${v.line}  ${v.text}`)
    }
    write('')
  }

  const decision = evaluateThreshold(violations.length, threshold)
  if (decision.reason === 'within-threshold') {
    console.log(
      `dsl-ownership: within threshold — ${violations.length}/${threshold} violations (freeze only)`,
    )
    process.exit(0)
  }
  if (decision.reason === 'exceeds-threshold') {
    console.error(`dsl-ownership: exceeds threshold — ${violations.length} > ${threshold}`)
    process.exit(1)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
