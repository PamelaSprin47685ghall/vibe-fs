import test from 'node:test'
import assert from 'node:assert/strict'
import * as fs from 'node:fs'
import * as os from 'node:os'
import * as path from 'node:path'
import { GuestOwner, bounded, absent } from './vibe-fs-mf01-vertical-a-callable-20261021/owner.mjs'
import { FrameReader } from './vibe-fs-mf01-vertical-a-callable-20261021/wire.mjs'
import { ProcessSession } from './vibe-fs-mf01-vertical-a-callable-20261021/dependencies/ProcessSession.mjs'

const owned = []
const methods = `get maxDuration() { return 3000; } sanity() { return { nextDone: () => [], watchVeto: () => [] }; } next() { return null; } watch() { return false; }`
async function withOwner(options, run) {
  const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-callable-independent-')))
  let owner
  try {
    owner = await GuestOwner.create(process.execPath, directory, options)
    owned.push({ pid: owner.pid, runId: owner.runId, directory })
    await run(owner, path.join(directory, 'scratch'))
  } finally {
    if (owner) {
      const fd = owner.replyFd
      const state = await bounded(owner.dispose(), 7000, 'INDEPENDENT_CLOSE_TIMEOUT')
      assert.equal(state.phase, 'Closed')
      assert.equal(state.failure, null)
      assert.equal(absent(owner.pid), true)
      assert.equal(owner.accepted.size, 0)
      assert.equal(owner.callablePending.size, 0)
      assert.equal(fs.existsSync(owner.fifoPath), false)
      assert.throws(() => fs.fstatSync(fd), { code: 'EBADF' })
      console.log(JSON.stringify({ evidence: 'owned-cleanup', runtime: process.versions.bun ? 'Bun' : process.versions.node, pid: owner.pid, exit: state.exit, phase: state.phase, absent: absent(owner.pid), faults: owner.transportFaults }))
    }
    fs.rmSync(directory, { recursive: true, force: true })
  }
}
async function offer(owner) { return await bounded(owner.nextOffer(), 1500, 'OFFER_TIMEOUT') }
async function finish(owner, ref, value) {
  await owner.callable('finishOffer', ref, { value })
  const result = await bounded(owner.result.promise, 1500, 'RESULT_TIMEOUT')
  assert.deepEqual(result, { kind: 'Result', value })
  await bounded(owner.closed.promise, 1500, 'NORMAL_CLOSE_TIMEOUT')
}

test('independent raw sanity array retains holes, function, object identity, undefined, and exact proof receiver', async () => {
  await withOwner({}, async (owner, scratch) => {
    await owner.start(`const program = this; const lexical = { value: 41 }; let constructions = 0;
      class Work {
        constructor() { this.id = ++constructions; this.identity = lexical; this.calls = 0; }
        get maxDuration() { return 3000; }
        sanity() {
          const args = [lexical, lexical, () => lexical, undefined, , -0];
          args.extra = () => 'must remain local';
          return { nextDone: function() { if (this !== undefined) throw new Error('thunk receiver'); return args; }, watchVeto: () => args };
        }
        next(first, second, callback, missing, hole, zero) {
          if (this.id !== 1 || first !== this.identity || first !== second || callback() !== first || missing !== undefined || hole !== undefined || !Object.is(zero, -0) || arguments.length !== 6) throw new Error('raw args changed');
          this.calls++; first.value++; program.write('proof', String(first.value)); return null;
        }
        watch(first, second, callback) {
          if (this.id !== 1 || this.calls !== 1 || first !== this.identity || first !== second || callback() !== first || first.value !== 42) throw new Error('proof receiver changed');
          return { abort: 0 };
        }
        onExit() { return { id: this.id, calls: this.calls, value: this.identity.value }; }
      }
      return await this.offerCallable(Work);`, 4000)
    const ref = await offer(owner)
    const proof = await owner.callable('construct', ref, { role: 'proof' })
    const pair = await owner.callable('sanity', proof)
    for (const [method, thunk] of [['next', pair.nextDone], ['watch', pair.watchVeto]]) {
      assert.deepEqual(await owner.callable('invokeThunkAndApplyProof', proof, { method, thunk }), { argumentArray: true, accepted: true })
    }
    assert.equal(fs.readFileSync(path.join(scratch, 'proof'), 'utf8'), '42')
    const execution = await owner.callable('construct', ref, { role: 'execution' })
    assert.deepEqual(await owner.callable('callMethod', execution, { method: 'onExit', context: null }), { id: 2, calls: 0, value: 42 })
    await owner.callable('release', proof)
    assert.throws(() => owner.callable('invokeThunkAndApplyProof', proof, { method: 'next', thunk: pair.nextDone }), /CALLABLE_REF_INVALID/)
    await finish(owner, ref, 'raw-preserved')
  })
})

