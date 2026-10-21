// Isolated mechanism tests. Real scratch fs; no production shell or ledger.
import test from 'node:test'
import assert from 'node:assert/strict'
import * as fs from 'node:fs'
import * as os from 'node:os'
import * as path from 'node:path'
import { GuestOwner, bounded, absent } from './owner.mjs'

async function withOwner(options, run) {
  const directory = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-callable-')))
  let owner
  try {
    owner = await GuestOwner.create(process.execPath, directory, options)
    await run(owner, path.join(directory, 'scratch'))
  } finally {
    if (owner) {
      const state = await owner.dispose()
      assert.equal(state.phase, 'Closed')
      assert.equal(state.failure, null)
      assert.equal(absent(owner.pid), true)
      console.log(JSON.stringify({ runtime: process.versions.bun ? 'Bun' : process.versions.node, pid: owner.pid, phase: state.phase, exit: state.exit, watchdog: owner.watchdogFired, faults: owner.transportFaults }))
    }
    fs.rmSync(directory, { recursive: true, force: true })
  }
}

const methods = `get maxDuration() { return 2000; }
  sanity() { return { nextDone: () => [], watchVeto: () => [] }; }
  next() { return null; }
  watch() { return false; }`

async function normalResult(owner, value) {
  assert.deepEqual(await bounded(owner.result.promise, 3000, 'RESULT_TIMEOUT'), { kind: 'Result', value })
  const state = await bounded(owner.closed.promise, 3000, 'NORMAL_CLOSE_TIMEOUT')
  assert.deepEqual(state.exit, { code: 0, signal: null })
}

test('Callable Host explicitly constructs separate proof/execution; raw closure args and receiver remain guest', async () => {
  await withOwner({}, async (owner, scratch) => {
    fs.writeFileSync(path.join(scratch, 'input'), 'async-value')
    await owner.start(`const program = this; let constructed = 0; const lexical = 41;
      class Work {
        constructor() { this.identity = ++constructed; this.calls = 0; }
        get maxDuration() { return 2000; }
        sanity() { return { nextDone: () => [() => lexical, undefined], watchVeto: () => [() => lexical] }; }
        async next(callback, missing) {
          this.calls++;
          if (typeof callback === 'function') return callback() === 41 && missing === undefined ? null : 'bad';
          const old = await program.cat('input');
          program.write('output', old + ':' + lexical);
          return { id: this.identity, calls: this.calls, old, get captured() { return lexical; }, toJSON() { return { id: this.id, calls: this.calls, old: this.old, captured: this.captured }; } };
        }
        watch(callback) { this.calls++; return callback() !== lexical; }
        async onExit(ctx) { await program.cat('output'); program.write('exit', String(this.identity)); return ctx; }
      }
      return await this.offerCallable(Work);`, 5000)
    const offered = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
    assert.equal(owner.apis.records.length, 0, 'registration does not drive callbacks')
    const proof = await owner.callable('construct', offered, { role: 'proof' })
    const spec = await owner.callable('readSpec', proof)
    assert.equal(spec.maxDuration, 2000)
    assert.equal(spec.hasOnExit, true)
    const pair = await owner.callable('sanity', proof)
    for (const [method, thunk] of [['next', pair.nextDone], ['watch', pair.watchVeto]]) {
      assert.deepEqual(await owner.callable('invokeThunkAndApplyProof', proof, { method, thunk }), { argumentArray: true, accepted: true })
    }
    const execution = await owner.callable('construct', offered, { role: 'execution' })
    assert.notEqual(execution.id, proof.id)
    assert.deepEqual(await owner.callable('callMethod', execution, { method: 'next', context: null }), { id: 2, calls: 1, old: 'async-value', captured: 41 })
    assert.deepEqual(await owner.callable('callMethod', execution, { method: 'onExit', context: { reason: 'done' } }), { reason: 'done' })
    assert.equal(fs.readFileSync(path.join(scratch, 'output'), 'utf8'), 'async-value:41')
    assert.equal(fs.readFileSync(path.join(scratch, 'exit'), 'utf8'), '2')
    await owner.callable('release', proof)
    assert.throws(() => owner.callable('readSpec', proof), /CALLABLE_REF_INVALID/)
    await owner.callable('finishOffer', offered, { value: 'finished' })
    await normalResult(owner, 'finished')
  })
})

