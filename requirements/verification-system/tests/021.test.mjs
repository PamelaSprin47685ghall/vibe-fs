import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { once } from 'node:events'
import { existsSync, mkdtempSync, readFileSync, realpathSync, rmSync, symlinkSync, writeFileSync } from 'node:fs'
import { availableParallelism, tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { PassThrough } from 'node:stream'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { createCompactReporter } from './support/compact-reporter.mjs'
import { createRunState, isFileCompletionEvent } from './support/test-run-state.mjs'
import { bindTestEntry, drainTestStream } from './support/run-inner.mjs'
import { classifyVerdict } from './support/verdict-feed.mjs'
import { NODE_TEST_INNER, superviseNodeTest } from './e2e/support/supervise-node-test.mjs'

const fixture = fileURLToPath(new URL('./support/fixtures/two-leaf.fixture.mjs', import.meta.url))
const childEnv = { ...process.env }
delete childEnv.NODE_TEST_CONTEXT
const verdict = (name, type = 'test:pass', extra = {}) => ({
  type,
  data: { name, file: '/test/a.mjs', details: { duration_ms: 1 }, ...extra },
})
const counts = (s) => ({
  passed: s.passed, failed: s.failed, skipped: s.skipped, todo: s.todo, cancelled: s.cancelled,
})

test('WHAT[verification-system-021] compact and verbose reports preserve outcomes and visible exclusions', async () => {
  const events = [
    verdict('pass'), verdict('fail', 'test:fail'),
    verdict('skip', 'test:pass', { skip: 'requires release environment' }),
    verdict('todo', 'test:pass', { todo: 'known proof gap' }),
    { type: 'test:summary', data: { duration_ms: 4 } },
  ]
  const reports = []
  for (const verbose of [false, true]) {
    let output = ''
    const sink = { write(text) { output += text } }
    const reporter = createCompactReporter({
      verbose, stdout: sink, stderr: sink, onSummary(summary) { reports.push(summary) },
    })
    for await (const _ of reporter(events)) {}
    assert.match(output, /1 passed, 1 failed, 1 skipped, 1 todo/)
    assert.match(output, /failing tests/)
    assert.match(output, /requires release environment/)
    assert.match(output, /known proof gap/)
  }
  assert.deepEqual(reports[0], reports[1])
  assert.deepEqual(counts(reports[0]), { passed: 1, failed: 1, skipped: 1, todo: 1, cancelled: 0 })
})

test('WHAT[verification-system-021] cancelled and TODO failures are not ordinary assertion verdicts', () => {
  const state = createRunState()
  state.applyEvent(verdict('cancelled', 'test:fail', {
    details: { error: { failureType: 'testAborted', message: 'parent cancelled' } },
  }))
  state.applyEvent(verdict('known gap', 'test:fail', { todo: 'missing continuous input guard' }))
  assert.deepEqual(counts(state.summarize()), { passed: 0, failed: 0, skipped: 0, todo: 1, cancelled: 1 })
})

test('WHAT[verification-system-021] conflicting final results cannot erase a previous failure', () => {
  const state = createRunState()
  const failure = verdict('same execution', 'test:fail')
  state.applyEvent(failure)
  state.applyEvent(failure)
  assert.equal(state.summarize().failed, 1)
  assert.throws(() => state.applyEvent(verdict('same execution')), /conflicting.*verdict/i)
  assert.deepEqual(counts(state.summarize()), { passed: 0, failed: 1, skipped: 0, todo: 0, cancelled: 0 })
})

test('WHAT[verification-system-021] equal names in different files or distinct subtests do not collapse', () => {
  const state = createRunState()
  state.applyEvent(verdict('same', 'test:pass', { testId: 1 }))
  state.applyEvent(verdict('same', 'test:pass', { testId: 2 }))
  state.applyEvent(verdict('same', 'test:fail', { file: '/test/b.mjs', testId: 1 }))
  const summary = state.summarize()
  assert.deepEqual(counts(summary), { passed: 2, failed: 1, skipped: 0, todo: 0, cancelled: 0 })
  assert.equal(summary.files, 2)
  assert.equal(summary.leafDurations.length, 3)
  assert.deepEqual(summary.failures.map(({ name, file }) => ({ name, file })), [{ name: 'same', file: '/test/b.mjs' }])
})

test('WHAT[verification-system-021] runtime entry identity separates shared registration sites while duplicate verdicts remain idempotent', () => {
  const state = createRunState()
  const pass = verdict('same', 'test:pass', { file: '/support/gate.mjs', entryFile: '/test/a.mjs', testId: 1 })
  const fail = verdict('same', 'test:fail', { file: '/support/gate.mjs', entryFile: '/test/b.mjs', testId: 1 })
  state.applyEvent(pass)
  state.applyEvent(pass)
  state.applyEvent(fail)
  state.applyEvent(fail)
  assert.deepEqual(counts(state.summarize()), { passed: 1, failed: 1, skipped: 0, todo: 0, cancelled: 0 })
  assert.equal(state.summarize().files, 2)
  assert.throws(() => state.applyEvent({ ...fail, type: 'test:pass' }), /conflicting.*verdict/i)
})

test('WHAT[verification-system-021] legacy runtime event identity preserves identical names and rejects contradictory reuse', () => {
  const state = createRunState()
  const first = verdict('same name', 'test:pass', { entryFile: '/test/a.mjs', testNumber: 1 })
  const second = verdict('same name', 'test:fail', { entryFile: '/test/a.mjs', testNumber: 1 })
  state.applyEvent(first)
  state.applyEvent(first)
  state.applyEvent(second)
  state.applyEvent(second)
  assert.deepEqual(counts(state.summarize()), { passed: 1, failed: 1, skipped: 0, todo: 0, cancelled: 0 })
  assert.throws(() => state.applyEvent({ ...second, type: 'test:pass' }), /conflicting.*verdict/i)
})

test('WHAT[verification-system-021] entry binding accepts real aliases and rejects contradictory or malformed runtime ownership', () => {
  const directory = mkdtempSync(join(tmpdir(), 'test-entry-binding-'))
  try {
    const source = join(directory, 'source.mjs')
    const alias = join(directory, 'alias.mjs')
    const other = join(directory, 'other.mjs')
    writeFileSync(source, '')
    writeFileSync(other, '')
    symlinkSync(source, alias)
    const event = verdict('same', 'test:pass', { entryFile: realpathSync(source) })
    const bound = bindTestEntry(event, alias)
    assert.equal(bound.data.entryFile, alias)
    assert.equal(bound.data.file, event.data.file)
    assert.equal(event.data.entryFile, realpathSync(source))
    assert.equal(isFileCompletionEvent(bindTestEntry({ type: 'test:complete', data: {
      name: realpathSync(source), file: realpathSync(source), entryFile: realpathSync(source),
    } }, alias)), true)
    assert.equal(bindTestEntry(verdict('legacy'), source).data.entryFile, source)
    assert.throws(() => bindTestEntry(event, other), /entry mismatch/)
    assert.throws(() => bindTestEntry(event, join(directory, 'missing.mjs')), { code: 'ENOENT' })
    for (const entryFile of [null, 42, {}, '']) {
      assert.throws(() => bindTestEntry(verdict('invalid', 'test:pass', { entryFile }), source), /entryFile/)
    }
    for (const data of [null, [], 'invalid']) {
      assert.throws(() => bindTestEntry({ type: 'test:pass', data }, source), /data must be an object/)
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] a failing suite preserves its container failure without double counting leaves', () => {
  const state = createRunState()
  state.applyEvent(verdict('pass'))
  state.applyEvent(verdict('fail', 'test:fail'))
  state.applyEvent(verdict('suite', 'test:fail', { details: { type: 'suite' } }))
  const summary = state.summarize()
  assert.deepEqual(counts(summary), { passed: 1, failed: 1, skipped: 0, todo: 0, cancelled: 0 })
  assert.equal(summary.containerFailures, 1)
  assert.deepEqual(summary.failures.map(({ name }) => name), ['fail'])
})

test('WHAT[verification-system-021] an outer file failure and inner suite failure do not share a runtime identity', () => {
  const state = createRunState()
  const suite = verdict('suite', 'test:fail', { testId: 1, details: { type: 'suite' } })
  const file = verdict('/test/a.mjs', 'test:fail', { testId: 1 })
  for (const event of [suite, file, suite, file]) state.applyEvent(event)
  assert.equal(state.summarize().containerFailures, 2)
  assert.equal(state.summarize().failed, 0)
})

test('WHAT[verification-system-021] a leaf completion cannot stand for completion of its file', () => {
  const complete = (name, file = fixture) => ({ type: 'test:complete', data: { name, file } })
  assert.equal(isFileCompletionEvent(complete('leaf')), false)
  assert.equal(isFileCompletionEvent(complete('nested leaf')), false)
  assert.equal(isFileCompletionEvent(complete(fixture)), true)
  assert.equal(isFileCompletionEvent(complete('./a.mjs', resolve('a.mjs'))), true)
  assert.equal(isFileCompletionEvent(complete('a.mjs', '/elsewhere/a.mjs')), false)
})

test('WHAT[verification-system-021] stream error and clean end have distinct completion evidence', async () => {
  const broken = new PassThrough({ objectMode: true })
  const messages = []
  const error = new Error('truncated result stream')
  const waiting = drainTestStream({ stream: broken, send(message) { messages.push(message) } })
  broken.destroy(error)
  assert.deepEqual(await waiting, { drained: false, error })
  assert.deepEqual(messages.map(({ type }) => type), ['runner:error'])
  assert.equal(messages[0].data.message, error.message)

  const clean = new PassThrough({ objectMode: true })
  clean.resume()
  const ended = drainTestStream({ stream: clean, send() { assert.fail('clean end must not emit an error') } })
  clean.end()
  assert.deepEqual(await ended, { drained: true, error: null })
})

test('WHAT[verification-system-021] a stream closed before end is inconclusive and reports its error once', async () => {
  const stream = new PassThrough({ objectMode: true })
  const messages = []
  let result = null
  drainTestStream({ stream, send(message) { messages.push(message) } }).then((value) => { result = value })
  const closed = once(stream, 'close')
  stream.destroy()
  await closed
  assert.equal(result?.drained, false)
  assert.match(result.error.message, /closed before end/)
  assert.deepEqual(messages.map(({ type }) => type), ['runner:error'])
})

test('WHAT[verification-system-021] a real inner runner completes all leaves before file completion and summary', async () => {
  const child = spawn(process.execPath, [NODE_TEST_INNER, fixture], {
    stdio: ['ignore', 'pipe', 'pipe', 'ipc'],
    env: childEnv,
  })
  const messages = []
  let stderr = ''
  child.stdout.resume()
  child.stderr.on('data', (text) => { stderr += text })
  child.on('message', (message) => messages.push(message))
  const exit = await new Promise((resolveExit, reject) => {
    child.on('error', reject)
    child.on('close', (code, signal) => resolveExit({ code, signal }))
  })
  assert.deepEqual(exit, { code: 0, signal: null }, stderr)
  const completions = messages.filter(({ type }) => type === 'test:complete')
  assert.equal(completions.length, 3)
  assert.deepEqual(completions.map(isFileCompletionEvent), [false, false, true])
  const summaryIndex = messages.findIndex(({ type }) => type === 'runner:summary')
  const drainedIndex = messages.findIndex(({ type }) => type === 'inner:drained')
  assert.ok(summaryIndex > messages.indexOf(completions.at(-1)))
  assert.ok(drainedIndex > summaryIndex)
  assert.deepEqual(counts(messages[summaryIndex].data), { passed: 2, failed: 0, skipped: 0, todo: 0, cancelled: 0 })
  assert.equal(messages[summaryIndex].data.filesCompleted, 1)
})

test('WHAT[verification-system-021] summary and clean exit cannot disguise missing file completion', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'incomplete-test-run-'))
  try {
    const inner = join(dir, 'inner.mjs')
    writeFileSync(inner, `process.send({type:'runner:summary',data:{passed:1,failed:0,leafDurations:[]}})
process.send({type:'inner:drained'})
`)
    await assert.rejects(superviseNodeTest({
      files: [fixture], inner, env: childEnv, label: 'incomplete-fixture', silenceMs: 10000, throwOnFailure: true,
    }), /supervised suite failed/)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] a clean child exit without authoritative summary is inconclusive', async () => {
  const messages = []
  const originalError = console.error
  console.error = (...args) => {
    messages.push(args.join(' '))
    originalError(...args)
  }
  try {
    await assert.rejects(superviseNodeTest({
      files: [fixture], inner: fixture, env: childEnv, label: 'no-summary-fixture', silenceMs: 10000, throwOnFailure: true,
    }), /supervised suite failed/)
  } finally {
    console.error = originalError
  }
  assert.match(messages.join('\n'), /verdict counts unavailable; no authoritative summary/)
  assert.doesNotMatch(messages.join('\n'), /\b0 passed, 0 failed\b/)
})

