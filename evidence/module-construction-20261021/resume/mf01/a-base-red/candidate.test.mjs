// MF01 isolated gate tests. Real scratch Host fs, no production ledger proof.
import test from 'node:test'
import assert from 'node:assert/strict'
import * as fs from 'node:fs'
import * as os from 'node:os'
import * as path from 'node:path'
import { GuestOwner, bounded, absent } from './owner.mjs'
import { frame, FrameReader } from './wire.mjs'
import { ProcessSession } from './dependencies/ProcessSession.mjs'

async function withOwner(options, run) {
  const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-vertical-a-')))
  let owner
  try {
    owner = await GuestOwner.create(process.execPath, directory, options)
    await run(owner, path.join(directory, 'scratch'))
  } finally {
    if (owner) {
      const state = await owner.dispose()
      assert.ok(state, 'physical close must be observed')
      assert.equal(state.phase, 'Closed')
      assert.equal(state.failure, null)
      assert.equal(absent(owner.pid), true)
      console.log(JSON.stringify({ runtime: process.versions.bun ? 'Bun' : process.versions.node, pid: owner.pid, phase: state.phase, exit: state.exit, watchdog: owner.watchdogFired, transportFaults: owner.transportFaults }))
    }
    fs.rmSync(directory, { recursive: true, force: true })
  }
}

test('gate1 wire keeps absence/undefined and Unicode split bytes, rejects non-data without JSON coercion', () => {
  const source = { ownUndefined: undefined, zero: -0, text: '汉🔥'.repeat(20), array: [undefined, null, true, 4] }
  const reader = new FrameReader()
  const messages = []
  for (const byte of frame(source)) messages.push(...reader.push(Buffer.of(byte)))
  reader.finish()
  assert.deepEqual(messages, [source])
  assert.equal(Object.hasOwn(messages[0], 'missing'), false)
  for (const value of [NaN, Infinity, 1n, Symbol(), () => {}, new Date(), new Map(), new Uint8Array(1)]) assert.throws(() => frame(value), /WIRE_/)
  const cyclic = {}; cyclic.self = cyclic
  assert.throws(() => frame(cyclic), /WIRE_CYCLE/)
  let reads = 0
  assert.throws(() => frame({ get secret() { reads++; return 1 } }), /WIRE_ACCESSOR/)
  assert.equal(reads, 0)
  const incomplete = new FrameReader()
  incomplete.push(frame(source).subarray(0, 9))
  assert.throws(() => incomplete.finish(), /WIRE_INCOMPLETE/)
})

test('gate1 actual sync Host write and simultaneous async cat preserve complete large Unicode replies', async () => {
  await withOwner({ catDelayMs: 10 }, async (owner, scratch) => {
    const text = '汉🔥\u0000edge'.repeat(40000)
    fs.writeFileSync(path.join(scratch, 'before'), 'prior', 'utf8')
    await owner.start(`const pending = this.cat('before'); const text = ${JSON.stringify(text)}; this.write('after', text); return { old: await pending, current: await this.cat('after') };`, 5000)
    const result = await bounded(owner.result.promise, 6000, 'RESULT_TIMEOUT')
    assert.deepEqual(result, { kind: 'Result', value: { old: 'prior', current: text } })
    assert.equal(fs.readFileSync(path.join(scratch, 'after'), 'utf8'), text)
    assert.equal(owner.apis.records.filter(record => record.operation === 'write').length, 1)
    assert.deepEqual(owner.transportFaults, [])
    const closed = await bounded(owner.closed.promise, 2000, 'NORMAL_CLOSE_TIMEOUT')
    assert.deepEqual(closed.exit, { code: 0, signal: null })
  })
})

test('gate1 accepted sync write with lost reply remains once, Host responsive, external deadline witnesses actual close', async () => {
  await withOwner({ dropReplyFor: 'write' }, async (owner, scratch) => {
    let ticks = 0
    const timer = setInterval(() => ticks++, 10)
    try {
      await owner.start("this.write('once', 'accepted'); return 'not reached';", 250)
      const result = await bounded(owner.result.promise, 2000, 'EXTERNAL_DEADLINE_TIMEOUT')
      assert.equal(result.kind, 'TimedOut')
      assert.equal(owner.watchdogFired, true)
      assert.ok(ticks >= 3)
      assert.equal(fs.readFileSync(path.join(scratch, 'once'), 'utf8'), 'accepted')
      assert.equal(owner.apis.records.filter(record => record.operation === 'write').length, 1)
      assert.deepEqual(result.state.exit, { code: null, signal: 'SIGKILL' })
    } finally { clearInterval(timer) }
  })
})

