import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const context = (sessionID, callID) => ({ tool: 'js-manager', sessionID, callID })
const complete = (hooks, input, args, output = {}) =>
  hooks['tool.execute.after']({ ...input, args }, { title: 'js-manager', output: '', metadata: {}, ...output })

test('WHAT[host-boundary-032] missing and malformed hints do not reject a permitted call and preserve original arguments', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const session = 'hint-admission'
    await openIncumbency(runtime, session)
    const cases = [
      { path: 'file.txt' },
      { contract: undefined, path: 'file.txt' },
      { a: 1, contract: null, b: 2 },
      ...[false, 17, {}, [], '', 'unrecognized'].map((contract) => ({ a: 1, contract, b: 2 })),
      { a: 1, b: 2, contract: 'last' },
    ]
    for (const [index, args] of cases.entries()) {
      const expected = { ...args }
      const keys = Object.keys(args)
      const input = context(session, 'call-' + index)
      const output = { args }
      await hooks['tool.execute.before'](input, output)
      assert.equal(output.args, args)
      assert.equal(Object.hasOwn(args, 'contract'), false)
      assert.deepEqual(Object.fromEntries(Object.entries(args)), Object.fromEntries(Object.entries(expected).filter(([key]) => key !== 'contract')))
      await complete(hooks, input, output.args)
      assert.equal(output.args, args)
      assert.deepEqual(args, expected)
      assert.deepEqual(Object.keys(args), keys)
    }
  })
})

test('WHAT[host-boundary-032] repeated callbacks preserve the first original value and cannot use provider pseudo-private fields', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await openIncumbency(runtime, 'hint-repeat')
    const input = context('hint-repeat', 'repeat')
    const args = { contract: 'original', _contract: 'untrusted', __contract: 'untrusted-2', path: 'file.txt' }
    const expected = { ...args }
    const output = { args }
    await hooks['tool.execute.before'](input, output)
    await hooks['tool.execute.before'](input, output)
    assert.equal(Object.hasOwn(args, 'contract'), false)
    await complete(hooks, input, args)
    await complete(hooks, input, args)
    assert.deepEqual(args, expected)
    assert.deepEqual(Object.keys(args), Object.keys(expected))
  })
})

test('WHAT[host-boundary-032] concurrent calls restore their own values even with a shared call ID across sessions', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const calls = [
      { input: context('hint-a', 'same-call'), args: { contract: 'first', path: 'a.txt' } },
      { input: context('hint-b', 'same-call'), args: { path: 'b.txt', contract: 'second' } },
    ]
    const expected = calls.map(({ args }) => ({ ...args }))
    await Promise.all(calls.map(({ input }) => openIncumbency(runtime, input.sessionID)))
    await Promise.all(calls.map(({ input, args }) => hooks['tool.execute.before'](input, { args })))
    for (const { args } of calls) assert.equal(Object.hasOwn(args, 'contract'), false)
    for (const { input, args } of [...calls].reverse()) await complete(hooks, input, args)
    assert.deepEqual(calls.map(({ args }) => args), expected)
    assert.deepEqual(calls.map(({ args }) => Object.keys(args)), expected.map(Object.keys))
  })
})

test('WHAT[host-boundary-032] an after callback with a reported failure restores the same arguments', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await openIncumbency(runtime, 'hint-failure')
    const input = context('hint-failure', 'failed-call')
    const args = { contract: 'original', path: 'missing.txt' }
    const expected = { ...args }
    await hooks['tool.execute.before'](input, { args })
    await complete(hooks, input, args, { output: 'read failed', metadata: { error: 'fixture-reported-failure' } })
    assert.deepEqual(args, expected)
    assert.deepEqual(Object.keys(args), Object.keys(expected))
  })
})

test.todo('WHAT[host-boundary-032] actual execution and downstream-hook exceptions must reach same-call restoration; manually calling after does not prove that route')

integrationTest('WHAT[host-boundary-032] a real Host preserves argument identity, order and durable input across success, failure and cancellation', () => {
  const runner = fileURLToPath(new URL('./support/run-manager-review-tools-canary.mjs', import.meta.url))
  const root = fileURLToPath(new URL('../../..', import.meta.url))
  const result = spawnSync(process.execPath, [runner], { cwd: root, encoding: 'utf8', timeout: 120000 })
  assert.equal(result.error, undefined)
  assert.equal(result.signal, null)
  assert.equal(result.status, 0, result.stderr + '\n' + result.stdout)
  const evidence = JSON.parse(result.stdout)
  assert.ok(evidence.versions.opencode)
  assert.ok(evidence.versions.plugin)
  for (const name of ['normal', 'executorError', 'cancellation']) {
    const observed = evidence.calls[name]
    assert.equal(observed.sameArguments, true, name)
    assert.equal(observed.originalOrder, true, name)
    assert.equal(observed.originalValues, true, name)
    assert.equal(observed.durableInputRetainsContract, true, name)
    assert.equal(observed.originalDurableInput, true, name)
  }
  assert.equal(evidence.calls.normal.status, 'completed')
  assert.equal(evidence.calls.executorError.status, 'error')
  assert.equal(evidence.calls.cancellation.status, 'error')
  assert.equal(evidence.historicalToolCallPreservesContract, true)
})
