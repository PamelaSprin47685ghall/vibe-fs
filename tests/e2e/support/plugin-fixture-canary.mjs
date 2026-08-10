/**
 * plugin-fixture-canary.mjs — readiness bark + sequential runner for canaries that
 * exercise the Host surface through `withExecutablePlugin` rather than `setupScenario`.
 *
 * `tests/e2e/run.mjs` gates launch stagger on the readiness ladder ending at
 * `[setupScenario] ready`. Plugin-fixture canaries never print those stage lines, so they
 * must emit a synthetic climb early (before heavy work) or the bark gate fails them as hung
 * startups. Markers are taken from `READINESS_STAGES` so order cannot drift from the ladder.
 */

import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { READINESS_STAGES } from './readiness.js'

/** Print every ladder marker, then the exact ready bark `run.mjs` matches. */
export function barkPluginFixtureReady() {
  for (const { marker } of READINESS_STAGES) {
    console.log(marker)
  }
}

/**
 * Sequential assert-driven runner. Process bound is the e2e launcher canary budget
 * (`CANARY_TIMEOUT_MS`) — never `PER_TEST_TIMEOUT_MS` and never a raised per-case ceiling.
 *
 * @param {{ name: string, fn: () => Promise<void> | void }[]} cases
 * @param {{ label?: string }} [opts]
 * @returns {Promise<number>} exit code
 */
export async function runSequentialCases(cases, opts = {}) {
  const label = opts.label ?? 'plugin-fixture-canary'
  let failed = 0

  for (const { name, fn } of cases) {
    const started = Date.now()
    try {
      await fn()
      console.log(`  ✓ ${name} (${Date.now() - started}ms)`)
    } catch (error) {
      failed += 1
      console.error(`  ✗ ${name} (${Date.now() - started}ms)`)
      console.error(error)
    }
  }

  const passed = cases.length - failed
  console.log(`\n${label}: ${passed} passed, ${failed} failed`)
  return failed > 0 ? 1 : 0
}

/** True when this module URL is the process entrypoint. */
export function isMainModule(metaUrl) {
  const entry = process.argv[1]
  if (!entry) return false
  return fileURLToPath(metaUrl) === resolve(entry)
}
