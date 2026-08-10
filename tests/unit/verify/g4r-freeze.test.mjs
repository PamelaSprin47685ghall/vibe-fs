/**
 * G4R-0 Freeze permanent gate: E2E population and timeout budgets must not grow
 * during migration to One World / Pure Time (changes/active/test.md).
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  E2E_CASE_CEILING,
  TIMEOUT_CEILINGS,
  parseTimeBudgetDefaults,
  scanCaseCeiling,
  scanG4RFreeze,
  scanPerCaseTimeoutMaps,
  scanTimeoutCeilings,
  scanTopLevelE2EEntries,
} from '../../../scripts/checks/g4r-freeze.mjs'

test('G4R_FREEZE_current_tree_passes', () => {
  const { caseCount, violations } = scanG4RFreeze()
  assert.ok(caseCount <= E2E_CASE_CEILING, `case count ${caseCount} > ceiling ${E2E_CASE_CEILING}`)
  assert.equal(
    violations.length,
    0,
    'G4R-0 freeze violations: ' + JSON.stringify(violations, null, 2),
  )
})

test('G4R_FREEZE_case_ceiling_rejects_growth', () => {
  const ok = scanCaseCeiling({ caseCount: E2E_CASE_CEILING })
  assert.equal(ok.length, 0)
  const over = scanCaseCeiling({ caseCount: E2E_CASE_CEILING + 1 })
  assert.equal(over.length, 1)
  assert.match(over[0], /exceeds G4R-0 freeze ceiling/)
})

test('G4R_FREEZE_timeout_ceiling_rejects_inflation', () => {
  const defaults = Object.fromEntries(
    Object.entries(TIMEOUT_CEILINGS).map(([name, ceiling]) => [name, ceiling]),
  )
  assert.equal(scanTimeoutCeilings(defaults).length, 0)

  const inflated = { ...defaults, CANARY_TIMEOUT_MS: TIMEOUT_CEILINGS.CANARY_TIMEOUT_MS + 1 }
  const hits = scanTimeoutCeilings(inflated)
  assert.ok(hits.some((m) => m.includes('CANARY_TIMEOUT_MS')))
})

test('G4R_FREEZE_parse_time_budget_defaults', () => {
  const source = `
export const CANARY_TIMEOUT_MS = 90000;
export const WATCHDOG_TIMEOUT_MS = budgetFromEnv('WATCHDOG_TIMEOUT_MS', 5000);
export const PER_TEST_TIMEOUT_MS = budgetFromEnv('PER_TEST_TIMEOUT_MS', 2500);
`
  const parsed = parseTimeBudgetDefaults(source)
  assert.equal(parsed.CANARY_TIMEOUT_MS, 90000)
  assert.equal(parsed.WATCHDOG_TIMEOUT_MS, 5000)
  assert.equal(parsed.PER_TEST_TIMEOUT_MS, 2500)
})

test('G4R_FREEZE_per_case_timeout_map_detector', () => {
  const clean = scanPerCaseTimeoutMaps([
    { file: 'tests/e2e/support/time-budget.js', text: 'export const CANARY_TIMEOUT_MS = 90000\n' },
  ])
  assert.equal(clean.length, 0)

  const dirty = scanPerCaseTimeoutMaps([
    {
      file: 'tests/e2e/support/evil.js',
      text: 'const CANARY_TIMEOUT_BY_BASENAME = { "x.test.mjs": 120000 }\n',
    },
  ])
  assert.equal(dirty.length, 1)
  assert.match(dirty[0].pattern, /CANARY_TIMEOUT_BY_BASENAME/)
})

test('G4R_FREEZE_top_level_entry_forbidden_during_freeze', () => {
  assert.equal(scanTopLevelE2EEntries([]).length, 0)
  const hits = scanTopLevelE2EEntries(['tests/e2e/entry.test.mjs'])
  assert.equal(hits.length, 1)
  assert.match(hits[0], /unexpected top-level E2E entry/)
})
