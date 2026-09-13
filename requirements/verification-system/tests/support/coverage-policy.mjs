// tests/support/coverage-policy.mjs — pure coverage policy for the verification system.
//
// Used by scripts/coverage.mjs and tested by coverage-runner.test.mjs to verify
// the production denominator matches dist emitted JS excluding fable_modules.

import path from 'node:path'

export const COVERAGE_EXCLUDE_GLOBS = [
  '**/node_modules/**',
  '**/fable_modules/**',
  '**/tests/**',
  '**/scripts/**',
]

/**
 * Filter walked dist files to production modules: exclude fable_modules.
 * A module nobody tests must count its lines at 0% instead of vanishing —
 * that is what makes the denominator a true whole-codebase number.
 */
export function selectProductionModules(files) {
  return files
    .map((file) => file.replace(/\\/g, '/'))
    .filter((file) => file.endsWith('.js'))
    .filter((file) => {
      const segments = file.split('/')
      return !segments.includes('fable_modules')
    })
}

/**
 * Verify that the files reported by coverage tool match the expected production modules.
 */
export function verifyCoverageDenominator(reportedFiles, expectedFiles) {
  const normReported = new Set(reportedFiles.map((f) => path.normalize(f)))
  const normExpected = new Set(expectedFiles.map((f) => path.normalize(f)))

  const missing = [...normExpected].filter((f) => !normReported.has(f)).sort()
  const extra = [...normReported].filter((f) => !normExpected.has(f)).sort()

  return {
    ok: missing.length === 0 && extra.length === 0,
    missing,
    extra,
  }
}
