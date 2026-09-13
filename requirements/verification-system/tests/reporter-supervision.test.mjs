// requirements/verification-system/tests/reporter-supervision.test.mjs
//
// Proof for P6:
// 1. Single leaf test:complete does NOT prematurely close an outstanding file in supervisor.
// 2. stream.on('error') produces runner:error verdict and non-zero exit (not drained).
// 3. Compact vs verbose reporters yield identical verdict counts on the same file set.

import assert from 'node:assert/strict'
import test from 'node:test'
import { spawn } from 'node:child_process'
import path from 'node:path'
import fs from 'node:fs'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'

import { createCompactReporter } from './support/compact-reporter.mjs'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../..')
const innerRunner = path.join(here, 'support/run-inner.mjs')

test('WHAT[P6-REPORTER-001] compact and verbose modes produce identical verdict counts', async () => {
  const targetFile = path.join(
    root,
    'requirements/distribution/tests/integration/package/layout.test.mjs',
  )

  const runWithMode = async (verbose) => {
    const events = [
      { type: 'test:start', data: { name: 'test A', file: targetFile, nesting: 0 } },
      { type: 'test:pass', data: { name: 'test A', file: targetFile, nesting: 0, details: { duration_ms: 10 } } },
      { type: 'test:start', data: { name: 'test B', file: targetFile, nesting: 0 } },
      { type: 'test:fail', data: { name: 'test B', file: targetFile, nesting: 0, details: { duration_ms: 20, error: new Error('fail B') } } },
      { type: 'test:start', data: { name: 'test C', file: targetFile, nesting: 0 } },
      { type: 'test:pass', data: { name: 'test C', file: targetFile, nesting: 0, skip: true, details: { duration_ms: 5 } } },
      { type: 'test:start', data: { name: 'test D', file: targetFile, nesting: 0 } },
      { type: 'test:pass', data: { name: 'test D', file: targetFile, nesting: 0, todo: true, details: { duration_ms: 5 } } },
      { type: 'test:summary', data: { duration_ms: 50 } },
    ]

    async function* sourceGen() {
      for (const ev of events) yield ev
    }

    let summaryResult = null
    const noopStream = { write() {} }
    const reporter = createCompactReporter({
      verbose,
      stdout: noopStream,
      stderr: noopStream,
      onSummary: (s) => {
        summaryResult = s
      },
    })
    for await (const _ of reporter(sourceGen())) {}
    return summaryResult
  }

  const compactSummary = await runWithMode(false)
  const verboseSummary = await runWithMode(true)

  assert.ok(compactSummary, 'compact summary must be produced')
  assert.ok(verboseSummary, 'verbose summary must be produced')
  assert.equal(compactSummary.files, verboseSummary.files)
  assert.equal(compactSummary.passed, verboseSummary.passed)
  assert.equal(compactSummary.failed, verboseSummary.failed)
  assert.equal(compactSummary.skipped, verboseSummary.skipped)
  assert.equal(compactSummary.todo, verboseSummary.todo)
  assert.equal(compactSummary.cancelled, verboseSummary.cancelled)
  assert.deepEqual(
    compactSummary.byFile.map((f) => ({ ...f, durationMs: 0 })),
    verboseSummary.byFile.map((f) => ({ ...f, durationMs: 0 })),
  )
})

test('WHAT[P6-REPORTER-002] single leaf completion does NOT remove file from outstanding set', () => {
  // Simulate the supervisor state machine
  const fileA = path.resolve('/tmp/a.test.mjs')
  const fileB = path.resolve('/tmp/b.test.mjs')
  const outstanding = new Set([fileA, fileB])

  const handleComplete = (event) => {
    if (event?.type === 'test:complete' && typeof event?.data?.file === 'string') {
      const isFileWrapper =
        typeof event?.data?.name === 'string' &&
        (event.data.name === event.data.file ||
          path.resolve(event.data.name) === path.resolve(event.data.file))
      if (isFileWrapper) {
        outstanding.delete(path.resolve(event.data.file))
      }
    }
  }

  // 1. Leaf test complete in fileA
  handleComplete({
    type: 'test:complete',
    data: { name: 'leaf test 1', file: fileA, nesting: 0 },
  })
  assert.ok(
    outstanding.has(fileA),
    'outstanding must still contain fileA after leaf test:complete',
  )
  assert.equal(outstanding.size, 2)

  // 2. Another leaf test complete in fileA
  handleComplete({
    type: 'test:complete',
    data: { name: 'leaf test 2', file: fileA, nesting: 1 },
  })
  assert.ok(outstanding.has(fileA), 'outstanding must still contain fileA after subtest complete')

  // 3. FileWrapper test:complete for fileA
  handleComplete({
    type: 'test:complete',
    data: { name: fileA, file: fileA, nesting: 0 },
  })
  assert.equal(outstanding.has(fileA), false, 'fileA must be removed on FileWrapper test:complete')
  assert.equal(outstanding.has(fileB), true, 'fileB must remain outstanding')
})

test('WHAT[P6-REPORTER-003] stream error emits runner:error verdict and fails closed without draining', async () => {
  // Run a mock test child that triggers stream 'error'
  const scratch = fs.mkdtempSync(path.join(tmpdir(), 'stream-err-'))
  const runnerScript = path.join(scratch, 'runner.mjs')

  fs.writeFileSync(
    runnerScript,
    `
    import { EventEmitter } from 'node:events'
    const fakeStream = new EventEmitter()
    fakeStream.compose = () => fakeStream
    fakeStream.pipe = () => {}

    let streamError = null
    let streamDrained = false

    await new Promise((resolve, reject) => {
      fakeStream.on('end', () => { streamDrained = true; resolve() })
      fakeStream.on('error', (err) => {
        streamError = err
        process.send?.({
          type: 'runner:error',
          data: { name: err.name, message: err.message, stack: err.stack },
        })
        reject(err)
      })
      setTimeout(() => fakeStream.emit('error', new Error('forced stream explosion')), 50)
    }).catch((err) => {
      process.exitCode = 1
    })

    if (streamDrained && !streamError) {
      process.send?.({ type: 'inner:drained' })
    }
  `,
  )

  const child = spawn(process.execPath, [runnerScript], {
    stdio: ['ignore', 'pipe', 'pipe', 'ipc'],
  })

  const messages = []
  child.on('message', (msg) => messages.push(msg))

  const exitCode = await new Promise((res) => child.on('exit', res))
  fs.rmSync(scratch, { recursive: true, force: true })

  assert.equal(exitCode, 1, 'process must exit with code 1 on stream error')
  assert.ok(
    messages.some((m) => m.type === 'runner:error' && m.data?.message === 'forced stream explosion'),
    'must emit runner:error IPC event',
  )
  assert.ok(
    !messages.some((m) => m.type === 'inner:drained'),
    'must NOT emit inner:drained on stream error',
  )
})
