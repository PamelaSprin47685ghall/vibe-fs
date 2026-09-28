import assert from 'node:assert/strict'
import test from 'node:test'
import { hideReviewContract as hide, restoreReviewContract as restore, wrapReviewExecutors } from '../../../../dist/OpenCode/Host/PluginHooksSurface.js'

test('WHAT[host-boundary-032] production cleanup restores the original object values descriptors and key order', () => {
  for (const contract of [undefined, null, false, 17, {}, [], '', 'unrecognized']) {
    const args = { first: 'first', contract, last: 'last' }
    const original = Object.getOwnPropertyDescriptors(args)
    const keys = Reflect.ownKeys(args)
    const same = args
    hide(args)
    hide(args)
    assert.equal(Object.hasOwn(args, 'contract'), false)
    assert.deepEqual(Object.keys(args), ['first', 'last'])
    restore(args)
    restore(args)
    assert.equal(args, same)
    assert.deepEqual(Object.getOwnPropertyDescriptors(args), original)
    assert.deepEqual(Reflect.ownKeys(args), keys)
    assert.equal(args.contract, contract)
  }
})

test('WHAT[host-boundary-032] cleanup preserves non-enumerable accessors without evaluating them', () => {
  const args = { contract: 'original' }
  let reads = 0
  Object.defineProperty(args, 'detail', { get() { reads += 1; return 'detail' }, enumerable: false, configurable: true })
  const original = Object.getOwnPropertyDescriptors(args)
  hide(args)
  restore(args)
  assert.deepEqual(Object.getOwnPropertyDescriptors(args), original)
  assert.deepEqual(Object.getOwnPropertyNames(args), ['contract', 'detail'])
  assert.equal(reads, 0)
})

test('WHAT[host-boundary-032] missing or trailing contracts do not disturb existing fixed properties', () => {
  for (const withContract of [false, true]) {
    const args = {}
    Object.defineProperty(args, 'fixed', { value: 'fixed', enumerable: true, configurable: false })
    if (withContract) args.contract = 'original'
    const original = Object.getOwnPropertyDescriptors(args)
    const keys = Reflect.ownKeys(args)
    hide(args)
    restore(args)
    assert.deepEqual(Object.getOwnPropertyDescriptors(args), original)
    assert.deepEqual(Reflect.ownKeys(args), keys)
  }
})

test('WHAT[host-boundary-032] cleanup refuses an unreorderable object before deleting its contract', () => {
  const args = { contract: 'original' }
  Object.defineProperty(args, 'fixed', { value: 'fixed', enumerable: true, configurable: false })
  const original = Object.getOwnPropertyDescriptors(args)
  assert.throws(() => hide(args), TypeError)
  assert.deepEqual(Object.getOwnPropertyDescriptors(args), original)
  assert.deepEqual(Reflect.ownKeys(args), ['contract', 'fixed'])
})

test('WHAT[host-boundary-032] failed restoration preserves business fields and the saved original when contract becomes fixed after hide', () => {
  const last = { value: 'business input' }
  const args = { first: 'first', contract: 'original', last }
  const same = args
  hide(args)
  Object.defineProperty(args, 'contract', { value: 'downstream value', configurable: false })
  const beforeRestore = Object.getOwnPropertyDescriptors(args)
  const keys = Reflect.ownKeys(args)
  assert.equal(Object.getOwnPropertySymbols(args).length, 1, 'the original is still privately saved')
  for (let attempt = 0; attempt < 2; attempt++) {
    assert.throws(() => restore(args), TypeError)
    assert.equal(args, same)
    assert.equal(args.last, last)
    assert.deepEqual(Object.getOwnPropertyDescriptors(args), beforeRestore)
    assert.deepEqual(Reflect.ownKeys(args), keys)
  }
})

test('WHAT[host-boundary-032] the registered executor hides only its own hint and restores the original arguments when it settles', async () => {
  const args = { first: 'first', contract: false, last: 'last' }
  const original = Object.getOwnPropertyDescriptors(args)
  const context = { sessionID: 'same-session' }
  const result = { output: 'actual-result' }
  const untouched = { execute: () => 'untouched' }
  let finish
  const pending = new Promise((resolve) => { finish = resolve })
  let observed
  const tool = { execute(received, receivedContext) {
    observed = { same: received === args, sameContext: receivedContext === context, sameTool: this === tool,
      hasContract: Object.hasOwn(received, 'contract'), keys: Object.keys(received), values: { ...received } }
    return pending
  } }
  const tools = { 'js-manager': tool, 'js-engineer': untouched }
  wrapReviewExecutors(tools)
  assert.equal(tools['js-engineer'], untouched)
  const running = tool.execute(args, context)
  assert.deepEqual(observed, { same: true, sameContext: true, sameTool: true, hasContract: false,
    keys: ['first', 'last'], values: { first: 'first', last: 'last' } })
  assert.equal(Object.hasOwn(args, 'contract'), false, 'arguments stay hidden while the actual executor remains pending')
  finish(result)
  assert.equal(await running, result)
  assert.deepEqual(Object.getOwnPropertyDescriptors(args), original)
  assert.deepEqual(Object.keys(args), ['first', 'contract', 'last'])
})

