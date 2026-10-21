// Temporary single-owner deadline proof. Scratch fs is real; no production ledger/shell.
import test from 'node:test'
import assert from 'node:assert/strict'
import * as fs from 'node:fs'
import * as os from 'node:os'
import * as path from 'node:path'
import { setTimeout as sleep } from 'node:timers/promises'
import { GuestOwner, bounded, absent, identity } from './owner.mjs'
import { GuestOwner as LegacyOwner } from '../vibe-fs-mf01-vertical-a-callable-20261021/owner.mjs'
import { ProcessSession } from './dependencies/ProcessSession.mjs'
import { FrameReader } from './wire.mjs'

async function withOwner(options, run, Owner = GuestOwner) {
  const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-deadline-')))
  let owner
  try {
    owner = await Owner.create(process.execPath, directory, options)
    await run(owner, path.join(directory, 'scratch'))
  } finally {
    if (owner) {
      const state = await owner.dispose()
      assert.equal(state.phase, 'Closed')
      assert.equal(state.failure, null)
      assert.equal(absent(owner.pid), true)
      assert.equal(owner.accepted.size, 0)
      assert.equal(owner.replyFd, null)
      assert.equal(fs.existsSync(owner.fifoPath), false)
      console.log(JSON.stringify({ runtime: process.versions.bun ? 'Bun' : process.versions.node, pid: owner.pid, runId: owner.runId, exit: state.exit, watchdog: owner.watchdogFired, faults: owner.transportFaults, businessSettled: owner.businessSettled }))
    }
    fs.rmSync(directory, { recursive: true, force: true })
  }
}

const work = `const program = this; let calls = 0;
  class Work {
    maxDuration = 70000;
    sanity() { return { nextDone: () => [], watchVeto: () => [] }; }
    watch() { return false; }
    async next() { const text = await program.cat('seed'); program.write('after-' + (++calls), text); return { text, calls }; }
  }
  return await this.offerCallable(Work);`

async function begin(owner, scratch, budget) {
  fs.writeFileSync(path.join(scratch, 'seed'), 'actual')
  await owner.start(work, budget)
  const offered = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
  const instance = await owner.callable('construct', offered, { role: 'execution' })
  return { offered, instance }
}
const next = (owner, instance) => owner.callable('callMethod', instance, { method: 'next', context: null })
async function finish(owner, offered, value = 'done') {
  await owner.callable('finishOffer', offered, { value })
  assert.deepEqual(await bounded(owner.result.promise, 2000, 'RESULT_TIMEOUT'), { kind: 'Result', value })
  const state = await bounded(owner.closed.promise, 2000, 'NORMAL_CLOSE_TIMEOUT')
  assert.deepEqual(state.exit, { code: 0, signal: null })
}