test('Callable readSpec preserves missing/invalid failures without function or Proxy JSON coercion', async () => {
  await withOwner({}, async owner => {
    await owner.start(`const bad = [undefined, () => 100, new Proxy({}, { get() { throw new Error('must not coerce'); } })];
      for (const value of bad) { class Work { ${methods} get maxDuration() { return value; } } await this.offerCallable(Work); }
      class BadBudget { ${methods} get outputBudgetBytes() { return () => 2; } }
      await this.offerCallable(BadBudget);
      class BadLock { ${methods} get worldLock() { return {}; } }
      await this.offerCallable(BadLock);
      return 'done';`, 5000)
    for (const code of ['CALLABLE_SPEC_INCOMPLETE', 'CALLABLE_SPEC_INCOMPLETE', 'CALLABLE_SPEC_INCOMPLETE', 'CALL_OUTPUT_BUDGET_INVALID', 'CALL_WORLD_LOCK_INVALID']) {
      const offered = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
      const proof = await owner.callable('construct', offered, { role: 'proof' })
      await assert.rejects(owner.callable('readSpec', proof), error => error.code === code && error.family === 'JsFailure')
      await owner.callable('finishOffer', offered, { value: null })
    }
    await normalResult(owner, 'done')
  })
})

test('Callable proof accepts object veto and evaluates abort getter only inside guest', async () => {
  await withOwner({}, async owner => {
    await owner.start(`class Work { ${methods} watch() { return { get abort() { return 'veto'; } }; } } return await this.offerCallable(Work);`, 3000)
    const offered = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
    const proof = await owner.callable('construct', offered, { role: 'proof' })
    const pair = await owner.callable('sanity', proof)
    assert.deepEqual(await owner.callable('invokeThunkAndApplyProof', proof, { method: 'watch', thunk: pair.watchVeto }), { argumentArray: true, accepted: true })
    await owner.callable('finishOffer', offered, { value: 'object veto' })
    await normalResult(owner, 'object veto')
  })
})

test('Callable stale/forged refs and wrong proof thunks cannot settle a fresh offer', async () => {
  await withOwner({}, async owner => {
    await owner.start(`class Work { ${methods} } await this.offerCallable(Work); return await this.offerCallable(Work);`, 5000)
    const first = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
    assert.throws(() => owner.callable('construct', { ...first }, { role: 'proof' }), /CALLABLE_REF_INVALID/)
    const a = await owner.callable('construct', first, { role: 'proof' })
    const b = await owner.callable('construct', first, { role: 'proof' })
    const pair = await owner.callable('sanity', a)
    await assert.rejects(owner.callable('invokeThunkAndApplyProof', b, { method: 'next', thunk: pair.nextDone }), error => error.code === 'CALLABLE_PROOF_FENCE')
    await owner.callable('finishOffer', first, { value: null })
    const second = await bounded(owner.nextOffer(), 2000, 'SECOND_OFFER_TIMEOUT')
    assert.throws(() => owner.callable('finishOffer', first, { value: 'stale' }), /CALLABLE_REF_INVALID/)
    await assert.rejects(owner.callable('readSpec', a), error => error.code === 'CALLABLE_REF_INVALID')
    await owner.callable('finishOffer', second, { value: 'second' })
    await normalResult(owner, 'second')
  })
})