test('independent watch veto uses own non-nullish abort, including falsy values, without inherited-field coercion', async () => {
  await withOwner({}, async owner => {
    await owner.start(`let reads = 0; let index = 0; const values = [false, { abort: false }, { abort: 0 }, { abort: '' }, { abort: null }, { abort: undefined }, Object.create({ abort: 'inherited' }), { get abort() { reads++; return 'own'; } }, undefined, true];
      class Work { ${methods} watch() { return values[index++]; } onExit() { return { reads, index }; } }
      return await this.offerCallable(Work);`, 4000)
    const ref = await offer(owner)
    const proof = await owner.callable('construct', ref, { role: 'proof' })
    const pair = await owner.callable('sanity', proof)
    for (const accepted of [true, true, true, true, false, false, false, true, false, false]) {
      assert.deepEqual(await owner.callable('invokeThunkAndApplyProof', proof, { method: 'watch', thunk: pair.watchVeto }), { argumentArray: true, accepted })
    }
    assert.deepEqual(await owner.callable('callMethod', proof, { method: 'onExit', context: null }), { reads: 1, index: 10 })
    await finish(owner, ref, 'veto-values')
  })
})

test('independent constructor, method getter, thenable and toJSON perform their effects through the real guest RPC', async () => {
  await withOwner({}, async (owner, scratch) => {
    await owner.start(`const program = this; let order = '';
      class Work {
        constructor() { order += 'C'; program.write('constructor', order); this.receiver = 41; }
        get maxDuration() { order += 'S'; program.write('metadata', order); return 3000; }
        sanity() { return { nextDone: () => [], watchVeto: () => [] }; }
        watch() { return false; }
        get next() { order += 'G'; program.write('method-getter', order); return function() {
          if (this.receiver !== 41) throw new Error('wrong receiver');
          return { get then() { order += 'T'; program.write('thenable', order); return resolve => resolve({ toJSON() { order += 'J'; program.write('toJSON', order); return { order, receiver: 42 }; } }); } };
        }; }
      }
      return await this.offerCallable(Work);`, 4000)
    const ref = await offer(owner)
    const execution = await owner.callable('construct', ref, { role: 'execution' })
    assert.equal(fs.readFileSync(path.join(scratch, 'constructor'), 'utf8'), 'C')
    await owner.callable('readSpec', execution)
    assert.equal(fs.readFileSync(path.join(scratch, 'metadata'), 'utf8'), 'CS')
    const result = await owner.callable('callMethod', execution, { method: 'next', context: null })
    assert.equal(result.receiver, 42)
    assert.match(result.order, /^CSGGTJ$/)
    assert.equal(fs.readFileSync(path.join(scratch, 'toJSON'), 'utf8'), result.order)
    assert.equal(owner.apis.records.filter(entry => entry.operation === 'write').length, 6)
    await finish(owner, ref, 'callbacks-local')
  })
})

test('independent callback errors and invalid passive results retain their family and do not close a live offer', async () => {
  await withOwner({}, async owner => {
    await owner.start(`class Work { ${methods}
        next(ctx) {
          if (ctx === 'declared') { const error = new Error('declared failure'); error.__jsFailure = { code: 'MF01_DECLARED', reason: 'kept local' }; throw error; }
          if (ctx === 'exception') { const error = new Error('ordinary failure'); error.code = 'MF01_EXCEPTION'; throw error; }
          if (ctx === 'cycle') { const value = {}; value.self = value; return value; }
          return { get value() { const error = new Error('result getter'); error.code = 'MF01_RESULT_GETTER'; throw error; } };
        }
      } return await this.offerCallable(Work);`, 4000)
    const ref = await offer(owner)
    const execution = await owner.callable('construct', ref, { role: 'execution' })
    for (const [context, code, family] of [['declared', 'MF01_DECLARED', 'JsFailure'], ['exception', 'MF01_EXCEPTION', 'Exception'], ['cycle', 'CALL_RESULT_INVALID', 'JsFailure'], ['getter', 'MF01_RESULT_GETTER', 'Exception']]) {
      await assert.rejects(owner.callable('callMethod', execution, { method: 'next', context }), error => error.code === code && error.family === family)
      assert.equal(owner.phase, 'Active')
    }
    await finish(owner, ref, 'error-families')
  })
})