if (process.env.MF01_DEADLINE_BASELINE === '1') {
  test('baseline Host-only watchdog extension cannot keep the legacy guest epoch alive', async () => {
    await withOwner({ catDelayMs: 400 }, async (owner, scratch) => {
      const { instance } = await begin(owner, scratch, 200)
      owner.arm(Date.now() + 1500)
      const callback = next(owner, instance).then(value => ({ value }), error => ({ error: error.message }))
      const result = await bounded(owner.result.promise, 2000, 'BASELINE_RESULT_TIMEOUT')
      console.log(JSON.stringify({ evidence: 'legacy-second-authority', hostRemaining: owner.lastDeadline - Date.now(), watchdog: owner.watchdogFired, result, callback: await callback }))
      assert.equal(result.kind, 'Result', 'a Host extension must be sufficient, without sending an epoch update to guest')
    }, LegacyOwner)
  })
} else {
  test('single owner legal 61-second async borrow crosses old epoch then writes and restores original budget', async () => {
    await withOwner({ catDelayMs: 61050 }, async (owner, scratch) => {
      const { offered, instance } = await begin(owner, scratch, 1500)
      const originalEpoch = owner.deadline.current
      const beforeBorrow = owner.deadline.remainingMs()
      const release = owner.borrowDeadline(66000)
      const started = performance.now()
      const callback = await bounded(next(owner, instance), 65000, 'LONG_BORROW_TIMEOUT')
      const elapsed = performance.now() - started
      assert.ok(elapsed >= 61000, String(elapsed))
      assert.ok(Date.now() > originalEpoch)
      assert.deepEqual(callback, { text: 'actual', calls: 1 })
      assert.equal(fs.readFileSync(path.join(scratch, 'after-1'), 'utf8'), 'actual')
      release()
      const remaining = owner.deadline.remainingMs()
      assert.ok(remaining > 0 && remaining <= beforeBorrow && remaining >= beforeBorrow - 30)
      console.log(JSON.stringify({ evidence: 'long-light-interaction', elapsed, originalEpoch, beforeBorrow, restoredRemaining: remaining, pid: owner.pid }))
      await finish(owner, offered, 'long valid')
    })
  })

  test('single owner final release restores first remaining budget and real guest exhausts it without a second write', async () => {
    await withOwner({ catDelayMs: 550 }, async (owner, scratch) => {
      const { instance } = await begin(owner, scratch, 350)
      const originalEpoch = owner.deadline.current
      const release = owner.borrowDeadline(1800)
      const saved = owner.deadline.remaining
      assert.deepEqual(await next(owner, instance), { text: 'actual', calls: 1 })
      assert.ok(Date.now() > originalEpoch)
      const releasedAt = performance.now()
      release()
      release()
      const rejected = next(owner, instance).then(() => false, () => true)
      const state = await bounded(owner.closed.promise, 2000, 'RESTORED_BUDGET_CLOSE_TIMEOUT')
      const elapsed = performance.now() - releasedAt
      assert.equal(await rejected, true)
      assert.ok(elapsed >= saved - 50 && elapsed < saved + 500, JSON.stringify({ elapsed, saved }))
      assert.equal(fs.existsSync(path.join(scratch, 'after-2')), false)
      assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
      await Promise.allSettled([...owner.accepted])
      assert.equal(owner.accepted.size, 0)
      console.log(JSON.stringify({ evidence: 'original-budget-restored', saved, elapsed, acceptedSettled: true }))
    })
  })

  for (const shortFirst of [true, false]) {
    test('single owner parallel leases release ' + (shortFirst ? 'short then long' : 'long then short') + ' without a second base snapshot', async () => {
      await withOwner({ catDelayMs: 350 }, async (owner, scratch) => {
        const { offered, instance } = await begin(owner, scratch, 300)
        const short = owner.borrowDeadline(750)
        const saved = owner.deadline.remaining
        const long = owner.borrowDeadline(1800)
        assert.equal(owner.deadline.remaining, saved)
        assert.deepEqual(await next(owner, instance), { text: 'actual', calls: 1 })
        const releaseFirst = shortFirst ? short : long
        releaseFirst()
        const epoch = owner.deadline.current
        releaseFirst()
        assert.equal(owner.deadline.current, epoch)
        await sleep(200)
        if (shortFirst) {
          assert.deepEqual(await next(owner, instance), { text: 'actual', calls: 2 })
          long()
          assert.ok(owner.deadline.remainingMs() <= saved)
          await finish(owner, offered)
        } else {
          const rejected = next(owner, instance).then(() => false, () => true)
          const state = await bounded(owner.closed.promise, 1600, 'SHORT_LEASE_CLOSE_TIMEOUT')
          assert.equal(await rejected, true)
          assert.equal(fs.existsSync(path.join(scratch, 'after-2')), false)
          assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
          short()
          short()
          assert.throws(() => owner.resetDeadline(Date.now() + 1000), /OWNER_REVOKED/)
        }
      })
    })
  }

  test('single owner reset during borrow replaces only saved base and final release exposes reset budget', async () => {
    await withOwner({ catDelayMs: 400 }, async (owner, scratch) => {
      const { instance } = await begin(owner, scratch, 300)
      const release = owner.borrowDeadline(1400)
      owner.resetDeadline(Date.now() + 180)
      const saved = owner.deadline.remaining
      assert.ok(saved >= 175 && saved <= 180)
      assert.deepEqual(await next(owner, instance), { text: 'actual', calls: 1 })
      release()
      const started = performance.now()
      const rejected = next(owner, instance).then(() => false, () => true)
      await bounded(owner.closed.promise, 1300, 'RESET_RELEASE_CLOSE_TIMEOUT')
      assert.equal(await rejected, true)
      const elapsed = performance.now() - started
      assert.ok(elapsed >= saved - 40 && elapsed < saved + 400)
      assert.equal(fs.existsSync(path.join(scratch, 'after-2')), false)
    })
  })

  test('single owner past reset under live lease protects admitted work then expires on final release', async () => {
    await withOwner({ catDelayMs: 300 }, async (owner, scratch) => {
      const { instance } = await begin(owner, scratch, 250)
      const release = owner.borrowDeadline(1000)
      owner.resetDeadline(Date.now() - 1000)
      assert.equal(owner.deadline.remaining, 0)
      assert.deepEqual(await next(owner, instance), { text: 'actual', calls: 1 })
      release()
      assert.throws(() => next(owner, instance), /DEADLINE_EXPIRED/)
      assert.equal(fs.existsSync(path.join(scratch, 'after-2')), false)
      const state = await bounded(owner.closed.promise, 1200, 'PAST_RELEASE_CLOSE_TIMEOUT')
      assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
    })
  })

  test('single owner past then future reset in one Host turn re-arms before admission as production 028 permits', async () => {
    await withOwner({}, async (owner, scratch) => {
      const { offered, instance } = await begin(owner, scratch, 1000)
      owner.resetDeadline(Date.now() - 1000)
      owner.resetDeadline(Date.now() + 1000)
      assert.deepEqual(await next(owner, instance), { text: 'actual', calls: 1 })
      await finish(owner, offered)
    })
  })

  test('single owner expired reset refuses next capability and Closing cannot be re-armed', async () => {
    await withOwner({}, async (owner, scratch) => {
      const { instance } = await begin(owner, scratch, 1000)
      owner.resetDeadline(Date.now() - 1)
      assert.throws(() => next(owner, instance), /DEADLINE_EXPIRED/)
      assert.throws(() => owner.resetDeadline(Date.now() + 1000), /OWNER_REVOKED/)
      const state = await bounded(owner.closed.promise, 1200, 'PAST_RESET_CLOSE_TIMEOUT')
      assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
      assert.equal(fs.existsSync(path.join(scratch, 'after-1')), false)
      assert.equal(owner.apis.records.length, 0)
    })
  })

  test('single owner timer chunks a far future epoch without overflow expiry', async () => {
    await withOwner({}, async (owner, scratch) => {
      const { offered, instance } = await begin(owner, scratch, 1000)
      owner.resetDeadline(Date.now() + 2147483647 + 100000)
      await sleep(40)
      assert.equal(owner.phase, 'Active')
      assert.equal(absent(owner.pid), false)
      assert.deepEqual(await next(owner, instance), { text: 'actual', calls: 1 })
      owner.resetDeadline(Date.now() + 1000)
      await finish(owner, offered)
    })
  })

  test('single owner updates while guest CPU loop cannot read any epoch message', async () => {
    await withOwner({}, async (owner, scratch) => {
      await owner.start("this.write('entered', 'loop'); while (true) {}", 300)
      await bounded((async () => { while (!fs.existsSync(path.join(scratch, 'entered'))) await sleep(5) })(), 1000, 'LOOP_NOT_ENTERED')
      const oldEpoch = owner.deadline.current
      owner.resetDeadline(Date.now() + 900)
      await sleep(450)
      assert.ok(Date.now() > oldEpoch)
      assert.equal(owner.phase, 'Active')
      assert.equal(absent(owner.pid), false)
      owner.resetDeadline(Date.now() - 1)
      const state = await bounded(owner.closed.promise, 1300, 'LOOP_RESET_CLOSE_TIMEOUT')
      assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
      assert.equal((await owner.result.promise).kind, 'TimedOut')
      assert.equal(owner.businessSettled, false)
    })
  })

  for (const kind of ['request', 'result']) {
    test('single owner rejects a real buffered ' + kind + ' after epoch expires before timer dispatch', async () => {
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
                if (message.kind === kind && (kind !== 'request' || message.operation === 'write')) {
                  observed = { phase: owner.phase, native: target.inspect().phase, absent: absent(owner.pid), beforeExpiry: owner.watchdogFired }
                  owner.resetDeadline(Date.now() - 1)
                }
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
        await owner.start("this.write('mutation', 'once'); return 'complete';", 1500)
        const result = await bounded(owner.result.promise, 2000, 'BUFFERED_FRAME_TIMEOUT')
        assert.equal(observed.phase, 'Active')
        assert.equal(observed.native, 'Running')
        assert.equal(observed.absent, false)
        assert.equal(observed.beforeExpiry, false)
        assert.notEqual(result.kind, 'Result')
        assert.equal(owner.watchdogFired, true)
        assert.equal(fs.existsSync(path.join(scratch, 'mutation')), kind === 'result')
        assert.equal(owner.apis.records.filter(record => record.operation === 'write').length, kind === 'result' ? 1 : 0)
        console.log(JSON.stringify({ evidence: 'buffered-expiry-admission', kind, observed, published: result.kind }))
      })
    })
  }

  test('single owner real T park survives original epoch and resumes original closure under new budget', async () => {
    await withOwner({}, async (owner, scratch) => {
      await owner.start("let closure = 41; await this.park('sleep'); this.write('resumed', String(++closure)); return closure;", 350)
      const originalEpoch = owner.deadline.current
      const site = await owner.park()
      await sleep(550)
      assert.ok(Date.now() > originalEpoch)
      assert.ok(identity(owner.pid).status.includes('T'))
      assert.equal(owner.phase, 'Parked')
      assert.equal(owner.watchdogFired, false)
      await owner.resume(site, null, { budgetMs: 1000 })
      assert.deepEqual(await bounded(owner.result.promise, 2000, 'PARKED_RESULT_TIMEOUT'), { kind: 'Result', value: 42 })
      assert.equal(fs.readFileSync(path.join(scratch, 'resumed'), 'utf8'), '42')
      await assert.rejects(owner.resume(site, null), /HANDLE_NOT_PARKED/)
      const state = await bounded(owner.closed.promise, 2000, 'PARKED_CLOSE_TIMEOUT')
      assert.deepEqual(state.exit, { code: 0, signal: null })
    })
  })

  test('single owner pre-existing lease releases while physically parked without killing original callback', async () => {
    await withOwner({}, async owner => {
      await owner.start(`const program = this; let closure = 41; class Work { maxDuration = 5000; next() { return program.park('callback').then(() => ++closure); } } return await this.offerCallable(Work);`, 350)
      const offered = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
      const release = owner.borrowDeadline(1800)
      const instance = await owner.callable('construct', offered, { role: 'execution' })
      const callback = next(owner, instance)
      const site = await owner.park()
      await sleep(500)
      release()
      release()
      assert.equal(owner.deadline.enabled, false)
      assert.ok(identity(owner.pid).status.includes('T'))
      await assert.rejects(Promise.resolve().then(() => owner.borrowDeadline(1000)), /OWNER_REVOKED/)
      await owner.resume(site, null, { budgetMs: 1000 })
      assert.equal(await callback, 42)
      await finish(owner, offered)
    })
  })

  test('single owner fixed cleanup retires old lease and cleanup borrow cannot extend a looping finally', async () => {
    await withOwner({ catDelayMs: 700 }, async (owner, scratch) => {
      fs.writeFileSync(path.join(scratch, 'seed'), 'retained')
      await owner.start(`const program = this; class Work { maxDuration = 5000; next() { return program.park('callback'); } }
        try { await this.offerCallable(Work); } catch (_) {}
        finally { this.cat('seed'); this.write('cleanup', 'actual'); await Promise.resolve(); while (true) {} }`, 1000)
      const offered = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
      const oldRelease = owner.borrowDeadline(10000)
      const instance = await owner.callable('construct', offered, { role: 'execution' })
      const callback = next(owner, instance).then(() => false, () => true)
      const site = await owner.park()
      const started = performance.now()
      await owner.resume(site, null, { abandon: true, budgetMs: 350 })
      const fixed = owner.deadline.current
      oldRelease()
      oldRelease()
      const cleanupRelease = owner.borrowDeadline(30000)
      owner.resetDeadline(Date.now() + 60000)
      cleanupRelease()
      cleanupRelease()
      assert.equal(owner.deadline.current, fixed)
      const state = await bounded(owner.closed.promise, 1500, 'FIXED_CLEANUP_CLOSE_TIMEOUT')
      assert.equal(await callback, true)
      assert.ok(performance.now() - started < 1400)
      assert.equal(fs.readFileSync(path.join(scratch, 'cleanup'), 'utf8'), 'actual')
      assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
      const result = await owner.result.promise
      assert.equal(result.kind, 'Abandoned')
      assert.equal(result.programSettled, false)
      assert.ok(owner.accepted.size > 0, 'accepted Host cat remains owned after the guest closes')
      await Promise.allSettled([...owner.accepted])
      assert.equal(owner.accepted.size, 0)
    })
  })

  test('single owner derived membrane keeps constructor reachability and captured WeakMap boundary', async () => {
    await withOwner({}, async owner => {
      fs.writeFileSync(path.join(owner.directory, 'scratch', 'seed'), 'actual')
      await owner.start(`const observations = [];
        const probe = value => { try { return value.constructor.constructor('return typeof process')(); } catch (_) { return 'blocked'; } };
        observations.push(probe(this.write));
        observations.push(probe(await this.cat('seed')));
        const original = WeakMap.prototype.get;
        let leaked = false;
        WeakMap.prototype.get = function(key) { leaked = true; return Reflect.apply(original, this, [key]); };
        this.write('inside', 'actual');
        return { observations, leaked, globalProcess: typeof process };`, 1500)
      const result = await bounded(owner.result.promise, 2000, 'MEMBRANE_RESULT_TIMEOUT')
      assert.equal(result.kind, 'Result')
      assert.equal(result.value.globalProcess, 'undefined')
      assert.equal(result.value.leaked, false)
      assert.ok(result.value.observations.every(value => value === 'undefined' || value === 'blocked'))
      const state = await bounded(owner.closed.promise, 1500, 'MEMBRANE_CLOSE_TIMEOUT')
      assert.deepEqual(state.exit, { code: 0, signal: null })
    })
  })
}
