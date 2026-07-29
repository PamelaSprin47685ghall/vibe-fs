#!/usr/bin/env node
// tests-mjs/runner.mjs — layers 1-3 test entry point (VERIFY-008).
//
// Two responsibilities, in order:
//
//   1. Staleness gate. mjs tests import build/next, so a stale build means the
//      suite silently describes yesterday's code. Fail closed rather than emit
//      a green light for bytes nobody asked about.
//   2. Delegate to node:test with a hard per-test timeout, so a hung causal
//      wait fails instead of parking the suite.
//
// Discovery is *.test.mjs under tests-mjs/. No compile step, no export
// scanning: node:test owns registration.
//
//   node tests-mjs/runner.mjs
//   node tests-mjs/runner.mjs --skip-staleness-check   (build-tooling work only)

import { statSync } from 'node:fs'
import { relative } from 'node:path'
import { run } from 'node:test'
import { spec } from 'node:test/reporters'
import { walk } from '../scripts/repo-scan.mjs'

// A pure fold or a fake-port trajectory has no reason to take a second. Layer 3
// Host trajectories use a fake clock, so they do not need wall-clock headroom
// either. Raising this is how a race gets papered over (VERIFY-002).
const PER_TEST_TIMEOUT_MS = 1000

// Whole-suite ceiling, so a runaway file cannot hold CI forever.
const SUITE_TIMEOUT_MS = 300000

const TESTS_ROOT = 'tests-mjs'
const PRODUCTION_ROOT = 'next'
const BUILD_ROOT = 'build/next'

const skipStaleness = process.argv.includes('--skip-staleness-check')

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

  // fable_modules holds vendored library output that Fable does not rewrite on
  // every build; comparing against it would mask a stale project build.
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
  console.error(
    `runner: build is current (${freshness.sources} sources, ${freshness.artifacts} artifacts)`,
  )
}

// ── discovery ───────────────────────────────────────────────────────────────

const files = walk(TESTS_ROOT, ['.test.mjs'])

if (files.length === 0) {
  console.error(`runner: no *.test.mjs found under ${TESTS_ROOT}/`)
  process.exit(1)
}

console.error(`runner: ${files.length} test file(s), ${PER_TEST_TIMEOUT_MS}ms per test`)

// ── execution ───────────────────────────────────────────────────────────────

const stream = run({
  files,
  timeout: PER_TEST_TIMEOUT_MS,
  signal: AbortSignal.timeout(SUITE_TIMEOUT_MS),
  concurrency: true,
})

let failed = 0
stream.on('test:fail', () => {
  failed += 1
})

stream.compose(spec).pipe(process.stdout)

// The reporter drains asynchronously; wait for the source stream to finish so a
// nonzero exit is not raced by pending output.
await new Promise((resolve, reject) => {
  stream.on('end', resolve)
  stream.on('error', reject)
})

if (failed > 0) {
  console.error(`\nrunner: ${failed} failing test(s)`)
  process.exit(1)
}
