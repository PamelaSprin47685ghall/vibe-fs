// requirements/verification-system/tests/reporter-supervision.test.mjs
//
// Proof for P6:
// 1. Compact vs verbose reporters yield identical verdict counts and match exact expected counts.
// 2. Leaf test:complete does NOT remove file from supervisor outstanding set; file wrapper does.
//    (a) Table of synthetic events tested against isFileCompletionEvent (including suffix non-ambiguity).
//    (b) Real child (run-inner.mjs) running two-leaf fixture with IPC messages verifying arrival order and completion predicate.
// 3. drainTestStream contract: error -> runner:error, not drained; end -> drained:true.
//    Real run-inner child normal execution delivers inner:drained at the end of the chain.

import assert from 'node:assert/strict'
import test from 'node:test'
import { spawn } from 'node:child_process'
import path from 'node:path'
import { PassThrough } from 'node:stream'
import { fileURLToPath } from 'node:url'

import { createCompactReporter } from './support/compact-reporter.mjs'
import { isFileCompletionEvent } from './support/test-run-state.mjs'
import { drainTestStream } from './support/run-inner.mjs'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(here, '../../..')
const innerRunner = path.join(here, 'support/run-inner.mjs')

function cleanEnv() {
  const env = { ...process.env }
  delete env.NODE_TEST_CONTEXT
  return env
}

test('WHAT[P6-REPORTER-001] compact and verbose modes produce identical verdict counts matching exact expectation', async () => {
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
    let stderrText = ''
    const noopStream = { write() {} }
    const errStream = {
      write(chunk) {
        stderrText += chunk
      },
    }
    const reporter = createCompactReporter({
      verbose,
      stdout: noopStream,
      stderr: errStream,
      onSummary: (s) => {
        summaryResult = s
      },
    })
    for await (const _ of reporter(sourceGen())) {}
    return { summaryResult, stderrText }
  }

  const compact = await runWithMode(false)
  const verbose = await runWithMode(true)

  const expected = {
    files: 1,
    passed: 1,
    failed: 1,
    skipped: 1,
    todo: 1,
    cancelled: 0,
  }

  for (const [key, val] of Object.entries(expected)) {
    assert.equal(compact.summaryResult[key], val, `compact summary.${key} must match expected`)
    assert.equal(verbose.summaryResult[key], val, `verbose summary.${key} must match expected`)
  }

  assert.deepEqual(
    compact.summaryResult.byFile.map((f) => ({ ...f, durationMs: 0 })),
    verbose.summaryResult.byFile.map((f) => ({ ...f, durationMs: 0 })),
  )

  // Failures detail in stderr contains test B name
  assert.match(compact.stderrText, /test B/)
  assert.match(verbose.stderrText, /test B/)
})

