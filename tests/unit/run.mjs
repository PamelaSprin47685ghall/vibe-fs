#!/usr/bin/env node
// tests/unit/runner.mjs — layers 1-3 entry point, and the causal-progress gate over them.
//
// Three responsibilities, in order:
//
//   1. Staleness gate. mjs tests import dist, so a stale build means the suite silently
//      describes yesterday's code. Fail closed rather than emit a green light for bytes nobody
//      asked about.
//   2. Supervise a child that runs node:test, with a silence window fed by test VERDICTS.
//   3. Own the authoritative counts, because the reporter's are wrong (see below).
//
// Discovery is *.test.mjs under tests/unit/. No compile step, no export scanning: node:test owns
// registration.
//
//   node tests/unit/runner.mjs
//   node tests/unit/runner.mjs --skip-staleness-check   (build-tooling work only)
//
// ── what node:test already got right, and must not be regressed ─────────────
//
// VERIFY-004's 「单测运行器：超时即遗忘」 was already satisfied before W4: node:test fails a
// timed-out test, continues to the next, and an abandoned test does not later reject into an
// unrelated test's result. That is why the inner tier is unchanged and the supervisor sits outside
// it rather than replacing it.
//
// ── what it got wrong, and why a second process is the fix ──────────────────
//
// Measured on Node v26.4.0:
//
//   run({ timeout }) on a 3000ms test    verdict at 200ms, run still takes 3074ms wall
//   a test that never resolves while
//   holding a live handle                verdict at 200ms, stream NEVER emits `end`
//
// So the previous version of this file — which delegated to `run({ timeout })` and then awaited
// `stream.on('end')` — both failed AND parked, and the only thing that ended a hung run was the
// 300s suite ceiling. Its header claimed the opposite ("a hung causal wait fails instead of parking
// the suite"); that sentence was the same species of defect as the empty `resetHeartbeat` this
// package was chartered to repair, so it is deleted rather than reworded.
//
// A supervisor in a separate process is not blocked by the child's event loop, so it also ends a
// CPU-bound hang no in-process timer could reach. One extra process buys that; process-per-file
// would have bought the same preemption for 25 module loads.
//
// ── why the feed is a verdict and not an assertion ──────────────────────────
//
// `verdict-feed.mjs` carries that argument. In short: an assertion runs after the function under
// test returns, so feeding it measures 「有字节在动」, and an asserting loop would renew forever.

