import test from 'node:test'
import assert from 'node:assert/strict'
import * as fs from 'node:fs'
import * as os from 'node:os'
import * as path from 'node:path'
import { GuestOwner, bounded, absent } from './vibe-fs-mf01-vertical-a-20261021/owner.mjs'
import { FrameReader } from './vibe-fs-mf01-vertical-a-20261021/wire.mjs'
import { ProcessSession } from './vibe-fs-mf01-vertical-a-20261021/dependencies/ProcessSession.mjs'

const sleep = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds))
const owned = []

async function create(options = {}) {
  const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-independent-')))
  try {
    const owner = await GuestOwner.create(process.execPath, directory, options)
    owned.push({ pid: owner.pid, runId: owner.runId, directory })
    return owner
  } catch (error) {
    if (error.cleanupOwner) throw error
    fs.rmSync(directory, { recursive: true, force: true })
    throw error
  }
}

async function cleanup(owner) {
  const descriptor = owner.replyFd
  const state = await bounded(owner.dispose(), 7000, 'INDEPENDENT_CLEANUP_TIMEOUT')
  assert.equal(state.phase, 'Closed')
  assert.equal(state.failure, null)
  assert.equal(absent(owner.pid), true)
  assert.equal(fs.existsSync(owner.fifoPath), false)
  if (descriptor !== null) assert.throws(() => fs.fstatSync(descriptor), { code: 'EBADF' })
  console.log(JSON.stringify({ evidence: 'owned-cleanup', runtime: process.versions.bun ? 'Bun' : process.versions.node, pid: owner.pid, runId: owner.runId, phase: state.phase, exit: state.exit, acceptedRemaining: owner.accepted.size, absent: absent(owner.pid), directory: owner.directory }))
  fs.rmSync(owner.directory, { recursive: true, force: true })
}

test('independent exact park site rejects copied and foreign owner references without consuming the current site', async () => {
  const first = await create()
  let second
  try {
    second = await create()
    for (const owner of [first, second]) await owner.start('let closure = 41; const value = await this.park({ closure }); return { closure: ++closure, value };', 3000)
    const firstSite = await first.park()
    const secondSite = await second.park()
    for (const [owner, badSites] of [[first, [{ ...firstSite }, secondSite]], [second, [{ ...secondSite }, firstSite]]]) {
      for (const site of badSites) await assert.rejects(() => owner.resume(site, 'invalid'), /HANDLE_NOT_PARKED/)
      assert.equal(owner.phase, 'Parked')
      assert.match(owner.sameIdentity().status, /T/)
      assert.equal(owner.receipts.some(receipt => receipt.kind === 'control-ack'), false)
    }
    await second.resume(secondSite, 'second')
    assert.deepEqual(await bounded(second.result.promise, 1500, 'SECOND_RESULT_TIMEOUT'), { kind: 'Result', value: { closure: 42, value: 'second' } })
    assert.equal(first.phase, 'Parked')
    await first.resume(firstSite, 'first')
    assert.deepEqual(await bounded(first.result.promise, 1500, 'FIRST_RESULT_TIMEOUT'), { kind: 'Result', value: { closure: 42, value: 'first' } })
    for (const owner of [first, second]) await bounded(owner.closed.promise, 1500, 'SITE_CLOSE_TIMEOUT')
  } finally {
    if (second) await cleanup(second)
    await cleanup(first)
  }
})

for (const [name, source] of [
  ['synchronous body', 'while (true) {}'],
  ['after-await returned getter', "await this.cat('seed'); return { get value() { while (true) {} } };"],
  ['after-await returned toJSON', "await this.cat('seed'); return { toJSON() { while (true) {} } };"],
  ['after-await thenable getter', "await this.cat('seed'); return { get then() { while (true) {} } };"],
]) {
  test(`independent external owner closes ${name} runaway while Host remains responsive`, async () => {
    const owner = await create()
    let ticks = 0
    const timer = setInterval(() => ticks++, 10)
    try {
      fs.writeFileSync(path.join(owner.directory, 'scratch', 'seed'), 'ready')
      await owner.start(source, 250)
      const state = await bounded(owner.closed.promise, 2000, 'RUNAWAY_CLOSE_TIMEOUT')
      assert.equal(owner.watchdogFired, true)
      assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
      assert.ok(ticks >= 3)
      assert.equal((await owner.result.promise).kind, 'TimedOut')
    } finally {
      clearInterval(timer)
      await cleanup(owner)
    }
  })
}