test('WHAT[P6-REPORTER-002] single leaf completion does NOT remove file from outstanding set', async () => {
  // (a) Truth table against isFileCompletionEvent
  const fileA = path.resolve('/tmp/a.test.mjs')
  const fileB = path.resolve('/tmp/nested/b.test.mjs')

  // leaf complete -> false
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: 'leaf 1', file: fileA, nesting: 0 },
    }),
    false,
  )
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: 'leaf 2', file: fileA, nesting: 1 },
    }),
    false,
  )

  // wrapper (name is absolute path equal to file) -> true
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: fileA, file: fileA, nesting: 0 },
    }),
    true,
  )

  // style variant: relative path whose resolve() matches file -> true
  const relFileA = path.relative(process.cwd(), fileA)
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: relFileA, file: fileA, nesting: 0 },
    }),
    true,
  )

  // Leaf name happens to equal file basename/suffix (e.g. name='b.test.mjs', file='/tmp/nested/b.test.mjs')
  // resolve('b.test.mjs') resolves to <cwd>/b.test.mjs which is not /tmp/nested/b.test.mjs -> false.
  // This guards against suffix-ambiguity regression where endsWith or basename is misused.
  assert.equal(
    isFileCompletionEvent({
      type: 'test:complete',
      data: { name: 'b.test.mjs', file: fileB, nesting: 0 },
    }),
    false,
  )

  // (b) Real child running two-leaf.fixture.mjs via IPC
  const fixturePath = path.join(here, 'support/fixtures/two-leaf.fixture.mjs')
  const child = spawn(process.execPath, [innerRunner, fixturePath], {
    stdio: ['ignore', 'ignore', 'ignore', 'ipc'],
    env: cleanEnv(),
  })

  const completes = []
  child.on('message', (msg) => {
    if (msg?.type === 'test:complete') {
      completes.push(msg)
    }
  })

  const exitCode = await new Promise((res) => child.on('exit', res))
  assert.equal(exitCode, 0, 'child must exit 0')

  assert.ok(completes.length >= 3, `must receive at least 3 completes (2 leaves + 1 wrapper), got ${completes.length}`)

  const leafCompletes = completes.slice(0, -1)
  const lastComplete = completes[completes.length - 1]

  for (const leaf of leafCompletes) {
    assert.equal(
      isFileCompletionEvent(leaf),
      false,
      `leaf complete for '${leaf.data?.name}' must not be a file completion event`,
    )
  }

  assert.equal(
    isFileCompletionEvent(lastComplete),
    true,
    'last complete event must be a file completion event',
  )

  // Replay against outstanding set in order of arrival
  const resolvedFixture = path.resolve(fixturePath)
  const outstanding = new Set([resolvedFixture])

  for (let i = 0; i < completes.length; i++) {
    const event = completes[i]
    if (isFileCompletionEvent(event)) {
      outstanding.delete(path.resolve(event.data.file))
    }
    if (i < completes.length - 1) {
      assert.ok(
        outstanding.has(resolvedFixture),
        `file must remain in outstanding before the final wrapper complete (step ${i})`,
      )
    }
  }

  assert.equal(outstanding.size, 0, 'file must be removed from outstanding after the final wrapper complete')
})

test('WHAT[P6-REPORTER-003] drainTestStream contract and inner:drained event delivery', async () => {
  // 1. Error branch: PassThrough destroyed with error
  const sendMessages = []
  const errorStream = new PassThrough()
  const drainErrorPromise = drainTestStream({
    stream: errorStream,
    send: (msg) => sendMessages.push(msg),
  })
  errorStream.destroy(new Error('forced stream explosion'))
  const errorResult = await drainErrorPromise

  assert.equal(errorResult.drained, false, 'must return drained:false on stream error')
  assert.ok(errorResult.error instanceof Error, 'must return error object')
  assert.equal(errorResult.error.message, 'forced stream explosion')
  assert.equal(sendMessages.length, 1, 'must send exactly one runner:error IPC message')
  assert.equal(sendMessages[0].type, 'runner:error')
  assert.equal(sendMessages[0].data?.message, 'forced stream explosion')
  assert.ok(
    !sendMessages.some((m) => m.type === 'inner:drained'),
    'must not send inner:drained on error',
  )

  // 2. End branch: PassThrough ended cleanly
  const endMessages = []
  const endStream = new PassThrough()
  endStream.resume() // Flowing mode so 'end' emits upon end()
  const drainEndPromise = drainTestStream({
    stream: endStream,
    send: (msg) => endMessages.push(msg),
  })
  endStream.end()
  const endResult = await drainEndPromise

  assert.equal(endResult.drained, true, 'must return drained:true on clean end')
  assert.equal(endResult.error, null, 'must return error:null on clean end')
  assert.equal(endMessages.length, 0, 'must not send error on clean end')

  // 3. Real child spawn: run-inner with fixture completes cleanly and delivers inner:drained
  const fixturePath = path.join(here, 'support/fixtures/all-pass.fixture.mjs')
  const child = spawn(process.execPath, [innerRunner, fixturePath], {
    stdio: ['ignore', 'ignore', 'ignore', 'ipc'],
    env: cleanEnv(),
  })

  const childMessages = []
  child.on('message', (msg) => childMessages.push(msg))

  const exitCode = await new Promise((res) => child.on('exit', res))
  assert.equal(exitCode, 0, 'child must exit 0 on clean run')
  assert.ok(
    childMessages.some((m) => m.type === 'inner:drained'),
    'must receive inner:drained event from child upon successful stream completion',
  )
})