test('WHAT[verification-system-021] a tier exclusion identifies the missing execution tier', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'tier-report-'))
  try {
    const file = join(dir, 'tier.fixture.mjs')
    const gate = new URL('./support/tier-gate.mjs', import.meta.url).href
    writeFileSync(file, `import { releaseTest } from ${JSON.stringify(gate)};
releaseTest('physical acceptance', () => { throw new Error('must remain unexecuted') });
`)
    const child = spawn(process.execPath, [NODE_TEST_INNER, file], {
      stdio: ['ignore', 'pipe', 'pipe', 'ipc'], env: { ...childEnv, WXS_TIER_RELEASE: '0' },
    })
    const messages = []
    child.stdout.resume()
    child.stderr.resume()
    child.on('message', (message) => messages.push(message))
    const code = await new Promise((resolveExit, reject) => {
      child.on('error', reject)
      child.on('close', resolveExit)
    })
    assert.equal(code, 0)
    const summary = messages.find(({ type }) => type === 'runner:summary').data
    assert.deepEqual(counts(summary), { passed: 0, failed: 0, skipped: 1, todo: 0, cancelled: 0 })
    assert.match(summary.exclusions[0].reason, /release.*not enabled/)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] shared tier registration preserves each entry file, verdict and failure location', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'shared-tier-entry-'))
  try {
    const passing = join(directory, 'passing.fixture.mjs')
    const failing = join(directory, 'failing.fixture.mjs')
    const gate = new URL('./support/tier-gate.mjs', import.meta.url)
    writeFileSync(passing, `import { integrationTest } from ${JSON.stringify(gate.href)};
integrationTest('same first test', () => {});
integrationTest('same second test', () => {});
`)
    writeFileSync(failing, `import { integrationTest } from ${JSON.stringify(gate.href)};
integrationTest('same first test', () => {});
integrationTest('same second test', () => { throw new Error('controlled shared-wrapper failure'); });
`)
    const child = spawn(process.execPath, [NODE_TEST_INNER, passing, failing], {
      stdio: ['ignore', 'pipe', 'pipe', 'ipc'],
      env: { ...childEnv, WXS_TIER_INTEGRATION: '1' },
    })
    const messages = []
    let stdout = ''
    let stderr = ''
    child.stdout.on('data', (text) => { stdout += text })
    child.stderr.on('data', (text) => { stderr += text })
    child.on('message', (message) => messages.push(message))
    const exit = await new Promise((resolveExit, reject) => {
      child.on('error', reject)
      child.on('close', (code, signal) => resolveExit({ code, signal }))
    })
    const summary = messages.find(({ type }) => type === 'runner:summary')?.data
    assert.ok(summary, JSON.stringify({ exit, messages, stdout, stderr }))
    assert.deepEqual(counts(summary), { passed: 3, failed: 1, skipped: 0, todo: 0, cancelled: 0 })
    assert.equal(summary.files, 2)
    assert.equal(summary.filesCompleted, 2)
    assert.equal(summary.containerFailures, 0)
    assert.equal(messages.filter(({ type }) => type === 'runner:summary').length, 1)
    assert.equal(messages.filter(({ type }) => type === 'inner:drained').length, 1)
    const completionIndices = messages.flatMap((message, index) => isFileCompletionEvent(message) ? [index] : [])
    assert.equal(completionIndices.length, 2)
    assert.ok(messages.findIndex(({ type }) => type === 'runner:summary') > Math.max(...completionIndices))
    assert.ok(summary.wallMs >= Math.max(...completionIndices.map((index) => messages[index].data.durationMs)))
    assert.deepEqual(summary.byFile.map(({ file, passed, failed }) => ({ file, passed, failed }))
      .sort((left, right) => left.file.localeCompare(right.file)), [
      { file: failing, passed: 1, failed: 1 },
      { file: passing, passed: 2, failed: 0 },
    ])
    assert.equal(summary.failures[0].file, failing)
    assert.equal(summary.failures[0].sourceFile, fileURLToPath(gate))
    assert.ok(summary.failures[0].line > 0)
    assert.ok(stderr.includes(failing), stderr)
    assert.match(stderr, /controlled shared-wrapper failure/)
    const failure = messages.find(({ type, data }) => type === 'test:fail' && data.name === 'same second test')
    assert.equal(failure.data.file, fileURLToPath(gate))
    assert.equal(failure.data.entryFile, failing)
    assert.equal(classifyVerdict(failure).lane, failing)
    await assert.rejects(superviseNodeTest({
      files: [passing, failing], env: { ...childEnv, WXS_TIER_INTEGRATION: '1' },
      label: 'shared-tier-failure', silenceMs: 5000, throwOnFailure: true,
    }), /supervised suite failed/)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] repeated names within one entry and nested suites preserve every actual verdict', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'repeated-test-names-'))
  try {
    const file = join(directory, 'repeated.fixture.mjs')
    writeFileSync(file, `import { describe, test } from 'node:test';
test('same top-level test', () => {});
test('same top-level test', () => {});
describe('first parent', () => {
  test('same child', () => {});
  test('same child', () => {});
  test.todo('missing proof');
});
describe('second parent', () => {
  test('same child', () => { throw new Error('controlled nested failure'); });
  test.skip('not executed', () => { throw new Error('must not execute'); });
});
const cancelled = new AbortController();
cancelled.abort(new Error('controlled cancellation'));
test('cancelled before execution', { signal: cancelled.signal }, () => { throw new Error('must not execute'); });
`)
    const child = spawn(process.execPath, [NODE_TEST_INNER, file], {
      stdio: ['ignore', 'pipe', 'pipe', 'ipc'], env: childEnv,
    })
    const messages = []
    let stderr = ''
    child.stdout.resume()
    child.stderr.on('data', (text) => { stderr += text })
    child.on('message', (message) => messages.push(message))
    await new Promise((resolveExit, reject) => {
      child.on('error', reject)
      child.on('close', resolveExit)
    })
    const summary = messages.find(({ type }) => type === 'runner:summary')?.data
    assert.ok(summary, stderr)
    assert.deepEqual(counts(summary), { passed: 4, failed: 1, skipped: 1, todo: 1, cancelled: 1 })
    assert.equal(summary.files, 1)
    assert.equal(summary.filesCompleted, 1)
    assert.equal(summary.containerFailures, 1)
    assert.equal(summary.containerFailureDetails[0].name, 'second parent')
    assert.equal(summary.containerFailureDetails[0].file, file)
    assert.match(stderr, /controlled nested failure/)
    assert.match(stderr, /second parent/)
    assert.match(stderr, /1 failed containers/)
    assert.equal(messages.filter(({ type }) => type === 'runner:summary').length, 1)
    assert.equal(messages.filter(({ type }) => type === 'inner:drained').length, 1)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] a symlinked test entry retains its planned identity and actual source location', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'aliased-test-entry-'))
  try {
    const source = join(directory, 'source.fixture.mjs')
    const entry = join(directory, 'entry.fixture.mjs')
    writeFileSync(source, "import test from 'node:test'; test('through alias', () => { throw new Error('aliased failure'); });\n")
    symlinkSync(source, entry)
    const child = spawn(process.execPath, [NODE_TEST_INNER, entry], {
      stdio: ['ignore', 'pipe', 'pipe', 'ipc'], env: childEnv,
    })
    const messages = []
    let stderr = ''
    child.stdout.resume()
    child.stderr.on('data', (text) => { stderr += text })
    child.on('message', (message) => messages.push(message))
    await new Promise((resolveExit, reject) => {
      child.on('error', reject)
      child.on('close', resolveExit)
    })
    const summary = messages.find(({ type }) => type === 'runner:summary')?.data
    assert.ok(summary, stderr)
    assert.deepEqual(counts(summary), { passed: 0, failed: 1, skipped: 0, todo: 0, cancelled: 0 })
    assert.equal(summary.files, 1)
    assert.equal(summary.filesCompleted, 1)
    assert.equal(summary.failures[0].file, entry)
    assert.equal(summary.failures[0].sourceFile, realpathSync(source))
    await assert.rejects(superviseNodeTest({
      files: [entry], env: childEnv, label: 'aliased-failure', silenceMs: 5000, throwOnFailure: true,
    }), /supervised suite failed/)
    writeFileSync(source, "import test from 'node:test'; test('through alias', () => {});\n")
    assert.deepEqual(await superviseNodeTest({
      files: [entry], env: childEnv, label: 'aliased-pass', silenceMs: 5000, throwOnFailure: true,
    }), { passed: 1, failed: 0 })
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] a default abort remains rejected when the runtime loses its cancellation classification', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'default-abort-'))
  try {
    const file = join(directory, 'abort.fixture.mjs')
    writeFileSync(file, `import test from 'node:test';
const stop = new AbortController();
stop.abort();
test('cancelled before execution', { signal: stop.signal }, () => { throw new Error('must not execute'); });
`)
    const child = spawn(process.execPath, [NODE_TEST_INNER, file], {
      stdio: ['ignore', 'pipe', 'pipe', 'ipc'], env: childEnv,
    })
    const messages = []
    let stderr = ''
    child.stdout.resume()
    child.stderr.on('data', (text) => { stderr += text })
    child.on('message', (message) => messages.push(message))
    await new Promise((resolveExit, reject) => {
      child.on('error', reject)
      child.on('close', resolveExit)
    })
    const summary = messages.find(({ type }) => type === 'runner:summary')?.data
    assert.ok(summary, stderr)
    assert.equal(summary.passed, 0)
    assert.equal(summary.failed + summary.cancelled, 1)
    assert.equal(summary.filesCompleted, 1)
    await assert.rejects(superviseNodeTest({
      files: [file], env: childEnv, label: 'default-abort', silenceMs: 5000, throwOnFailure: true,
    }), /supervised suite failed/)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] a real reporter error aborts active work, stops queued entries and forbids a final summary', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'reporter-error-'))
  try {
    const first = join(directory, 'first.fixture.mjs')
    const next = join(directory, 'queued.fixture.mjs')
    const marker = join(directory, 'queued-started')
    const runner = join(directory, 'reporter.fixture.mjs')
    const result = join(directory, 'result.json')
    const activePid = join(directory, 'active.pid')
    writeFileSync(first, `import test from 'node:test';
import { writeFileSync } from 'node:fs';
writeFileSync(${JSON.stringify(activePid)}, String(process.pid));
test('active when reporting fails', async (context) => {
  const timer = setInterval(() => {}, 1000);
  context.after(() => clearInterval(timer));
  console.error('reporter fault trigger');
  await new Promise(() => {});
});
`)
    writeFileSync(next, `import { writeFileSync } from 'node:fs';
writeFileSync(${JSON.stringify(marker)}, 'started');
`)
    writeFileSync(runner, `import { runTestFiles } from ${JSON.stringify(new URL('./support/run-inner.mjs', import.meta.url).href)};
import { readFileSync, writeFileSync } from 'node:fs';
const messages = [];
const complete = await runTestFiles({
  files: process.argv.slice(2), concurrency: 1,
  stderr: { write() { throw new Error('controlled reporter failure'); } },
  send(message) { messages.push(message); process.send?.(message); },
});
process.once('exit', () => {
  let activeStillExists = true;
  try { process.kill(Number(readFileSync(${JSON.stringify(activePid)}, 'utf8')), 0); }
  catch (error) { if (error.code === 'ESRCH') activeStillExists = false; else throw error; }
  writeFileSync(${JSON.stringify(result)}, JSON.stringify({ complete, messages, activeStillExists }));
});
if (!complete) process.exitCode = 1;
`)
    await assert.rejects(superviseNodeTest({
      files: [first, next], inner: runner, env: childEnv,
      label: 'reporter-error', silenceMs: 5000, throwOnFailure: true,
    }), /supervised suite failed/)
    const outcome = JSON.parse(readFileSync(result, 'utf8'))
    assert.equal(outcome.complete, false)
    assert.equal(outcome.activeStillExists, false, 'active file process must end before the helper exits and outer cleanup begins')
    assert.ok(outcome.messages.some(({ type, data }) => type === 'runner:error' && /controlled reporter failure/.test(data.message)))
    assert.equal(outcome.messages.some(({ type }) => type === 'runner:summary' || type === 'inner:drained'), false)
    assert.equal(existsSync(marker), false, 'queued entry must not start after the reporting failure')
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] independent file streams preserve the configured concurrency and complete the whole group', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'runner-concurrency-'))
  try {
    for (const configured of ['1', '2', '']) {
      const limit = configured ? Number(configured) : Math.max(availableParallelism() - 1, 1)
      const eventsPath = join(directory, `events-${configured || 'default'}.jsonl`)
      writeFileSync(eventsPath, '')
      const files = []
      for (let index = 0; index < limit + 1; index++) {
        const file = join(directory, `${configured || 'default'}-${index}.fixture.mjs`)
        files.push(file)
        writeFileSync(file, `import test from 'node:test';
import { appendFileSync, readFileSync } from 'node:fs';
import { setTimeout } from 'node:timers/promises';
const path = ${JSON.stringify(eventsPath)};
const record = kind => appendFileSync(path, JSON.stringify({ kind, id: ${index} }) + '\\n');
record('start');
process.once('exit', () => record('end'));
test('wait for the initial concurrency slots', async () => {
  while (readFileSync(path, 'utf8').trim().split('\\n').map(JSON.parse).filter(x => x.kind === 'start').length < ${limit}) {
    await setTimeout(5);
  }
});
`)
      }
      assert.deepEqual(await superviseNodeTest({
        files, env: { ...childEnv, NODE_TEST_CONCURRENCY: configured },
        label: 'file-concurrency', silenceMs: 5000, throwOnFailure: true,
      }), { passed: files.length, failed: 0 })
      const events = readFileSync(eventsPath, 'utf8').trim().split('\n').map(JSON.parse)
      let active = 0
      let maximum = 0
      for (const event of events) {
        active += event.kind === 'start' ? 1 : -1
        maximum = Math.max(maximum, active)
        assert.ok(active >= 0 && active <= limit, JSON.stringify(events))
      }
      assert.equal(active, 0)
      assert.equal(maximum, limit)
      assert.equal(events.length, files.length * 2)
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] a known TODO cannot authorize complete acceptance', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'pending-proof-'))
  try {
    const inner = join(dir, 'inner.mjs')
    writeFileSync(inner, `const file = process.argv[2];
process.send({type:'test:complete',data:{name:file,file}});
process.send({type:'runner:summary',data:{passed:1,failed:0,todo:1,leafDurations:[]}});
process.send({type:'inner:drained'});
`)
    await assert.rejects(superviseNodeTest({
      files: [fixture], inner, env: childEnv, label: 'pending-proof', silenceMs: 10000, throwOnFailure: true,
    }), /supervised suite failed/)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[verification-system-021] missing or unloadable planned files fail the real supervisor despite completed file wrappers', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'failed-test-file-'))
  try {
    const missing = join(directory, 'missing.fixture.mjs')
    const broken = join(directory, 'broken.fixture.mjs')
    writeFileSync(broken, 'throw new Error("fixture failed before registering any test")\n')
    for (const file of [missing, broken]) {
      await assert.rejects(superviseNodeTest({
        files: [fixture, file], env: childEnv, label: 'failed-file', silenceMs: 10000, throwOnFailure: true,
      }), /supervised suite failed/)
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})