test('Callable accepted method with lost reply has one actual mutation and no replay after close', async () => {
  await withOwner({ dropCallableReplyFor: 'callMethod' }, async (owner, scratch) => {
    await owner.start(`const program = this; class Work { ${methods} next() { program.write('once', 'actual'); return null; } } return await this.offerCallable(Work);`, 500)
    const offered = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
    const execution = await owner.callable('construct', offered, { role: 'execution' })
    const callback = owner.callable('callMethod', execution, { method: 'next', context: null }).then(() => false, () => true)
    const state = await bounded(owner.closed.promise, 2000, 'LOST_REPLY_CLOSE_TIMEOUT')
    assert.equal(await callback, true)
    assert.equal(fs.readFileSync(path.join(scratch, 'once'), 'utf8'), 'actual')
    assert.equal(owner.apis.records.filter(record => record.operation === 'write').length, 1)
    assert.equal(owner.droppedCallableReply.ok, true)
    assert.equal(owner.watchdogFired, true)
    assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
    assert.throws(() => owner.callable('callMethod', execution, { method: 'next', context: null }), /OWNER_REVOKED/)
  })
})

test('Callable reference from a closed physical run cannot address a new run using the same local id', async () => {
  let retired
  await withOwner({}, async owner => {
    await owner.start(`class Work { ${methods} } return await this.offerCallable(Work);`, 3000)
    retired = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
  })
  await withOwner({}, async owner => {
    await owner.start(`class Work { ${methods} } return await this.offerCallable(Work);`, 3000)
    const current = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
    assert.equal(current.id, retired.id)
    assert.notEqual(current.runId, retired.runId)
    assert.throws(() => owner.callable('construct', retired, { role: 'proof' }), /CALLABLE_REF_INVALID/)
    await owner.callable('finishOffer', current, { value: 'current' })
    await normalResult(owner, 'current')
  })
})

test('Callable lost finish confirmation consumes the exact offer once and cannot settle its successor', async () => {
  await withOwner({ dropCallableReplyFor: 'finishOffer' }, async (owner, scratch) => {
    await owner.start(`class Work { ${methods} } const value = await this.offerCallable(Work); this.write('first', value); return await this.offerCallable(Work);`, 4000)
    const first = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
    const lost = owner.callable('finishOffer', first, { value: 'once' }).then(() => false, () => true)
    const second = await bounded(owner.nextOffer(), 2000, 'SECOND_OFFER_TIMEOUT')
    assert.equal(fs.readFileSync(path.join(scratch, 'first'), 'utf8'), 'once')
    assert.throws(() => owner.callable('finishOffer', first, { value: 'redo' }), /CALLABLE_REF_INVALID/)
    await owner.callable('finishOffer', second, { value: 'second' })
    await normalResult(owner, 'second')
    assert.equal(await lost, true, 'lost confirmation stays unknown to its caller')
    assert.equal(owner.apis.records.filter(record => record.operation === 'write').length, 1)
  })
})

test('Callable caught abandon keeps Abandoned and native finally performs actual Host write/rm', async () => {
  await withOwner({}, async (owner, scratch) => {
    fs.writeFileSync(path.join(scratch, 'remove'), 'present')
    await owner.start(`const program = this;
      class Work { ${methods} async next() { return await program.park('inside callback'); } }
      try { await this.offerCallable(Work); } catch (_) { }
      finally { this.write('finally', 'actual'); this.rm('remove'); const denied = await this.park('forbidden'); this.write('park-result', denied.code); }
      return 'caught must not restore success';`, 5000)
    const offered = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
    const execution = await owner.callable('construct', offered, { role: 'execution' })
    const callback = owner.callable('callMethod', execution, { method: 'next', context: null }).then(value => ({ value }), error => ({ error }))
    const site = await owner.park()
    await owner.resume(site, null, { abandon: true, budgetMs: 2000 })
    assert.equal((await bounded(owner.result.promise, 3000, 'ABANDON_TIMEOUT')).kind, 'Abandoned')
    await callback
    assert.equal(fs.readFileSync(path.join(scratch, 'finally'), 'utf8'), 'actual')
    assert.equal(fs.existsSync(path.join(scratch, 'remove')), false)
    assert.equal(fs.readFileSync(path.join(scratch, 'park-result'), 'utf8'), 'CANNOT_PARK_WHILE_ABANDONING')
    assert.throws(() => owner.callable('finishOffer', offered, { value: 'late' }), /OWNER_REVOKED/)
    const state = await bounded(owner.closed.promise, 2000, 'ABANDON_CLOSE_TIMEOUT')
    assert.deepEqual(state.exit, { code: 0, signal: null })
  })
})

