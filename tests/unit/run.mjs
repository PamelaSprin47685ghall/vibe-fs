#!/usr/bin/env node
// tests/unit/run.mjs — layers 1-3 entry point, and the causal-progress gate over them.
//
// Three responsibilities, in order:
//
//   1. Staleness gate. mjs tests import dist, so a stale build means the suite silently
//      describes yesterday's code. Fail closed rather than emit a green light for bytes nobody
//      asked about.
//   2. Supervise a child that runs node:test, with a silence window fed by test VERDICTS.
//   3. Own the authoritative counts, because the reporter's are wrong (see supervise helper).
//
// Discovery is *.test.mjs under tests/unit/. No compile step, no export scanning: node:test owns
// registration.
//
//   node tests/unit/run.mjs
//   node tests/unit/run.mjs --skip-staleness-check   (build-tooling work only)
//
// Supervision lives in tests/e2e/support/supervise-node-test.mjs so integration/package share
// the same verdict-silence criterion (VERIFY-004). UNIT_VERDICT_SILENCE_MS remains the unit budget.

import { statSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'

import { UNIT_VERDICT_SILENCE_MS } from '../e2e/support/time-budget.js'
import { superviseNodeTest } from '../e2e/support/supervise-node-test.mjs'
import { walk } from '../../scripts/lib/walk.mjs'

const TESTS_ROOT = 'tests/unit'
const PRODUCTION_ROOT = 'src/Wanxiangshu'
const BUILD_ROOT = 'dist'
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..')

const skipStaleness = process.argv.includes('--skip-staleness-check')
const withCoverage = process.argv.includes('--coverage')

// Coverage is measured in the inner runner (NODE_TEST_COVERAGE) so the V8 map lives in the same
// process as the tests. The threshold is VERIFY-009's floor, and the summary lands in artifacts/.
if (withCoverage) {
  process.env.NODE_TEST_COVERAGE = '1'
  process.env.COVERAGE_SUMMARY_PATH = join(ROOT, 'artifacts/coverage/coverage-summary.json')
  process.env.COVERAGE_LINE_THRESHOLD = '60'
  console.error('runner: coverage ON — summary → artifacts/coverage/coverage-summary.json, threshold 60% lines')
}

// ── staleness gate ──────────────────────────────────────────────────────────

const newestFile = (files) => {
  let newest = null
  for (const file of files) {
    let stat
    try {
      stat = statSync(file)
    } catch {
      continue
    }
    if (newest === null || stat.mtimeMs > newest.mtimeMs) newest = { file, mtimeMs: stat.mtimeMs }
  }
  return newest
}

const checkBuildFreshness = () => {
  const sources = [...walk(PRODUCTION_ROOT, ['.fs']), ...walk(PRODUCTION_ROOT, ['.fsproj'])]
  if (sources.length === 0) return { ok: false, reason: `no sources found under ${PRODUCTION_ROOT}/` }

  // fable_modules holds vendored library output that Fable does not rewrite on every build;
  // comparing against it would mask a stale project build.
  const artifacts = walk(BUILD_ROOT, ['.js']).filter((file) => !file.includes('fable_modules'))
  if (artifacts.length === 0) {
    return { ok: false, reason: `${BUILD_ROOT}/ has no compiled output — run: npm run build` }
  }

  const newestSource = newestFile(sources)
  const newestArtifact = newestFile(artifacts)

  if (newestSource.mtimeMs > newestArtifact.mtimeMs) {
    const staleBy = Math.round((newestSource.mtimeMs - newestArtifact.mtimeMs) / 1000)
    return {
      ok: false,
      reason: [
        `${BUILD_ROOT}/ is stale by ${staleBy}s — run: npm run build`,
        `  newest source:   ${relative('.', newestSource.file)}`,
        `  newest artifact: ${relative('.', newestArtifact.file)}`,
      ].join('\n'),
    }
  }

  return { ok: true, sources: sources.length, artifacts: artifacts.length }
}

if (skipStaleness) {
  console.error('runner: staleness check SKIPPED by flag — results do not describe current sources')
} else {
  const freshness = checkBuildFreshness()
  if (!freshness.ok) {
    console.error(`runner: refusing to run.\n${freshness.reason}`)
    process.exit(1)
  }
  console.error(`runner: build is current (${freshness.sources} sources, ${freshness.artifacts} artifacts)`)
}

// ── discovery ───────────────────────────────────────────────────────────────

// `fixtures/` is excluded by construction, not by filter: its files are named `*.fixture.mjs`
// precisely so this walk cannot reach them. They exist to hang, and one swept into the real suite
// would hang it. `gate-unit-runner-cases.mjs` asserts that naming still holds.
//
// `TESTS_MJS_FILES` overrides discovery with an explicit list. It exists for the gate cases, which
// need to drive a hang fixture through the REAL supervisor while keeping that fixture undiscoverable
// — the two properties are otherwise in conflict. A test-only entry point in production code would
// be forbidden; this is the runner's own harness surface, and the override is announced on stderr so
// it cannot be used to quietly narrow a CI run.
const override = process.env.TESTS_MJS_FILES
const files = override
  ? override
      .split(',')
      .map((file) => file.trim())
      .filter(Boolean)
  : walk(TESTS_ROOT, ['.test.mjs'])

if (override) console.error(`runner: discovery OVERRIDDEN by TESTS_MJS_FILES (${files.length} file(s))`)

if (files.length === 0) {
  console.error(`runner: no *.test.mjs found under ${TESTS_ROOT}/`)
  process.exit(1)
}

// Same 3s dog as e2e (UNIT_VERDICT_SILENCE_MS === WATCHDOG_TIMEOUT_MS; gate injects the former).
await superviseNodeTest({
  files,
  label: 'tests/unit',
  silenceMs: UNIT_VERDICT_SILENCE_MS,
  logPrefix: 'runner',
})