test('independent abandon runs original finally with legitimate synchronous Host effects and refuses another park', async () => {
  const owner = await create()
  const scratch = path.join(owner.directory, 'scratch')
  try {
    await owner.start("let closure = 41; try { await this.park({ closure }); } finally { this.write('finally', String(++closure)); const denied = await this.park({ again: true }); this.write('denied', denied.code); } return 'unexpected';", 3000)
    const site = await owner.park()
    await owner.resume(site, undefined, { abandon: true, budgetMs: 1500 })
    const result = await bounded(owner.result.promise, 2000, 'ABANDON_RESULT_TIMEOUT')
    assert.equal(result.kind, 'GuestFailure')
    assert.equal(result.error.message, 'ABANDONED')
    assert.equal(fs.readFileSync(path.join(scratch, 'finally'), 'utf8'), '42')
    assert.equal(fs.readFileSync(path.join(scratch, 'denied'), 'utf8'), 'CANNOT_PARK_WHILE_ABANDONING')
    assert.equal(owner.receipts.filter(receipt => receipt.kind === 'park').length, 1)
    assert.equal(owner.apis.records.filter(record => record.operation === 'write').length, 2)
    assert.deepEqual(owner.rejected, [])
    await bounded(owner.closed.promise, 2000, 'ABANDON_CLOSE_TIMEOUT')
  } finally { await cleanup(owner) }
})

test('independent accepted old async reply after close cannot reach a fresh owner with the same request ordinal', async () => {
  const first = await create({ catDelayMs: 250 })
  let second
  try {
    fs.writeFileSync(path.join(first.directory, 'scratch', 'seed'), 'old')
    await first.start("await this.cat('seed'); this.write('late', 'must not run'); return 'old';", 3000)
    await bounded((async () => { while (first.apis.records.length === 0) await sleep(5) })(), 1000, 'OLD_CAT_NOT_ACCEPTED')
    first.kill()
    await bounded(first.closed.promise, 1500, 'OLD_CLOSE_TIMEOUT')
    second = await create()
    await second.start("this.write('fresh', 'new'); return 'new';", 1500)
    assert.deepEqual(await bounded(second.result.promise, 2000, 'FRESH_RESULT_TIMEOUT'), { kind: 'Result', value: 'new' })
    await bounded(Promise.allSettled([...first.accepted]), 1500, 'OLD_REPLY_DRAIN_TIMEOUT')
    assert.equal(fs.existsSync(path.join(first.directory, 'scratch', 'late')), false)
    assert.equal(fs.readFileSync(path.join(second.directory, 'scratch', 'fresh'), 'utf8'), 'new')
    assert.equal(first.apis.records.length, 1)
    assert.deepEqual(first.transportFaults, [])
    assert.deepEqual(second.transportFaults, [])
    await bounded(second.closed.promise, 1500, 'FRESH_CLOSE_TIMEOUT')
  } finally {
    if (second) await cleanup(second)
    await cleanup(first)
  }
})

test('independent a real result chunk observed after owner cancellation cannot publish Result', async () => {
  const tap = new FrameReader()
  let owner
  let observed
  const sessionFactory = options => {
    const real = new ProcessSession(options, 'Capture')
    return new Proxy(real, { get(target, key) {
      if (key === 'takeOutput') return async () => {
        const output = await target.takeOutput()
        if (output.kind === 'Chunk' && output.channel === 'stdout') {
          for (const message of tap.push(output.bytes)) {
            if (message.kind === 'result') {
              assert.ok(owner)
              observed = { phaseBefore: owner.phase, nativeBefore: target.inspect().phase, absentBefore: absent(owner.pid), value: message.value }
              owner.kill()
              observed.phaseAfterCancel = owner.phase
            }
          }
        }
        return output
      }
      const value = target[key]
      return typeof value === 'function' ? value.bind(target) : value
    } })
  }
  try {
    owner = await create({ sessionFactory })
    await owner.start("this.write('accepted', 'once'); return 'complete';", 1500)
    const result = await bounded(owner.result.promise, 2000, 'CANCEL_RESULT_TIMEOUT')
    console.log(JSON.stringify({ evidence: 'cancel-before-result-dispatch', runtime: process.versions.bun ? 'Bun' : process.versions.node, pid: owner.pid, observed, published: result.kind }))
    assert.equal(observed.phaseAfterCancel, 'Closing')
    await bounded(owner.closed.promise, 1500, 'CANCEL_CLOSE_TIMEOUT')
    assert.notEqual(result.kind, 'Result', 'owner cancellation precedes dispatch of the actual buffered guest result')
  } finally { if (owner) await cleanup(owner) }
})

test.after(() => {
  for (const entry of owned) {
    assert.equal(absent(entry.pid), true, `owned PID ${entry.pid} must be absent`)
    assert.equal(fs.existsSync(entry.directory), false)
  }
  console.log(JSON.stringify({ evidence: 'all-owned-absent', runtime: process.versions.bun ? 'Bun' : process.versions.node, owned }))
})
