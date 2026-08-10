#!/usr/bin/env node
/**
 * g4r-freeze.mjs — G4R-0 Freeze ratchet (changes/active/test.md).
 *
 * During migration toward One World / Pure Time:
 *   - E2E case count must not grow (ceiling only decreases)
 *   - Named wall-clock budgets in time-budget.js must not inflate
 *   - No per-basename / per-case canary timeout maps
 *   - No extra top-level E2E entry tests outside cases/ (Long Stroke lands later as entry.test.mjs)
 *
 * Final one-world ratchets (count == 1, spawn == 1, …) arrive in G4R-4/G4R-6 — not here.
 *
 * Usage: node scripts/checks/g4r-freeze.mjs
 */

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { basename, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const ROOT = fileURLToPath(new URL('../..', import.meta.url))

/** Freeze ceiling: current multi-canary population. May decrease; must never increase. */
export const E2E_CASE_CEILING = 31

/** Named budgets in tests/e2e/support/time-budget.js — freeze ceilings (defaults only). */
export const TIMEOUT_CEILINGS = Object.freeze({
  CANARY_TIMEOUT_MS: 90_000,
  PER_TEST_TIMEOUT_MS: 2_500,
  WATCHDOG_TIMEOUT_MS: 5_000,
  HARNESS_CASE_SILENCE_MS: 20_000,
  WAIT_FACT_WINDOW_MS: 120_000,
  READINESS_STAGE_MS: 4_000,
  FORK_COMPLETION_WINDOW_MS: 10_000,
  SUITE_BACKSTOP_MS: 300_000,
  UNIT_VERDICT_SILENCE_MS: 5_000,
})

export const TIME_BUDGET_REL = 'tests/e2e/support/time-budget.js'
export const E2E_CASES_REL = 'tests/e2e/cases'
export const E2E_ROOT_REL = 'tests/e2e'

const norm = (p) => p.replace(/\\/g, '/')

/**
 * Detect basename→timeout style maps / per-case canary ceilings.
 * Storage Phase 8 already forbade FINALITY_COHORT_* / per-basename ceilings;
 * G4R-0 keeps that machine-enforced during migration.
 */
export const PER_CASE_TIMEOUT_PATTERNS = [
  /\bCANARY_TIMEOUT_BY_BASENAME\b/,
  /\bbasenameTimeouts\b/,
  /\bperCaseTimeout\b/i,
  /\bPER_CASE_CANARY_TIMEOUT\b/,
  /\bFINALITY_COHORT_\w*TIMEOUT\b/,
  /\bTIMEOUT_BY_BASENAME\b/,
  /\bcanaryTimeoutByBasename\b/,
]

/**
 * Parse a time-budget source string for named export defaults.
 * Accepts either `export const NAME = 123` or `budgetFromEnv('NAME', 123)`.
 * @param {string} source
 * @returns {Record<string, number>}
 */
export const parseTimeBudgetDefaults = (source) => {
  const found = {}
  const direct =
    /export\s+const\s+([A-Z][A-Z0-9_]*)\s*=\s*(?:budgetFromEnv\s*\(\s*['"][A-Z0-9_]+['"]\s*,\s*)?(\d+)\s*\)?/g
  let m
  while ((m = direct.exec(source)) !== null) {
    found[m[1]] = Number(m[2])
  }
  return found
}

/**
 * @param {{ caseCount: number, ceiling?: number }} input
 * @returns {string[]}
 */
export const scanCaseCeiling = ({ caseCount, ceiling = E2E_CASE_CEILING }) => {
  if (!Number.isInteger(caseCount) || caseCount < 0) {
    return [`e2e case count is not a non-negative integer: ${caseCount}`]
  }
  if (caseCount > ceiling) {
    return [
      `E2E case count ${caseCount} exceeds G4R-0 freeze ceiling ${ceiling} — ` +
        'do not add E2E cases; migrate adversity into temporal proofs / The Long Stroke',
    ]
  }
  return []
}

/**
 * @param {Record<string, number>} defaults
 * @param {Record<string, number>} [ceilings]
 * @returns {string[]}
 */
export const scanTimeoutCeilings = (defaults, ceilings = TIMEOUT_CEILINGS) => {
  const violations = []
  for (const [name, ceiling] of Object.entries(ceilings)) {
    if (!(name in defaults)) {
      violations.push(
        `time-budget.js missing frozen budget '${name}' (expected default ≤ ${ceiling})`,
      )
      continue
    }
    const value = defaults[name]
    if (!Number.isFinite(value) || value > ceiling) {
      violations.push(
        `timeout inflation: ${name}=${value} exceeds G4R-0 freeze ceiling ${ceiling}`,
      )
    }
  }
  return violations
}

/**
 * @param {{ file: string, text: string }[]} entries
 * @returns {{ file: string, line: number, pattern: string, text: string }[]}
 */
export const scanPerCaseTimeoutMaps = (entries) => {
  const violations = []
  for (const { file, text } of entries) {
    const lines = text.split('\n')
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i]
      for (const pattern of PER_CASE_TIMEOUT_PATTERNS) {
        if (pattern.test(line)) {
          violations.push({
            file: norm(file),
            line: i + 1,
            pattern: pattern.source,
            text: line.trim(),
          })
        }
      }
    }
  }
  return violations
}

/**
 * Top-level tests/e2e/*.test.mjs are forbidden until Long Stroke lands as the sole entry.
 * Nested cases/ are counted by the ceiling; support/ is not an entry.
 * @param {string[]} topLevelTestFiles absolute or relative paths
 * @returns {string[]}
 */
export const scanTopLevelE2EEntries = (topLevelTestFiles) => {
  if (topLevelTestFiles.length === 0) return []
  return [
    `unexpected top-level E2E entr${topLevelTestFiles.length === 1 ? 'y' : 'ies'} ` +
      `(G4R-0 freeze; Long Stroke will be the sole entry later): ` +
      topLevelTestFiles.map((f) => basename(f)).join(', '),
  ]
}

/**
 * Count *.test.mjs under casesDir (non-recursive flat dir is fine; walk is used for safety).
 * @param {string} casesDir
 */
export const countE2ECases = (casesDir) => {
  if (!existsSync(casesDir)) {
    throw new Error(`g4r-freeze: missing cases directory ${casesDir}`)
  }
  return walk(casesDir, ['.test.mjs']).length
}

/**
 * List only direct children *.test.mjs of e2e root (not under cases/ or support/).
 * @param {string} e2eRoot
 */
export const listTopLevelE2ETests = (e2eRoot) => {
  if (!existsSync(e2eRoot)) return []
  return readdirSync(e2eRoot)
    .filter((name) => name.endsWith('.test.mjs'))
    .map((name) => join(e2eRoot, name))
    .filter((p) => {
      try {
        return statSync(p).isFile()
      } catch {
        return false
      }
    })
}

/**
 * Full scan against a repo root (injectable for unit tests).
 * @param {string} [root]
 */
export const scanG4RFreeze = (root = ROOT) => {
  const violations = []
  const casesDir = join(root, E2E_CASES_REL)
  const e2eRoot = join(root, E2E_ROOT_REL)
  const budgetPath = join(root, TIME_BUDGET_REL)

  const caseCount = countE2ECases(casesDir)
  violations.push(...scanCaseCeiling({ caseCount }).map((message) => ({ kind: 'case-ceiling', message })))

  if (!existsSync(budgetPath)) {
    violations.push({
      kind: 'timeout-ceiling',
      message: `missing ${TIME_BUDGET_REL}`,
    })
  } else {
    const defaults = parseTimeBudgetDefaults(readFileSync(budgetPath, 'utf8'))
    violations.push(
      ...scanTimeoutCeilings(defaults).map((message) => ({ kind: 'timeout-ceiling', message })),
    )
  }

  const e2eFiles = existsSync(e2eRoot)
    ? walk(e2eRoot, ['.js', '.mjs']).map((file) => ({
        file: relative(root, file) || file,
        text: readFileSync(file, 'utf8'),
      }))
    : []
  for (const hit of scanPerCaseTimeoutMaps(e2eFiles)) {
    violations.push({
      kind: 'per-case-timeout',
      message: `${hit.file}:${hit.line} matches /${hit.pattern}/ — ${hit.text}`,
    })
  }

  const topLevel = listTopLevelE2ETests(e2eRoot).map((p) => relative(root, p) || p)
  violations.push(
    ...scanTopLevelE2EEntries(topLevel).map((message) => ({ kind: 'top-level-entry', message })),
  )

  return { caseCount, violations }
}

const runCli = () => {
  const { caseCount, violations } = scanG4RFreeze(ROOT)

  if (violations.length === 0) {
    console.log(
      `g4r-freeze: OK — cases ${caseCount}/${E2E_CASE_CEILING}; timeout ceilings held; no per-case timeout maps; no top-level E2E entry`,
    )
    process.exit(0)
  }

  console.error(`g4r-freeze: ${violations.length} violation(s) (cases ${caseCount}/${E2E_CASE_CEILING})\n`)
  for (const v of violations) {
    console.error(`  [${v.kind}] ${v.message}`)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
