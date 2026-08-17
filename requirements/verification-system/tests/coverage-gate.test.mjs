// coverage-gate.test.mjs — VERIFICATION-SYSTEM-011 的机器落点。
//
// 覆盖率门禁（run.mjs --coverage → support/run-inner.mjs → support/coverage-policy.mjs）
// 的「分母完整」义务，以行为测试行使真实实现路径：
// 1) 覆盖率必须先预导入全部 dist 生产模块（排除 fable_modules），未加载模块以 0%
//    计入分母而不是消失；2) 低于 COVERAGE_LINE_THRESHOLD 必须 exit 1，无豁免通道；
// 3) 排除项固定为 node_modules / fable_modules / tests / scripts。
//
// 主证据：直接调用 run-inner.mjs 所导入的 coverage-policy.mjs 纯函数，用确定性临时
// 模块夹具行使预导入失败与阈值判定。删掉任一义务的 helper 逻辑立即红。
// 补充证据：静态断言 run-inner.mjs 确实接入了 helper（非唯一证据）。

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync, writeFileSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import test from 'node:test'

import {
  COVERAGE_EXCLUDE_GLOBS,
  DEFAULT_COVERAGE_LINE_THRESHOLD,
  parseCoverageThreshold,
  selectProductionModules,
  preImportModules,
  evaluateCoverage,
} from './support/coverage-policy.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const RUNNER_PATH = join(ROOT, 'requirements/verification-system/tests/support/run-inner.mjs')

// ── threshold parsing ──────────────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-011] parseCoverageThreshold accepts valid positive finite numbers', () => {
  assert.equal(parseCoverageThreshold('80'), 80)
  assert.equal(parseCoverageThreshold('1'), 1)
  assert.equal(parseCoverageThreshold('99.5'), 99.5)
  assert.equal(parseCoverageThreshold(undefined), DEFAULT_COVERAGE_LINE_THRESHOLD)
  assert.equal(parseCoverageThreshold(null), DEFAULT_COVERAGE_LINE_THRESHOLD)
})

test('WHAT[VERIFICATION-SYSTEM-011] parseCoverageThreshold rejects non-positive or non-finite thresholds (no bypass)', () => {
  // A threshold of 0 or NaN would silently let any coverage pass — must be rejected.
  for (const bad of ['0', '-1', 'NaN', 'Infinity', 'abc', '']) {
    assert.throws(() => parseCoverageThreshold(bad), RangeError, `should reject ${JSON.stringify(bad)}`)
  }
})

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

// ── pre-import failure (fail closed) ───────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-011] preImportModules counts failures so the runner can fail closed on partial denominator', async () => {
  const tmp = mkdtempSync(join(tmpdir(), 'cov-gate-fail-'))
  try {
    const ok = join(tmp, 'ok.mjs')
    const bad = join(tmp, 'bad.mjs')
    writeFileSync(ok, 'export const x = 1\n')
    writeFileSync(bad, 'throw new Error("boom")\n')

    const result = await preImportModules(
      [pathToFileURL(ok).href, pathToFileURL(bad).href],
      (file) => import(file),
    )
    assert.equal(result.total, 2)
    assert.equal(result.failures, 1)
    assert.equal(result.failedFiles.length, 1)
    assert.equal(result.failedFiles[0].file, pathToFileURL(bad).href)
    assert.match(result.failedFiles[0].message, /boom/)
  } finally {
    rmSync(tmp, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-011] preImportModules reports zero failures when all modules load', async () => {
  const tmp = mkdtempSync(join(tmpdir(), 'cov-gate-ok-'))
  try {
    const a = join(tmp, 'a.mjs')
    const b = join(tmp, 'b.mjs')
    writeFileSync(a, 'export const a = 1\n')
    writeFileSync(b, 'export const b = 2\n')

    const result = await preImportModules(
      [pathToFileURL(a).href, pathToFileURL(b).href],
      (file) => import(file),
    )
    assert.equal(result.total, 2)
    assert.equal(result.failures, 0)
    assert.deepEqual(result.failedFiles, [])
  } finally {
    rmSync(tmp, { recursive: true, force: true })
  }
})

// ── threshold enforcement ──────────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-011] evaluateCoverage passes when percent meets threshold', () => {
  const summary = { totals: { coveredLinePercent: 80, coveredLineCount: 80, totalLineCount: 100 } }
  const result = evaluateCoverage(summary, 80)
  assert.equal(result.ok, true)
  assert.equal(result.percent, 80)
  assert.equal(result.reason, 'pass')
})

test('WHAT[VERIFICATION-SYSTEM-011] evaluateCoverage fails when percent is below threshold (no exemption)', () => {
  const summary = { totals: { coveredLinePercent: 50, coveredLineCount: 50, totalLineCount: 100 } }
  const result = evaluateCoverage(summary, 80)
  assert.equal(result.ok, false)
  assert.equal(result.percent, 50)
  assert.equal(result.reason, 'below-threshold')
})

test('WHAT[VERIFICATION-SYSTEM-011] evaluateCoverage fails closed when no coverage event arrived', () => {
  assert.equal(evaluateCoverage(null, 80).ok, false)
  assert.equal(evaluateCoverage(null, 80).reason, 'no-coverage-event')
  assert.equal(evaluateCoverage({}, 80).ok, false)
  assert.equal(evaluateCoverage({}, 80).reason, 'no-coverage-event')
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

// ── supplemental: the runner actually wires the helper ─────────────────────
//
// Static source assertions are NOT the sole evidence — every policy above is
// exercised behaviorally. This check only guards against accidental unwiring.

test('WHAT[VERIFICATION-SYSTEM-011] run-inner.mjs imports and uses the coverage-policy helper (supplemental)', () => {
  const src = readFileSync(RUNNER_PATH, 'utf8')
  assert.match(src, /from ['"]\.\/coverage-policy\.mjs['"]/)
  assert.match(src, /parseCoverageThreshold/)
  assert.match(src, /selectProductionModules/)
  assert.match(src, /preImportModules/)
  assert.match(src, /evaluateCoverage/)
  assert.match(src, /COVERAGE_EXCLUDE_GLOBS/)
  // The runner must still fail closed on preload failures and enforce the threshold.
  assert.match(src, /preImport\.failures > 0/)
  assert.match(src, /process\.exit\(1\)/)
  assert.match(src, /if \(!ok\) process\.exitCode = 1/)
})
