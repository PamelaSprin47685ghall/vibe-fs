/**
 * G4R-0 Freeze permanent gate: E2E population and timeout budgets must not grow
 * during migration to One World / Pure Time (changes/active/test.md).
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  E2E_CASE_CEILING,
  LONG_STROKE_ENTRY_REL,
  TIMEOUT_CEILINGS,
  nextCaseCeiling,
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

test('G4R_FREEZE_case_ceiling_may_only_decrease', () => {
  const ceiling = E2E_CASE_CEILING
  // No deletions: ceiling stays put.
  assert.equal(nextCaseCeiling({ caseCount: ceiling, ceiling }), ceiling)
  // Cases deleted: ratchet down to remaining count (G4R-4: already 0).
  if (ceiling > 0) {
    assert.equal(nextCaseCeiling({ caseCount: ceiling - 1, ceiling }), ceiling - 1)
  }
  assert.equal(nextCaseCeiling({ caseCount: 0, ceiling }), 0)
  // Never raise — even if caseCount somehow exceeds the freeze bar.
  assert.equal(nextCaseCeiling({ caseCount: ceiling + 5, ceiling }), ceiling)
  // Monotone: successive deletions only lower (or hold) the ceiling.
  // Exercise from a positive historical bar so the ratchet path stays covered at 0.
  let current = Math.max(ceiling, 31)
  for (const remaining of [current, current - 3, current - 10, 1, 0]) {
    const next = nextCaseCeiling({ caseCount: remaining, ceiling: current })
    assert.ok(next <= current, `ceiling rose: ${current} → ${next}`)
    current = next
  }
  assert.equal(current, 0)
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

test('G4R_FREEZE_top_level_entry_required_exactly_one_when_present', () => {
  // Pre-cutover: zero top-level *.test.mjs is OK.
  assert.equal(scanTopLevelE2EEntries([]).length, 0)
  // Cutover: sole Long Stroke entry is OK.
  assert.equal(scanTopLevelE2EEntries([LONG_STROKE_ENTRY_REL]).length, 0)

  const wrongName = scanTopLevelE2EEntries(['tests/e2e/other.test.mjs'])
  assert.equal(wrongName.length, 1)
  assert.match(wrongName[0], /required-exactly-one-when-present/)
  assert.match(wrongName[0], /entry\.test\.mjs/)

  const tooMany = scanTopLevelE2EEntries([
    LONG_STROKE_ENTRY_REL,
    'tests/e2e/other.test.mjs',
  ])
  assert.equal(tooMany.length, 1)
  assert.match(tooMany[0], /required-exactly-one-when-present/)
})
