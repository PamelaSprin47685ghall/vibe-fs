// tests/support/coverage-policy.mjs — pure coverage policy for the verification runner.
//
// Extracted from run-inner.mjs so the coverage gate's three obligations
// (whole-codebase denominator, fail-closed on preload failure, finite-positive
// threshold) can be exercised behaviorally without spawning a full coverage run.
//
// run-inner.mjs imports and calls every export below; the behavioral tests in
// coverage-gate.test.mjs exercise them directly with deterministic fixtures.

// Fixed exclude globs: production bytes only. The runner, support files, tests,
// the Fable runtime (fable_modules), vendored packages and repo tooling (scripts/)
// are noise that must never enter the denominator.
export const COVERAGE_EXCLUDE_GLOBS = [
  '**/node_modules/**',
  '**/fable_modules/**',
  '**/tests/**',
  '**/scripts/**',
]

export const DEFAULT_COVERAGE_LINE_THRESHOLD = 80

/**
 * Parse and validate COVERAGE_LINE_THRESHOLD.
 *
 * Returns a positive finite number (defaulting to 80 when raw is null/undefined).
 * Throws RangeError on any non-positive or non-finite value so the caller can
 * exit(2) with a clear message — a threshold of 0 or NaN would silently let
 * any coverage pass.
 */
export function parseCoverageThreshold(raw) {
  const value = raw == null ? DEFAULT_COVERAGE_LINE_THRESHOLD : Number(raw)
  if (!Number.isFinite(value) || value <= 0) {
    throw new RangeError(
      `COVERAGE_LINE_THRESHOLD must be a positive finite number, got ${raw}`,
    )
  }
  return value
}

/**
 * Filter walked dist files to production modules: exclude fable_modules.
 * A module nobody tests must count its lines at 0% instead of vanishing —
 * that is what makes the denominator a true whole-codebase number.
 */
export function selectProductionModules(files) {
  return files.filter((file) => !file.includes('fable_modules'))
}

/**
 * Pre-import every module via importFn.
 *
 * Returns { total, failures, failedFiles }. The caller fails closed when
 * failures > 0: a module that only loads to its failure point is counted only
 * up to that point, producing a dishonest denominator. Aborting is the only
 * honest response.
 */
export async function preImportModules(modules, importFn) {
  let failures = 0
  const failedFiles = []
  for (const file of modules) {
    try {
      await importFn(file)
    } catch (error) {
      failures += 1
      failedFiles.push({ file, message: error?.message ?? String(error) })
    }
  }
  return { total: modules.length, failures, failedFiles }
}

/**
 * Evaluate a node:test coverage summary against the threshold.
 *
 * Returns { ok, percent, totals, reason }:
 *   - reason 'pass'              — percent >= threshold
 *   - reason 'below-threshold'   — percent < threshold (caller must fail)
 *   - reason 'no-coverage-event' — summary or totals missing (run is broken)
 */
export function evaluateCoverage(summary, threshold) {
  const totals = summary?.totals
  if (!totals) {
    return { ok: false, percent: null, totals: null, reason: 'no-coverage-event' }
  }
  const percent = totals.coveredLinePercent
  const ok = percent >= threshold
  return { ok, percent, totals, reason: ok ? 'pass' : 'below-threshold' }
}
