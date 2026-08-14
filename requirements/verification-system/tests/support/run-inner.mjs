// tests/unit/support/run-inner.mjs — the inner tier: node:test itself, semantics unchanged.
//
// Spawned by the out-of-process supervisor (unit / integration / package). Its only added job is to
// report every event to the parent over IPC so the parent's watchdog can be fed by verdicts.
//
// ── what is deliberately NOT changed here ───────────────────────────────────
//
// `run({ timeout, concurrency })` keeps both properties the clause already grants it:
//
//   「超时即遗忘」  a timed-out test is failed and the runner continues; an abandoned test does not
//                  later reject into an unrelated test's result
//   in-process parallelism, which is what makes many files cost one `dist` module load
//
// Measured (Node v26.4.0): `timeout` here is a VERDICT line, not an abort line. A test that
// overruns is failed at the deadline and then keeps running to completion, and a test that never
// resolves while holding a live handle prevents the stream from ever emitting `end`. Neither is
// fixable from inside this process — that is the parent's job, and it is why there is a parent.
//
// Per-test / suite budgets arrive as env overrides on the shared time-budget constants so package
// and integration can use wider windows without forking this file.

import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname } from 'node:path'
import { pathToFileURL } from 'node:url'

import { run } from 'node:test'
import { spec } from 'node:test/reporters'

import { PER_TEST_TIMEOUT_MS, SUITE_BACKSTOP_MS } from '../e2e/support/time-budget.js'

const files = process.argv.slice(2).filter((argument) => argument.endsWith('.mjs'))

if (files.length === 0) {
  console.error('run-inner: no test files given')
  process.exit(2)
}

// ── coverage (NODE_TEST_COVERAGE=1) ─────────────────────────────────────────
//
// node:test's own V8 coverage (`run({ coverage: true })`) measures files that were LOADED.
// Unloaded production modules would be invisible and the "overall" percent would only describe
// the subset the suite happened to import — a number that rises when tests import less. To make
// the totals a true whole-codebase number, every production module (dist minus fable_modules)
// is pre-imported first: a module nobody tests then counts its lines at 0% instead of vanishing.
//
// The summary is written as JSON and the line percent is gated against COVERAGE_LINE_THRESHOLD;
// a run below it exits 1 so the supervising runner fails the suite.

const withCoverage = process.env.NODE_TEST_COVERAGE === '1'
const coverageSummaryPath = process.env.COVERAGE_SUMMARY_PATH
const coverageLineThreshold = withCoverage ? Number(process.env.COVERAGE_LINE_THRESHOLD ?? 80) : null

if (withCoverage) {
  if (!Number.isFinite(coverageLineThreshold) || coverageLineThreshold <= 0) {
    console.error(
      `run-inner: COVERAGE_LINE_THRESHOLD must be a positive finite number, got ${process.env.COVERAGE_LINE_THRESHOLD}`,
    )
    process.exit(2)
  }
  if (!coverageSummaryPath) {
    console.error('run-inner: coverage on but COVERAGE_SUMMARY_PATH unset')
    process.exit(2)
  }

  const { walk } = await import('../../../scripts/lib/walk.mjs')
  const modules = walk('dist', ['.js']).filter((file) => !file.includes('fable_modules'))
  let failures = 0
  for (const file of modules) {
    try {
      await import(pathToFileURL(file).href)
    } catch (error) {
      failures += 1
      console.error(`coverage: pre-import failed ${file}: ${error.message}`)
    }
  }
  // A module that failed to load is counted only up to its failure point — a dishonest denominator.
  // Fail closed rather than report a percent over a partial world.
  if (failures > 0) {
    console.error(`coverage: ${failures}/${modules.length} production modules failed pre-import — aborting`)
    process.exit(1)
  }
  console.error(`coverage: pre-imported ${modules.length} production modules (excluding fable_modules)`)
}

// Default: full in-process parallelism (one dist load). Unit-runner renew probes
// must force serial slices so wall time exceeds silence (concurrency collapses total).
const concurrencyEnv = process.env.NODE_TEST_CONCURRENCY
const concurrency =
  concurrencyEnv === '1' || concurrencyEnv === 'false' ? 1 : concurrencyEnv ? Number(concurrencyEnv) : true

const stream = run({
  files,
  timeout: PER_TEST_TIMEOUT_MS,
  // 兜底 only. The clause permits a wall-clock ceiling and forbids it being the sole or primary
  // criterion; the parent's verdict-silence window is the primary one, and this exists so a child
  // that somehow outlives its supervisor cannot run forever.
  signal: AbortSignal.timeout(SUITE_BACKSTOP_MS),
  concurrency: Number.isFinite(concurrency) && concurrency > 0 ? concurrency : true,
  ...(withCoverage
    ? {
        coverage: true,
        // The report must describe production bytes only: the runner, support files and tests
        // themselves, the Fable runtime (fable_modules), vendored packages and repo tooling
        // (scripts/ — checker scripts some tests import) are noise.
        coverageExcludeGlobs: [
          '**/node_modules/**',
          '**/fable_modules/**',
          '**/tests/**',
          '**/scripts/**',
        ],
      }
    : {}),
})

// Every event, not just verdicts. The classifier in `verdict-feed.mjs` decides what renews; sending
// only the blocking kinds would move that decision into this file and leave the parent unable to
// report background progress in its dump.
for (const type of [
  'test:start',
  'test:pass',
  'test:fail',
  'test:complete',
  'test:diagnostic',
  'test:stderr',
  'test:stdout',
]) {
  stream.on(type, (data) => {
    process.send?.({
      type,
      data: {
        name: data?.name,
        file: data?.file,
        nesting: data?.nesting,
        // Duration rides along with the verdict so the parent can report the tier's timing
        // distribution. One number per verdict, measured by node:test — the alternative was a
        // second timing mechanism in the parent for something already measured here.
        durationMs: data?.details?.duration_ms,
      },
    })
  })
}

stream.compose(spec).pipe(process.stdout)

let coverageSummary = null
if (withCoverage) {
  stream.on('test:coverage', (data) => {
    coverageSummary = data?.summary ?? null
  })
}

// `end` may never arrive — measured, see the header. So this awaits it without treating its absence
// as an error: when it does not come, the parent's silence window is what ends the run.
await new Promise((resolve) => {
  stream.on('end', resolve)
  stream.on('error', resolve)
})

if (withCoverage) {
  const totals = coverageSummary?.totals
  if (totals) {
    mkdirSync(dirname(coverageSummaryPath), { recursive: true })
    writeFileSync(coverageSummaryPath, JSON.stringify(coverageSummary, null, 2))
    const percent = totals.coveredLinePercent
    const ok = percent >= coverageLineThreshold
    console.error(
      `coverage: ${percent.toFixed(2)}% lines (${totals.coveredLineCount}/${totals.totalLineCount}) — ` +
        `threshold ${coverageLineThreshold}% → ${ok ? 'PASS' : 'FAIL'}`,
    )
    if (!ok) process.exitCode = 1
  } else {
    console.error('coverage: no test:coverage event arrived — coverage run broken, failing')
    process.exitCode = 1
  }
}

process.send?.({ type: 'inner:drained' })