test('WHAT[host-boundary-032] synchronous throws and asynchronous rejection preserve the original error and restore the same arguments', async () => {
  for (const asynchronous of [false, true]) {
    const error = new Error(asynchronous ? 'async-rejection' : 'sync-throw')
    const args = { first: 1, contract: 'original', last: 2 }
    const original = Object.getOwnPropertyDescriptors(args)
    let entered = false
    const tool = { execute(received) {
      assert.equal(received, args)
      assert.equal(Object.hasOwn(received, 'contract'), false)
      entered = true
      if (asynchronous) return Promise.reject(error)
      throw error
    } }
    wrapReviewExecutors({ 'js-manager': tool })
    await assert.rejects(() => tool.execute(args, {}), (actual) => actual === error)
    assert.equal(entered, true)
    assert.deepEqual(Object.getOwnPropertyDescriptors(args), original)
    assert.deepEqual(Object.keys(args), ['first', 'contract', 'last'])
  }
})

test('WHAT[host-boundary-032] abort restores the same arguments only after the actual executor rejects', async () => {
  const controller = new AbortController()
  const args = { first: 1, contract: 'original', last: 2 }
  const original = Object.getOwnPropertyDescriptors(args)
  const error = new Error('executor-aborted')
  let entered
  const started = new Promise((resolve) => { entered = resolve })
  let executorSettled = false
  const tool = { execute(received, context) {
    assert.equal(received, args)
    assert.equal(Object.hasOwn(received, 'contract'), false)
    return new Promise((_resolve, reject) => {
      context.abort.addEventListener('abort', () => { executorSettled = true; reject(error) }, { once: true })
      entered()
    })
  } }
  wrapReviewExecutors({ 'js-manager': tool })
  const running = tool.execute(args, { abort: controller.signal })
  const rejected = assert.rejects(running, (actual) => actual === error)
  await started
  assert.equal(executorSettled, false)
  assert.equal(Object.hasOwn(args, 'contract'), false)
  controller.abort()
  await rejected
  assert.equal(executorSettled, true)
  assert.deepEqual(Object.getOwnPropertyDescriptors(args), original)
  assert.deepEqual(Object.keys(args), ['first', 'contract', 'last'])
})

test('WHAT[host-boundary-032] missing or invalid registered review executors fail instead of silently bypassing cleanup', () => {
  for (const tools of [undefined, null, {}, { 'js-manager': {} }, { 'js-manager': { execute: 'not-callable' } }]) {
    assert.throws(() => wrapReviewExecutors(tools), /Registered/)
  }
})

test('WHAT[host-boundary-032] execution and restoration failures are both preserved, while a sole restoration error remains a failure', async () => {
  for (const executionFails of [false, true]) {
    const originalError = new Error('actual executor rejection')
    const args = { first: 1, contract: 'original', last: 2 }
    let beforeRestoration
    const tool = { execute(received) {
      assert.equal(received, args)
      Object.defineProperty(received, 'contract', { value: 'incompatible downstream value', configurable: false })
      beforeRestoration = Object.getOwnPropertyDescriptors(received)
      return executionFails ? Promise.reject(originalError) : Promise.resolve('executed')
    } }
    wrapReviewExecutors({ 'js-manager': tool })
    await assert.rejects(() => tool.execute(args, {}), (error) => {
      if (executionFails) {
        assert.ok(error instanceof AggregateError)
        assert.equal(error.errors.length, 2)
        assert.equal(error.errors[0], originalError)
        assert.ok(error.errors[1] instanceof TypeError)
        assert.equal(error.cause, originalError)
      } else {
        assert.ok(error instanceof TypeError)
      }
      return true
    })
    assert.deepEqual(Object.getOwnPropertyDescriptors(args), beforeRestoration)
    assert.equal(Object.getOwnPropertySymbols(args).length, 1, 'failed restoration retains the actual original privately')
  }
})