import { spawn } from 'node:child_process'
import { statSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { Watchdog } from '../e2e/watchdog.js'
import { UNIT_VERDICT_SILENCE_MS } from '../e2e/time-budget.js'
import { walk } from '../../scripts/lib/walk.mjs'
import { classifyVerdict } from './support/verdict-feed.mjs'

const TESTS_ROOT = 'tests/unit'
const PRODUCTION_ROOT = 'src/Wanxiangshu'
const BUILD_ROOT = 'dist'
const INNER = fileURLToPath(new URL('./support/run-inner.mjs', import.meta.url))

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
const files = override ? override.split(',').map((file) => file.trim()).filter(Boolean) : walk(TESTS_ROOT, ['.test.mjs'])

if (override) console.error(`runner: discovery OVERRIDDEN by TESTS_MJS_FILES (${files.length} file(s))`)

if (files.length === 0) {
  console.error(`runner: no *.test.mjs found under ${TESTS_ROOT}/`)
  process.exit(1)
}

console.error(`runner: ${files.length} test file(s), ${UNIT_VERDICT_SILENCE_MS}ms verdict-silence window`)

// ── supervision ─────────────────────────────────────────────────────────────

// Keyed by ABSOLUTE path. Measured while writing the gate cases: `test:complete` reports
// `data.file` absolute, while discovery (and `TESTS_MJS_FILES`) yields repo-relative paths, so a
// Set of the latter never matched and nothing was ever removed. The visible symptom was the wrong
// diagnostic — a leaked handle was reported as an unfinished file — which is worse than no
// diagnostic, because it sends the reader after a test that had in fact completed.
const outstanding = new Set(files.map((file) => resolve(file)))
let passed = 0
let failed = 0
let drained = false

/**
 * Armed BEFORE the child spawns.
 *
 * That closes the startup window on the unit side — VERIFY-004's 「覆盖必须无缝」 — so a child that
 * dies during module load, or never reaches its first verdict, is bounded by the same criterion as
 * one that hangs mid-suite rather than by a separate startup timeout.
 */
const watchdog = new Watchdog({
  timeoutMs: UNIT_VERDICT_SILENCE_MS,
  label: 'tests/unit',
  onTimeout: () => {
    // The verdict-level accounting lives here rather than after the await, because `Watchdog._fire`
    // ends in `process.exit(1)` — correct for a canary, and it means the parent never reaches its
    // own epilogue. So the two causes of silence are distinguished at the point where the knowledge
    // to distinguish them exists.
    if (failed === 0 && outstanding.size === 0) {
      // Every verdict arrived and passed, yet the child could not leave. A green ledger is not a
      // green run, and the previous runner had no way to say so: it awaited `stream.on('end')`,
      // which DOES arrive in this case, so it exited 0 with a handle still open.
      console.error(
        'runner: every verdict passed but the child would not exit — ' +
        'a handle the suite created is still open',
      )
    } else {
      console.error(
        `runner: ${outstanding.size} file(s) had not reported completion: ` +
          `${[...outstanding].map((file) => relative(process.cwd(), file)).join(', ')}`,
      )
    }
    console.error(`runner: ${passed} passed, ${failed} failed before the silence`)
    // SIGKILL the process GROUP. A hung test can be holding a grandchild, and the spike for this
    // design confirmed a group kill reaches even a SIGTERM-ignoring one.
    try {
      process.kill(-child.pid, 'SIGKILL')
    } catch {}
  },
})

const child = spawn(process.execPath, [INNER, ...files], {
  stdio: ['ignore', 'inherit', 'inherit', 'ipc'],
  detached: true,
})

child.on('message', (event) => {
  if (event?.type === 'inner:drained') {
    drained = true
    return
  }
  if (event?.type === 'test:pass') passed += 1
  if (event?.type === 'test:fail') failed += 1
  if (event?.type === 'test:complete' && typeof event?.data?.file === 'string') {
    outstanding.delete(resolve(event.data.file))
  }

  const progress = classifyVerdict(event)
  if (progress !== null) watchdog.advance(progress)
})

const exit = await new Promise((resolve) => {
  child.on('exit', (code, signal) => resolve({ code, signal }))
  child.on('error', (error) => {
    console.error(`runner: could not start the inner runner: ${error.message}`)
    resolve({ code: 1, signal: null })
  })
})

watchdog.stop()

// ── the authoritative summary ───────────────────────────────────────────────
//
// Printed by the parent because the reporter's own line is wrong: measured, `spec` printed
// `ℹ fail 1` for a run whose source stream emitted two `test:fail`. The exit code was still right
// because the counter decides it, not the reporter — but a reader taking the printed number at face
// value would be misled indefinitely. Patching or replacing `spec` is out of scope; it is upstream's
// bug and the honest fix costs one line here.
console.error(
  `\nrunner: ${passed} passed, ${failed} failed (authoritative; the spec reporter undercounts on timeout)`,
)

if (failed > 0) process.exit(1)

// A child that died by signal without the watchdog firing: killed from outside, or crashed hard.
// Either way the verdicts it reported describe an incomplete run, so a green ledger cannot stand.
if (exit.signal !== null) {
  console.error(`runner: the inner runner died by ${exit.signal}; its verdicts describe an incomplete run`)
  process.exit(1)
}

if (exit.code !== 0) {
  console.error(`runner: inner runner exited ${exit.code}`)
  process.exit(exit.code ?? 1)
}

if (!drained) {
  console.error('runner: the inner runner exited without draining its result stream')
  process.exit(1)
}
