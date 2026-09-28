import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import test from 'node:test'
import { promisify } from 'node:util'
import { openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import './support/manager-review-contract.mjs'

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
      assert.deepEqual(args, expected)
      assert.deepEqual(Object.keys(args), keys)
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
    assert.deepEqual(args, expected)
    await complete(hooks, input, args)
    await complete(hooks, input, args)
    assert.deepEqual(args, expected)
    assert.deepEqual(Object.keys(args), Object.keys(expected))
  })
})

test('WHAT[host-boundary-032] admission hooks preserve each call input even with a shared call ID across sessions', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const calls = [
      { input: context('hint-a', 'same-call'), args: { contract: 'first', path: 'a.txt' } },
      { input: context('hint-b', 'same-call'), args: { path: 'b.txt', contract: 'second' } },
    ]
    const expected = calls.map(({ args }) => ({ ...args }))
    await Promise.all(calls.map(({ input }) => openIncumbency(runtime, input.sessionID)))
    await Promise.all(calls.map(({ input, args }) => hooks['tool.execute.before'](input, { args })))
    assert.deepEqual(calls.map(({ args }) => args), expected)
    for (const { input, args } of [...calls].reverse()) await complete(hooks, input, args)
    assert.deepEqual(calls.map(({ args }) => args), expected)
    assert.deepEqual(calls.map(({ args }) => Object.keys(args)), expected.map(Object.keys))
  })
})

test('WHAT[host-boundary-032] an after callback with a reported failure preserves the original public arguments', async () => {
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

test.todo('WHAT[host-boundary-032] actual production JS cancellation must settle its executor and restore inputs; a controlled AbortSignal rejection does not prove this')

integrationTest('WHAT[host-boundary-032] one real Host preserves arguments and durable history for production JS and controlled executor failure/cancellation', async (t) => {
  const { runManagerReviewToolsCanary } = await import('./support/run-manager-review-tools-canary.mjs')
  const stage = (name, verify) => async (value) => {
    let failure
    await t.test(`WHAT[host-boundary-032] ${name}`, async () => {
      try { await verify(value) } catch (error) { failure = error; throw error }
    })
    if (failure) throw failure
  }
  const verifyCall = (call, status) => {
    assert.deepEqual(call, {
      sameArguments: true,
      originalOrder: true,
      originalValues: true,
      durableInputRetainsContract: true,
      originalDurableInput: true,
      status,
    })
  }
  const evidence = await runManagerReviewToolsCanary({
    versions: stage('installed binary and plugin have the supported version', (versions) => {
      assert.deepEqual(versions, { opencode: '1.18.29', plugin: '1.18.29' })
    }),
    ready: stage('one real Host is healthy and remains in its supervising process group', async ({ sessionID, pid, health }) => {
      assert.equal(typeof sessionID, 'string')
      assert.ok(sessionID.length > 0)
      assert.equal(health.status, 200)
      assert.equal(health.data.healthy, true)
      assert.ok(Number.isInteger(pid) && pid > 0)
      const execute = promisify(execFile)
      const group = async (target) => Number((await execute('ps', ['-o', 'pgid=', '-p', String(target)])).stdout.trim())
      const hostGroup = await group(pid)
      assert.ok(hostGroup > 0)
      assert.equal(hostGroup, await group(process.pid))
      assert.notEqual(hostGroup, pid, 'Host must not escape into a detached group')
    }),
    normal: stage('normal execution restores the original arguments and durable input', ({ before, after, call }) => {
      assert.equal(before.argsIdentityPreserved, true)
      assert.equal(before.preContractInArgs, true)
      assert.equal(before.postContractInArgs, true)
      assert.equal(before.businessKeysPreserved, true)
      assert.equal(after.postAfterContractInArgs, true)
      assert.equal(after.postAfterContractValue, 'do-not-use-except-for-review')
      assert.equal(after.output, "# ok\n\ndata = '''\nHello Wanxiangshu Manager Review Tools Canary\nLine 2: sample text\n\n'''\n")
      verifyCall(call, 'completed')
    }),
    history: stage('the next provider request preserves the original historical tool call', ({ historicalToolCallPreservesContract }) => {
      assert.equal(historicalToolCallPreservesContract, true)
    }),
    executorError: stage('controlled registered executor throw restores the original arguments and durable input', ({ before, settled, call }) => {
      assert.equal(before.postContractInArgs, true)
      assert.equal(settled.entered.sameArguments, true)
      assert.equal(settled.entered.contractHidden, true)
      assert.deepEqual(settled.entered.businessArguments, { program: 'CANARY_CONTROLLED_THROW' })
      assert.equal(settled.originalError, true)
      assert.equal(settled.contractRestored, true)
      verifyCall(call, 'error')
    }),
    cancelRunning: stage('controlled cancellation targets an entered executor and actual running Host call', ({ before, executing, running, observations }) => {
      assert.equal(before.postContractInArgs, true)
      assert.equal(executing.sameArguments, true)
      assert.equal(executing.contractHidden, true)
      assert.deepEqual(executing.businessArguments, { program: 'CANARY_CONTROLLED_ABORT' })
      assert.equal(running.value.callID, 'call_js_cancel_1')
      assert.equal(observations.some(({ kind, value }) => kind === 'tool.terminal.observed' && value?.callID === running.value.callID), false)
    }),
    cancellation: stage('public Host abort rejects the controlled executor and restores original arguments and durable input', ({ settled, call }) => {
      assert.equal(settled.entered.abortObserved, true)
      assert.equal(settled.originalError, true)
      assert.equal(settled.contractRestored, true)
      verifyCall(call, 'error')
    }),
  })
  assert.ok(evidence.versions.opencode)
  assert.ok(evidence.versions.plugin)
  assert.deepEqual(evidence.executionKinds, {
    normal: 'production-js', executorError: 'controlled-throw', cancellation: 'controlled-abort-rejection',
  })
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
