import test from 'node:test'
import assert from 'node:assert/strict'
import * as fs from 'node:fs'
import * as os from 'node:os'
import * as path from 'node:path'
import { setTimeout as sleep } from 'node:timers/promises'
import { GuestOwner, bounded, absent } from './vibe-fs-mf01-vertical-c-deadline-20261021/owner.mjs'
import { ProcessSession } from './vibe-fs-mf01-vertical-c-deadline-20261021/dependencies/ProcessSession.mjs'
import { FrameReader } from './vibe-fs-mf01-vertical-c-deadline-20261021/wire.mjs'

const owned = []
const work = `const program = this; let count = 0;
  class Work {
    maxDuration = 3000;
    sanity() { return { nextDone: () => [], watchVeto: () => [] }; }
    watch() { return false; }
    async next() { const value = await program.cat('seed'); program.write('write-' + (++count), value); return { value, count }; }
  }
  return await this.offerCallable(Work);`

async function withOwner(options, run) {
  const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-c-independent-')))
  let owner
  try {
    owner = await GuestOwner.create(process.execPath, directory, options)
    owned.push({ pid: owner.pid, runId: owner.runId, directory })
    console.log(JSON.stringify({ evidence: 'owned-opened', runtime: process.versions.bun ? 'Bun' : process.versions.node, pid: owner.pid, runId: owner.runId, directory }))
    await run(owner, path.join(directory, 'scratch'))
  } finally {
    if (owner) {
      const descriptor = owner.replyFd
      const state = await bounded(owner.dispose(), 7000, 'INDEPENDENT_CLEANUP_TIMEOUT')
      assert.equal(state.phase, 'Closed')
      assert.equal(state.failure, null)
      assert.equal(absent(owner.pid), true)
      assert.equal(owner.accepted.size, 0)
      assert.equal(owner.callablePending.size, 0)
      assert.equal(owner.replyFd, null)
      assert.equal(fs.existsSync(owner.fifoPath), false)
      assert.throws(() => fs.fstatSync(descriptor), { code: 'EBADF' })
      console.log(JSON.stringify({ evidence: 'owned-cleanup', runtime: process.versions.bun ? 'Bun' : process.versions.node, pid: owner.pid, state, absent: absent(owner.pid), faults: owner.transportFaults }))
    }
    fs.rmSync(directory, { recursive: true, force: true })
  }
}

async function begin(owner, scratch, budget = 1500) {
  fs.writeFileSync(path.join(scratch, 'seed'), 'accepted')
  await owner.start(work, budget)
  const offered = await bounded(owner.nextOffer(), 1000, 'OFFER_TIMEOUT')
  const instance = await owner.callable('construct', offered, { role: 'execution' })
  return { offered, instance }
}

for (const kind of ['callable-offer', 'callable-reply']) {
  test(`independent actual buffered ${kind} cannot outrun a past reset before the timer callback`, async () => {
    const tap = new FrameReader()
    let owner
    let observed
    const sessionFactory = options => {
      const real = new ProcessSession(options, 'Capture')
      return new Proxy(real, { get(target, key) {
        if (key === 'takeOutput') return async () => {
          const output = await target.takeOutput()
          if (output.kind === 'Chunk' && output.channel === 'stdout') for (const message of tap.push(output.bytes)) {
            const selected = message.kind === kind && (kind === 'callable-offer' || owner.callablePending.get(message.commandId)?.operation === 'callMethod')
            if (selected && !observed) {
              observed = { phase: owner.phase, native: target.inspect().phase, absent: absent(owner.pid), watchdogBefore: owner.watchdogFired, commandId: message.commandId ?? null }
              owner.resetDeadline(Date.now() - 1)
            }
          }
          return output
        }
        const value = target[key]
        return typeof value === 'function' ? value.bind(target) : value
      } })
    }
    await withOwner({ sessionFactory }, async (created, scratch) => {
      owner = created
      let command
      if (kind === 'callable-offer') {
        await owner.start(`class Work { maxDuration = 1000; } return await this.offerCallable(Work);`, 1500)
        command = owner.nextOffer().then(value => ({ value }), error => ({ error: error.message }))
      } else {
        const { instance } = await begin(owner, scratch)
        command = owner.callable('callMethod', instance, { method: 'next', context: null }).then(value => ({ value }), error => ({ error: error.message }))
      }
      const state = await bounded(owner.closed.promise, 1500, 'BUFFERED_CLOSE_TIMEOUT')
      const reply = await bounded(command, 500, 'REVOKED_COMMAND_TIMEOUT')
      assert.equal(observed.phase, 'Active')
      assert.equal(observed.native, 'Running')
      assert.equal(observed.absent, false)
      assert.equal(observed.watchdogBefore, false)
      assert.equal(typeof reply.error, 'string')
      assert.equal(owner.businessSettled, false)
      assert.equal(owner.watchdogFired, true)
      assert.equal((await owner.result.promise).kind, 'TimedOut')
      assert.equal(fs.existsSync(path.join(scratch, 'write-1')), kind === 'callable-reply')
      assert.equal(owner.apis.records.filter(entry => entry.operation === 'write').length, kind === 'callable-reply' ? 1 : 0)
      assert.equal(owner.offerQueue.length, 0)
      assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
      console.log(JSON.stringify({ evidence: 'buffered-expiry', kind, pid: owner.pid, observed, reply, acceptedRecords: owner.apis.records }))
    })
  })
}

