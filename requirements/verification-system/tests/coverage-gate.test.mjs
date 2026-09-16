// coverage-gate.test.mjs — VERIFICATION-SYSTEM-011 辅助策略测试。
//
// 验证生产文件分母过滤（selectProductionModules）与覆盖率排除规则（COVERAGE_EXCLUDE_GLOBS）。
// 完整的覆盖率执行与分母核对由 coverage-runner.test.mjs 行使。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import test from 'node:test'

import {
  COVERAGE_EXCLUDE_GLOBS,
  selectProductionModules,
  verifyCoverageDenominator,
} from './support/coverage-policy.mjs'

// ── module selection (denominator completeness) ────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-011] selectProductionModules excludes fable_modules so untested modules count at 0%', () => {
  const files = [
    'dist/foo.js',
    'dist/fable_modules/bar.js',
    'dist/sub/fable_modules/baz.js',
    'dist/qux.js',
  ]
  assert.deepEqual(selectProductionModules(files), ['dist/foo.js', 'dist/qux.js'])
})

test('WHAT[VERIFICATION-SYSTEM-011] verifyCoverageDenominator catches missing and extra files', () => {
  const expected = ['dist/a.js', 'dist/b.js']
  const reported1 = ['dist/a.js', 'dist/b.js']
  assert.equal(verifyCoverageDenominator(reported1, expected).ok, true)

  const reported2 = ['dist/a.js']
  const check2 = verifyCoverageDenominator(reported2, expected)
  assert.equal(check2.ok, false)
  assert.deepEqual(check2.missing, ['dist/b.js'])

  const reported3 = ['dist/a.js', 'dist/b.js', 'dist/extra.js']
  const check3 = verifyCoverageDenominator(reported3, expected)
  assert.equal(check3.ok, false)
  assert.deepEqual(check3.extra, ['dist/extra.js'])
})

// ── fixed excludes ─────────────────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-011] coverage exclude globs are fixed: node_modules, fable_modules, tests, scripts', () => {
  assert.deepEqual(COVERAGE_EXCLUDE_GLOBS, [
    '**/node_modules/**',
    '**/fable_modules/**',
    '**/tests/**',
    '**/scripts/**',
  ])
})
