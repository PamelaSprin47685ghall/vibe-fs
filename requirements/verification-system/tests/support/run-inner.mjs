// tests/unit/support/run-inner.mjs — the inner tier: node:test itself, semantics unchanged.
//
// Spawned by the out-of-process supervisor (unit / integration / package). Its only added job is to
// report every event to the parent over IPC so the parent's watchdog can be fed by verdicts.
//
// ── what is deliberately NOT changed here ───────────────────────────────────
//
// `run({ concurrency })` preserves process isolation without confusing two scopes:
//
//   explicit leaf timeout  a leaf that declares a timeout is failed and forgotten
//   file process           may contain many healthy leaves and has no leaf-sized total budget
//   process parallelism    keeps the full suite efficient
//
// Measured (Node v26.4.0): an explicit leaf `timeout` is a VERDICT line, not an abort line. A test that
// overruns is failed at the deadline and then keeps running to completion, and a test that never
// resolves while holding a live handle prevents the stream from ever emitting `end`. Neither is
// fixable from inside this process — that is the parent's job, and it is why there is a parent.
//
// The external supervisor owns verdict silence and the suite backstop. Putting either a leaf timeout
// or one shared AbortSignal here applies it to every process-isolated FILE wrapper under Node 20,
// which kills healthy multi-test files and fans hundreds of listeners out from one signal.

import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { tmpdir } from 'node:os'
import { pathToFileURL } from 'node:url'

import { run } from 'node:test'
import { spec } from 'node:test/reporters'

import {
  COVERAGE_EXCLUDE_GLOBS,
  parseCoverageThreshold,
  selectProductionModules,
  preImportModules,
  evaluateCoverage,
} from './coverage-policy.mjs'

// Fatal semantics stay physical in production. The verification child opts out
// explicitly so tests can inspect the fatal classification and durable aftermath
// without killing the whole test tier. Production code never infers this from
// NODE_TEST_CONTEXT or any other Host-owned environment variable.
process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

// Isolate HOME / USERPROFILE for the node:test inner runner so any test or
// pre-import that touches ~/.config/opencode defaults to a throwaway temporary
// directory rather than the developer's real user configuration directory.
// Keep the .NET CLI tool store independent: compiler canaries must still resolve
// the repository-pinned local Fable tool after application HOME is isolated.
process.env.DOTNET_CLI_HOME ??= process.env.HOME
const runnerTestHome = mkdtempSync(join(tmpdir(), 'wxs-runner-home-'))
const runnerRoutingDir = join(runnerTestHome, '.config', 'opencode')
mkdirSync(runnerRoutingDir, { recursive: true })
writeFileSync(
  join(runnerRoutingDir, 'wanxiangshu.mjs'),
  `export default function route(role, running) {
  if (!/^(fast|deep)-/.test(role)) throw new Error('unexpected managed role: ' + role)
  return { model: 'provider/' + role + '-model', reasoning: 'none' }
}\n`,
  'utf8',
)
process.env.HOME = runnerTestHome
process.env.USERPROFILE = runnerTestHome

process.on('exit', () => {
  try { rmSync(runnerTestHome, { recursive: true, force: true }) } catch {}
})

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
let coverageLineThreshold = null

if (withCoverage) {
  try {
    coverageLineThreshold = parseCoverageThreshold(process.env.COVERAGE_LINE_THRESHOLD)
  } catch (error) {
    console.error(`run-inner: ${error.message}`)
    process.exit(2)
  }
  if (!coverageSummaryPath) {
    console.error('run-inner: coverage on but COVERAGE_SUMMARY_PATH unset')
    process.exit(2)
  }

  const { walk } = await import('../../../scripts/lib/walk.mjs')
  const modules = selectProductionModules(walk('dist', ['.js']))
  const preImport = await preImportModules(modules, (file) => import(pathToFileURL(file).href))
  for (const { file, message } of preImport.failedFiles) {
    console.error(`coverage: pre-import failed ${file}: ${message}`)
  }
  // A module that failed to load is counted only up to its failure point — a dishonest denominator.
  // Fail closed rather than report a percent over a partial world.
  if (preImport.failures > 0) {
    console.error(
      `coverage: ${preImport.failures}/${preImport.total} production modules failed pre-import — aborting`,
    )
    process.exit(1)
  }
  console.error(`coverage: pre-imported ${preImport.total} production modules (excluding fable_modules)`)
}

// Default: full in-process parallelism (one dist load). Unit-runner renew probes
// must force serial slices so wall time exceeds silence (concurrency collapses total).
const concurrencyEnv = process.env.NODE_TEST_CONCURRENCY
const concurrency =
  concurrencyEnv === '1' || concurrencyEnv === 'false' ? 1 : concurrencyEnv ? Number(concurrencyEnv) : true

const stream = run({
  files,
  concurrency: Number.isFinite(concurrency) && concurrency > 0 ? concurrency : true,
  ...(withCoverage
    ? {
        coverage: true,
        // The report must describe production bytes only: the runner, support files and tests
        // themselves, the Fable runtime (fable_modules), vendored packages and repo tooling
        // (scripts/ — checker scripts some tests import) are noise.
        coverageExcludeGlobs: COVERAGE_EXCLUDE_GLOBS,
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
  const result = evaluateCoverage(coverageSummary, coverageLineThreshold)
  if (result.totals) {
    mkdirSync(dirname(coverageSummaryPath), { recursive: true })
    writeFileSync(coverageSummaryPath, JSON.stringify(coverageSummary, null, 2))
    const { percent, totals, ok } = result
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