test('gate1 after-await runaway does not block Host and is physically terminated by its external owner', async () => {
  await withOwner({}, async (owner, scratch) => {
    fs.writeFileSync(path.join(scratch, 'seed'), 'go')
    let ticks = 0
    const timer = setInterval(() => ticks++, 10)
    try {
      await owner.start("await this.cat('seed'); while (true) {}", 250)
      const state = await bounded(owner.closed.promise, 2000, 'RUNAWAY_CLOSE_TIMEOUT')
      assert.equal(owner.watchdogFired, true)
      assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
      assert.ok(ticks >= 3)
    } finally { clearInterval(timer) }
  })
})

test('gate2 observed physical park resumes one original continuation with >pipe payload and immediate legitimate sync write', async () => {
  await withOwner({}, async (owner, scratch) => {
    await owner.start("let keep = 41; const value = await this.park({ keep }); this.write('resumed', value); return { keep: ++keep, value };", 5000)
    const parked = await owner.park()
    assert.match(parked.observed.status, /T/)
    assert.equal(parked.payload.keep, 41)
    assert.equal(owner.session.inspect().phase, 'Running')
    assert.equal(owner.session.inspect().exit, null)
    assert.equal((await owner.session.wait(20)).kind, 'TimedOut')
    assert.equal(fs.existsSync(path.join(scratch, 'resumed')), false)
    const text = 'large汉🔥'.repeat(70000)
    const resumed = owner.resume(parked, text, { budgetMs: 5000 })
    await assert.rejects(() => owner.resume(parked, 'duplicate'), /HANDLE_NOT_PARKED/)
    await resumed
    const result = await bounded(owner.result.promise, 6000, 'PARK_RESULT_TIMEOUT')
    assert.deepEqual(result, { kind: 'Result', value: { keep: 42, value: text } })
    assert.equal(fs.readFileSync(path.join(scratch, 'resumed'), 'utf8'), text)
    assert.equal(owner.apis.records.filter(record => record.operation === 'write').length, 1)
    assert.deepEqual(owner.rejected, [])
    assert.deepEqual(owner.transportFaults, [])
    assert.equal(owner.receipts.filter(receipt => receipt.kind === 'control-ack').length, 1)
    assert.equal(owner.receipts.filter(receipt => receipt.kind === 'activated').length, 1)
    const closed = await bounded(owner.closed.promise, 2000, 'RESUMED_CLOSE_TIMEOUT')
    assert.deepEqual(closed.exit, { code: 0, signal: null })
  })
})

test('gate2 ready-proven TERM-resistant stopped owner requires actual KILL and EOF, never a request-only terminal', async () => {
  await withOwner({ ignoreTerm: true }, async owner => {
    assert.equal(owner.readyFrame.termIgnoring, true)
    await owner.start("await this.park({ stopped: true }); return 1;", 5000)
    await owner.park()
    assert.equal(owner.session.signal('SIGTERM').kind, 'Requested')
    assert.equal((await owner.session.wait(20)).kind, 'TimedOut')
    assert.equal(owner.session.inspect().exit, null)
    assert.match(owner.sameIdentity().status, /T/)
    owner.kill()
    const state = await bounded(owner.closed.promise, 2000, 'STOPPED_KILL_TIMEOUT')
    assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
  })
})

test('gate2 an accepted async result settles while stopped and its original response survives resume', async () => {
  await withOwner({ catDelayMs: 100 }, async (owner, scratch) => {
    fs.writeFileSync(path.join(scratch, 'before'), 'accepted-before-park')
    await owner.start("const pending = this.cat('before'); const value = await this.park({ step: 1 }); return { value, before: await pending };", 5000)
    const site = await owner.park()
    await bounded((async () => { while (owner.replyQueue.length === 0) await new Promise(resolve => setTimeout(resolve, 5)) })(), 1000, 'ACCEPTED_REPLY_NOT_RETAINED')
    assert.equal(owner.phase, 'Parked')
    assert.match(owner.sameIdentity().status, /T/)
    assert.equal(owner.replyQueue.length, 1)
    assert.equal(owner.apis.records.filter(record => record.operation === 'cat').length, 1)
    await owner.resume(site, 'new-value')
    assert.deepEqual(await bounded(owner.result.promise, 2000, 'PENDING_RESULT_TIMEOUT'), { kind: 'Result', value: { value: 'new-value', before: 'accepted-before-park' } })
    assert.equal(owner.replyQueue.length, 0)
    assert.deepEqual((await bounded(owner.closed.promise, 2000, 'PENDING_CLOSE_TIMEOUT')).exit, { code: 0, signal: null })
  })
})

test('gate2 again-park has a new exact site and a consumed old site cannot resume the successor', async () => {
  await withOwner({}, async owner => {
    await owner.start("let keep = 41; const first = await this.park({ step: 1 }); const second = await this.park({ step: 2 }); return { first, second, keep: ++keep };", 5000)
    const first = await owner.park()
    await owner.resume(first, 'first')
    const second = await owner.park()
    assert.equal(second.generation, first.generation + 1)
    assert.notEqual(second, first)
    await assert.rejects(() => owner.resume(first, 'stale'), /HANDLE_NOT_PARKED/)
    assert.equal(owner.phase, 'Parked')
    assert.match(owner.sameIdentity().status, /T/)
    await owner.resume(second, 'second')
    assert.deepEqual(await bounded(owner.result.promise, 2000, 'SECOND_PARK_TIMEOUT'), { kind: 'Result', value: { first: 'first', second: 'second', keep: 42 } })
    assert.deepEqual((await bounded(owner.closed.promise, 2000, 'SECOND_PARK_CLOSE_TIMEOUT')).exit, { code: 0, signal: null })
  })
})