test('independent lost finish remains once and stale or cross-run references cannot consume a successor offer', async () => {
  await withOwner({ dropCallableReplyFor: 'finishOffer' }, async (old, oldScratch) => {
    await old.start(`class Work { ${methods} } const first = await this.offerCallable(Work); this.write('receipt', first); const second = await this.offerCallable(Work); return { first, second };`, 5000)
    const first = await offer(old)
    const proof = await old.callable('construct', first, { role: 'proof' })
    const lost = old.callable('finishOffer', first, { value: 'once' }).then(value => ({ value }), error => ({ error: error.message }))
    const second = await offer(old)
    const before = old.commandSequence
    for (const invalid of [first, { ...second }]) assert.throws(() => old.callable('finishOffer', invalid, { value: 'redo' }), /CALLABLE_REF_INVALID/)
    assert.equal(old.commandSequence, before)
    await assert.rejects(old.callable('readSpec', proof), error => error.code === 'CALLABLE_REF_INVALID')
    await withOwner({}, async fresh => {
      await fresh.start(`class Work { ${methods} } return await this.offerCallable(Work);`, 3000)
      const current = await offer(fresh)
      assert.equal(current.id, first.id)
      const sequence = fresh.commandSequence
      for (const invalid of [first, second, proof, { ...current }]) assert.throws(() => fresh.callable('finishOffer', invalid, { value: 'cross' }), /CALLABLE_REF_INVALID/)
      assert.equal(fresh.commandSequence, sequence)
      await finish(fresh, current, 'fresh')
    })
    assert.equal(fs.readFileSync(path.join(oldScratch, 'receipt'), 'utf8'), 'once')
    assert.equal(old.apis.records.filter(entry => entry.operation === 'write').length, 1)
    await old.callable('finishOffer', second, { value: 'second' })
    assert.deepEqual(await bounded(old.result.promise, 1500, 'OLD_RESULT_TIMEOUT'), { kind: 'Result', value: { first: 'once', second: 'second' } })
    assert.equal(typeof (await lost).error, 'string')
    await bounded(old.closed.promise, 1500, 'OLD_CLOSE_TIMEOUT')
  })
})

test('independent onExit callback park retains the original instance and refuses copied or consumed site', async () => {
  await withOwner({}, async (owner, scratch) => {
    await owner.start(`const program = this; class Work { ${methods} constructor() { this.keep = 41; }
      async onExit(ctx) { const value = await program.park({ reason: ctx.reason, keep: this.keep }); program.write('exit', String(++this.keep)); return { keep: this.keep, value }; }
      } return await this.offerCallable(Work);`, 4000)
    const ref = await offer(owner)
    const execution = await owner.callable('construct', ref, { role: 'execution' })
    const pending = owner.callable('callMethod', execution, { method: 'onExit', context: { reason: 'done' } })
    const observed = pending.then(value => ({ value }), error => ({ error: error.message }))
    const site = await owner.park()
    assert.deepEqual(site.payload, { reason: 'done', keep: 41 })
    await assert.rejects(() => owner.resume({ ...site }, 'copy'), /HANDLE_NOT_PARKED/)
    await owner.resume(site, 'original')
    assert.deepEqual(await bounded(observed, 1500, 'EXIT_CALLBACK_TIMEOUT'), { value: { keep: 42, value: 'original' } })
    await assert.rejects(() => owner.resume(site, 'again'), /HANDLE_NOT_PARKED/)
    assert.equal(fs.readFileSync(path.join(scratch, 'exit'), 'utf8'), '42')
    await finish(owner, ref, 'exit-finished')
  })
})

test('independent real Closing result ordering is preserved with Callable registration installed', async () => {
  const tap = new FrameReader()
  let owner
  let observed
  const sessionFactory = options => {
    const real = new ProcessSession(options, 'Capture')
    return new Proxy(real, { get(target, key) {
      if (key === 'takeOutput') return async () => {
        const output = await target.takeOutput()
        if (output.kind === 'Chunk' && output.channel === 'stdout') for (const message of tap.push(output.bytes)) {
          if (message.kind === 'result') {
            observed = { before: owner.phase, native: target.inspect().phase, absent: absent(owner.pid) }
            owner.kill()
            observed.after = owner.phase
          }
        }
        return output
      }
      const value = target[key]
      return typeof value === 'function' ? value.bind(target) : value
    } })
  }
  await withOwner({ sessionFactory }, async created => {
    owner = created
    await owner.start("this.write('once', 'actual'); return 'late';", 2000)
    const result = await bounded(owner.result.promise, 1500, 'CLOSING_RESULT_TIMEOUT')
    assert.equal(observed.after, 'Closing')
    assert.equal(result.kind, 'Closed')
    console.log(JSON.stringify({ evidence: 'real-closing-result', runtime: process.versions.bun ? 'Bun' : process.versions.node, pid: owner.pid, observed, published: result.kind }))
    await assert.rejects(owner.nextOffer(), /OWNER_REVOKED/)
  })
})

test.after(() => {
  for (const entry of owned) {
    assert.equal(absent(entry.pid), true)
    assert.equal(fs.existsSync(entry.directory), false)
  }
  console.log(JSON.stringify({ evidence: 'all-owned-absent', runtime: process.versions.bun ? 'Bun' : process.versions.node, owned }))
})