test('Callable looping finally closes physically without claiming the program settled or cleanup completed', async () => {
  await withOwner({}, async (owner, scratch) => {
    await owner.start(`try { await this.park('cleanup'); } finally { this.write('entered', 'actual'); await Promise.resolve(); while (true) {} this.write('finished', 'unreachable'); }`, 3000)
    const site = await owner.park()
    await owner.resume(site, null, { abandon: true, budgetMs: 350 })
    const state = await bounded(owner.closed.promise, 2000, 'CLEANUP_CLOSE_TIMEOUT')
    const result = await owner.result.promise
    assert.equal(result.kind, 'Abandoned')
    assert.equal(result.programSettled, false)
    assert.equal(owner.businessSettled, false)
    assert.equal(owner.watchdogFired, true)
    assert.equal(fs.readFileSync(path.join(scratch, 'entered'), 'utf8'), 'actual')
    assert.equal(fs.existsSync(path.join(scratch, 'finished')), false)
    assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
  })
})

for (const [name, body, operation] of [
  ['constructor', `constructor() { while (true) {} } ${methods}`, 'construct'],
  ['getter', `${methods} get maxDuration() { while (true) {} }`, 'readSpec'],
  ['sanity thunk', `${methods} sanity() { return { nextDone: () => { while (true) {} }, watchVeto: () => [] }; }`, 'thunk'],
  ['watch abort getter', `${methods} watch() { return { get abort() { while (true) {} } }; }`, 'watch-thunk'],
  ['after-await method', `${methods} async next() { await Promise.resolve(); while (true) {} }`, 'method'],
  ['thenable assimilation', `${methods} next() { return { then() { while (true) {} } }; }`, 'method'],
  ['toJSON', `${methods} next() { return { toJSON() { while (true) {} } }; }`, 'method'],
]) {
  test('Callable external owner cuts off guest ' + name + ' without blocking Host', async () => {
    await withOwner({}, async owner => {
      let ticks = 0
      const timer = setInterval(() => ticks++, 10)
      try {
        await owner.start(`class Work { ${body} } return await this.offerCallable(Work);`, 350)
        const offered = await bounded(owner.nextOffer(), 2000, 'OFFER_TIMEOUT')
        let pending
        if (operation === 'construct') pending = owner.callable('construct', offered, { role: 'proof' })
        else {
          const proof = await owner.callable('construct', offered, { role: 'proof' })
          if (operation === 'readSpec') pending = owner.callable('readSpec', proof)
          if (operation === 'method') pending = owner.callable('callMethod', proof, { method: 'next', context: null })
          if (operation === 'thunk' || operation === 'watch-thunk') {
            const pair = await owner.callable('sanity', proof)
            pending = owner.callable('invokeThunkAndApplyProof', proof, operation === 'watch-thunk' ? { method: 'watch', thunk: pair.watchVeto } : { method: 'next', thunk: pair.nextDone })
          }
        }
        const rejected = pending.then(() => false, () => true)
        const state = await bounded(owner.closed.promise, 2000, 'WATCHDOG_CLOSE_TIMEOUT')
        assert.equal(await rejected, true)
        assert.equal(owner.watchdogFired, true)
        assert.ok(ticks >= 3)
        assert.deepEqual(state.exit, { code: null, signal: 'SIGKILL' })
      } finally { clearInterval(timer) }
    })
  })
}
