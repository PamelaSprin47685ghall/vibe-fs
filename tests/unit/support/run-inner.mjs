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

import { run } from 'node:test'
import { spec } from 'node:test/reporters'

import { PER_TEST_TIMEOUT_MS, SUITE_BACKSTOP_MS } from '../../e2e/support/time-budget.js'

const files = process.argv.slice(2).filter((argument) => argument.endsWith('.mjs'))

if (files.length === 0) {
  console.error('run-inner: no test files given')
  process.exit(2)
}

const stream = run({
  files,
  timeout: PER_TEST_TIMEOUT_MS,
  // 兜底 only. The clause permits a wall-clock ceiling and forbids it being the sole or primary
  // criterion; the parent's verdict-silence window is the primary one, and this exists so a child
  // that somehow outlives its supervisor cannot run forever.
  signal: AbortSignal.timeout(SUITE_BACKSTOP_MS),
  concurrency: true,
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
    process.send?.({ type, data: { name: data?.name, file: data?.file, nesting: data?.nesting } })
  })
}

stream.compose(spec).pipe(process.stdout)

// `end` may never arrive — measured, see the header. So this awaits it without treating its absence
// as an error: when it does not come, the parent's silence window is what ends the run.
await new Promise((resolve) => {
  stream.on('end', resolve)
  stream.on('error', resolve)
})

process.send?.({ type: 'inner:drained' })
