import { readdirSync } from 'node:fs'

/**
 * Deterministic discovery of `*.test.mjs` files directly inside a suite
 * directory. Non-test files (including any `run.mjs`) and subdirectories are
 * excluded; results are sorted so ordering is reproducible.
 *
 * VERIFICATION-SYSTEM-009: this is the single source of truth for which
 * child-owned suite tests exist. Both the child runner (which executes them)
 * and the parent integration entry (which delegates them and fail-closes on
 * drift) consume the same discovered set, so coverage and execution cannot
 * diverge — an added `*.test.mjs` is picked up automatically by both, and a
 * missing/stale/duplicate child test makes the parent entry go red.
 *
 * @param {string} dir absolute directory to scan (flat; non-recursive)
 * @param {{ testSuffix?: string }} [opts]
 * @returns {string[]} sorted bare filenames (e.g. `['contents.test.mjs', ...]`)
 */
export function discoverSuiteTests(dir, { testSuffix = '.test.mjs' } = {}) {
  let entries
  try {
    entries = readdirSync(dir, { withFileTypes: true })
  } catch {
    return []
  }
  return entries
    .filter((entry) => entry.isFile() && entry.name.endsWith(testSuffix))
    .map((entry) => entry.name)
    .sort()
}
