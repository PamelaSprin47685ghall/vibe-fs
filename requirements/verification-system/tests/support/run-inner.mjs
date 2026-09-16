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
// It runs node:test files that load the compiled distribution under dist/.

import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { finished } from 'node:stream/promises'

import { run } from 'node:test'
import { createCompactReporter } from './compact-reporter.mjs'
import { createRunState, applyEvent, summarize } from './test-run-state.mjs'

import { parseConcurrency } from '../../../../scripts/lib/concurrency-cap.mjs'

/**
 * 契约：监听 'end'/'error'；
 * end → { drained: true, error: null }；
 * error → send({ type: 'runner:error', data: { name, message, stack } }) 后返回 { drained: false, error }；
 * end/error 竞态先到者胜，不可二次 resolve。
 *
 * @param {{
 *   stream: import('node:stream').Readable | import('node:events').EventEmitter,
 *   send?: (message: any) => void,
 * }} opts
 * @returns {Promise<{ drained: boolean, error: any }>}
 */
export function drainTestStream({ stream, send = (message) => process.send?.(message) }) {
  return new Promise((res) => {
    let settled = false
    stream.on('end', () => {
      if (settled) return
      settled = true
      res({ drained: true, error: null })
    })
    stream.on('error', (err) => {
      if (settled) return
      settled = true
      send?.({
        type: 'runner:error',
        data: {
          name: err?.name ?? 'StreamError',
          message: err?.message ?? String(err),
          stack: err?.stack,
        },
      })
      res({ drained: false, error: err })
    })
  })
}

async function main() {
  // Fatal semantics stay physical in production. The verification child opts out
  // explicitly so tests can inspect the fatal classification and durable aftermath
  // without killing the whole test tier. Production code never infers this from
  // NODE_TEST_CONTEXT or any other Host-owned environment variable.
  const originalHome = process.env.HOME || process.env.USERPROFILE
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
  if (!new Set(['manager', 'orchestrator', 'engineer', 'coder', 'inspector', 'browser', 'inquiry', 'reviewer', 'devops', 'distiller', 'blogger', 'bookkeeper', 'predictor']).has(role)) throw new Error('unexpected managed role: ' + role)
  return { model: 'provider/' + role + '-model', reasoning: 'none' }
}\n`,
    'utf8',
  )
  process.env.HOME = runnerTestHome
  process.env.USERPROFILE = runnerTestHome
  if (originalHome) process.env.DOTNET_CLI_HOME = originalHome

  process.on('exit', () => {
    try { rmSync(runnerTestHome, { recursive: true, force: true }) } catch {}
  })

  const files = process.argv.slice(2).filter((argument) => argument.endsWith('.mjs'))

  if (files.length === 0) {
    console.error('run-inner: no test files given')
    process.exit(2)
  }

  // Default: full in-process parallelism (one dist load). Unit-runner renew probes
  // must force serial slices so wall time exceeds silence (concurrency collapses total).
  // Concurrency probe 待测.
  const concurrency = parseConcurrency(process.env.NODE_TEST_CONCURRENCY)

  const stream = run({
    files,
    concurrency,
  })

  const runState = createRunState()

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
    'test:summary',
  ]) {
    stream.on(type, (data) => {
      applyEvent(runState, { type, data })

      if (type !== 'test:summary') {
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
      }
    })
  }

  const compactReporter = createCompactReporter({ state: runState })
  const composedStream = stream.compose(compactReporter)
  composedStream.pipe(process.stdout)

  const { drained: streamDrained, error: streamError } = await drainTestStream({
    stream,
    send: (message) => process.send?.(message),
  })

  // 等待 reporter stream 完全写完
  try {
    await finished(composedStream)
  } catch {}

  if (streamError) {
    console.error(`run-inner: stream error: ${streamError?.message ?? streamError}`)
    process.exitCode = 1
  }

  if (streamDrained && !streamError) {
    process.send?.({
      type: 'runner:summary',
      data: summarize(runState),
    })
    process.send?.({ type: 'inner:drained' })
  }
}

if (typeof process.argv[1] === 'string' && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await main()
}