test('independent past then future reset under a live lease restores only the latest saved base at final release', async () => {
  await withOwner({ catDelayMs: 250 }, async (owner, scratch) => {
    const { instance } = await begin(owner, scratch, 300)
    const release = owner.borrowDeadline(1200)
    owner.resetDeadline(Date.now() - 1000)
    owner.resetDeadline(Date.now() + 150)
    const saved = owner.deadline.remaining
    assert.ok(saved > 0 && saved <= 150)
    assert.deepEqual(await owner.callable('callMethod', instance, { method: 'next', context: null }), { value: 'accepted', count: 1 })
    release()
    const finalEpoch = owner.deadline.current
    release()
    assert.equal(owner.deadline.current, finalEpoch)
    const started = performance.now()
    const next = owner.callable('callMethod', instance, { method: 'next', context: null }).then(value => ({ value }), error => ({ error: error.message }))
    const state = await bounded(owner.closed.promise, 1200, 'LATEST_BASE_CLOSE_TIMEOUT')
    const elapsed = performance.now() - started
    assert.equal(typeof (await next).error, 'string')
    assert.ok(elapsed >= saved - 40 && elapsed < saved + 400, JSON.stringify({ elapsed, saved }))
    assert.equal(fs.readFileSync(path.join(scratch, 'write-1'), 'utf8'), 'accepted')
    assert.equal(fs.existsSync(path.join(scratch, 'write-2')), false)
    assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
    console.log(JSON.stringify({ evidence: 'live-lease-past-future', pid: owner.pid, saved, elapsed, finalEpoch }))
  })
})

test('independent an expired pre-existing lease releases during actual T without reviving permission or killing the closure', async () => {
  await withOwner({}, async (owner, scratch) => {
    await owner.start(`const program = this; let closure = 41;
      class Work { maxDuration = 3000; async next() { await program.park('aged lease'); program.write('resumed', String(++closure)); return closure; } }
      return await this.offerCallable(Work);`, 300)
    const offered = await bounded(owner.nextOffer(), 1000, 'AGED_LEASE_OFFER_TIMEOUT')
    const instance = await owner.callable('construct', offered, { role: 'execution' })
    const release = owner.borrowDeadline(200)
    const callback = owner.callable('callMethod', instance, { method: 'next', context: null }).then(value => ({ value }), error => ({ error: error.message }))
    const leaseEnd = [...owner.deadline.leases.values()][0]
    const epoch = owner.deadline.current
    const site = await owner.park()
    await sleep(450)
    assert.ok(Date.now() > leaseEnd && Date.now() > epoch)
    assert.match(owner.sameIdentity().status, /T/)
    release()
    const afterRelease = owner.deadline.current
    release()
    assert.equal(owner.deadline.current, afterRelease)
    assert.equal(owner.deadline.leases.size, 0)
    assert.equal(owner.deadline.enabled, false)
    assert.equal(owner.watchdogFired, false)
    assert.throws(() => owner.borrowDeadline(1000), /OWNER_REVOKED/)
    owner.resetDeadline(Date.now() + 1000)
    assert.equal(owner.deadline.enabled, false)
    assert.match(owner.sameIdentity().status, /T/)
    await owner.resume(site, null, { budgetMs: 1000 })
    assert.deepEqual(await bounded(callback, 1500, 'AGED_LEASE_CALLBACK_TIMEOUT'), { value: 42 })
    assert.equal(fs.readFileSync(path.join(scratch, 'resumed'), 'utf8'), '42')
    await owner.callable('finishOffer', offered, { value: 'finished' })
    assert.deepEqual(await bounded(owner.result.promise, 1500, 'AGED_LEASE_RESULT_TIMEOUT'), { kind: 'Result', value: 'finished' })
    await bounded(owner.closed.promise, 1000, 'AGED_LEASE_CLOSE_TIMEOUT')
    console.log(JSON.stringify({ evidence: 'aged-lease-during-T', pid: owner.pid, leaseEnd, epoch, afterRelease }))
  })
})

test('independent a genuine T observation after a past reset cannot publish a usable park site', async () => {
  await withOwner({}, async (owner, scratch) => {
    const original = owner.sameIdentity.bind(owner)
    let observed
    owner.sameIdentity = () => {
      const current = original()
      if (!observed && current.status.includes('T') && owner.phase === 'ParkRequested') {
        observed = current
        owner.resetDeadline(Date.now() - 1)
      }
      return current
    }
    await owner.start("await this.park('stop'); this.write('resumed', 'forbidden'); return 1;", 1500)
    await assert.rejects(owner.park(), /PARK_DEADLINE_EXPIRED/)
    assert.match(observed.status, /T/)
    assert.equal(owner.currentSite, undefined)
    assert.equal(owner.watchdogFired, true)
    const state = await bounded(owner.closed.promise, 1500, 'LATE_T_CLOSE_TIMEOUT')
    assert.equal((await owner.result.promise).kind, 'TimedOut')
    assert.equal(fs.existsSync(path.join(scratch, 'resumed')), false)
    assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
    console.log(JSON.stringify({ evidence: 'late-real-T-expiry', pid: owner.pid, observed }))
  })
})

test.after(() => {
  for (const entry of owned) {
    assert.equal(absent(entry.pid), true)
    assert.equal(fs.existsSync(entry.directory), false)
  }
  console.log(JSON.stringify({ evidence: 'all-owned-absent', runtime: process.versions.bun ? 'Bun' : process.versions.node, owned }))
})