test('boundary startup ENOENT and ready timeout reclaim only their exact process and private FIFO descriptor', async () => {
  for (const [executable, options, expected] of [
    ['/definitely-no-mf01-executable', {}, /Failed/],
    [process.execPath, { noReady: true, readyTimeoutMs: 200 }, /READY_TIMEOUT/],
  ]) {
    const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-start-failure-')))
    let session
    try {
      const factory = values => { session = new ProcessSession(values, 'Capture'); return session }
      await assert.rejects(() => GuestOwner.create(executable, directory, { ...options, sessionFactory: factory }), error => {
        assert.equal(error.message, 'GUEST_START_FAILED')
        assert.match(error.cause.message, expected)
        assert.equal(error.cleanupOwner, null)
        assert.equal(error.cleanup.phase, 'Closed')
        return true
      })
      assert.equal(fs.existsSync(path.join(directory, 'sync-reply')), false)
      assert.equal(session.inspect().phase, 'Closed')
      const pid = session.inspect().pid
      if (pid !== null) assert.equal(absent(pid), true)
    } finally { fs.rmSync(directory, { recursive: true, force: true }) }
  }
  const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-foreign-fifo-')))
  const foreign = path.join(directory, 'sync-reply')
  fs.writeFileSync(foreign, 'foreign sentinel')
  let called = false
  try {
    await assert.rejects(() => GuestOwner.create(process.execPath, directory, { sessionFactory() { called = true; throw new Error('MUST_NOT_SPAWN') } }), error => {
      assert.equal(error.message, 'GUEST_START_FAILED')
      assert.deepEqual(error.cleanup, { kind: 'NotStarted' })
      return true
    })
    assert.equal(called, false)
    assert.equal(fs.readFileSync(foreign, 'utf8'), 'foreign sentinel')
  } finally { fs.rmSync(directory, { recursive: true, force: true }) }
})

test('boundary injected unreadable close receipt retains ClosingUnknown and descriptor until real close can be observed', async () => {
  const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-unknown-close-')))
  let real
  let owner
  let permitClose = false
  let reads = 0
  let inject
  const fault = new Promise((_, reject) => { inject = () => reject(new Error('INJECTED_CAPTURE_FAULT')) })
  const sessionFactory = options => {
    real = new ProcessSession(options, 'Capture')
    return new Proxy(real, { get(target, key) {
      if (key === 'wait') return milliseconds => permitClose ? target.wait(milliseconds) : Promise.resolve({ kind: 'TimedOut', state: target.inspect() })
      if (key === 'takeOutput') return () => ++reads === 1 ? target.takeOutput() : Promise.race([target.takeOutput(), fault])
      const value = target[key]
      return typeof value === 'function' ? value.bind(target) : value
    } })
  }
  try {
    owner = await GuestOwner.create(process.execPath, directory, { sessionFactory })
    const descriptor = owner.replyFd
    let publishedClosed = false
    owner.closed.promise.then(() => { publishedClosed = true })
    inject()
    const unknown = await bounded(owner.closeUnknown.promise, 2000, 'UNKNOWN_NOT_RETAINED')
    assert.equal(unknown.owner, owner)
    assert.equal(owner.phase, 'ClosingUnknown')
    assert.equal(publishedClosed, false)
    assert.equal(fs.existsSync(owner.fifoPath), true)
    assert.ok(fs.fstatSync(descriptor).isFIFO())
    await assert.rejects(() => owner.dispose(), error => error.message === 'CLOSE_UNCONFIRMED' && error.cleanupOwner === owner)
    assert.equal(owner.phase, 'ClosingUnknown')
    permitClose = true
    const closed = await bounded(owner.closed.promise, 2000, 'RETAINED_CLOSE_NOT_OBSERVED')
    assert.equal(closed.phase, 'Closed')
    assert.deepEqual(closed.exit, { code: null, signal: 'SIGKILL' })
    await owner.dispose()
    assert.equal(fs.existsSync(owner.fifoPath), false)
    assert.throws(() => fs.fstatSync(descriptor), { code: 'EBADF' })
    assert.equal(absent(owner.pid), true)
  } finally {
    permitClose = true
    if (owner) await owner.dispose()
    else if (real) { real.signal('SIGKILL'); real.discardOutput(); await real.wait(5000) }
    fs.rmSync(directory, { recursive: true, force: true })
  }
})
