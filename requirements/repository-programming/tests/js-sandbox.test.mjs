// tests/unit/js-tools/js-sandbox.test.mjs — G5 Phase B: vm sandbox runner.
//
// JS-011: arbitrary model JS without ambient Host authority. JS-054: deadline
// killable, output bounded. JS-019: failures are stable codes, never
// exception-message sniffing.

import assert from 'node:assert/strict'
import test from 'node:test'

import { run, wrapProgram } from '../../../dist/Process/JsSandbox.js'
import { JsFailureModule_code as failureCode } from '../../../dist/Repository/Programming/Js/Failure.js'
import { resultOf } from '../../verification-system/tests/support/domain.mjs'

const runWrapped = async (wrapped, api, { deadlineMs = 2000, outputBound = 1 << 20 } = {}) => resultOf(await run(wrapped, api, deadlineMs, outputBound))

const BASE = `class JsProgram {
  constructor(api) { this._api = api; }
  file(path) { return this._api.js.read(path); }
}`

const PROGRAM = `class Js extends JsProgram {
  async run() {
    return { sum: 1 + 2, text: (await this.file('a.txt')).text };
  }
}`

test('WHAT[REPOSITORY-PROGRAMMING-006] JS011_program_runs_and_returns_json', async () => {
  const api = { js: { read: async (path) => ({ path, text: 'hello' }) } }
  const result = await runWrapped(wrapProgram(BASE, PROGRAM, Date.now() + 60_000), api)
  assert.equal(result.ok, true)
  assert.deepEqual(JSON.parse(result.value), { sum: 3, text: 'hello' })
})

test('WHAT[REPOSITORY-PROGRAMMING-006] JS011_api_is_the_only_authority_in_the_context', async () => {
  // process / require / fs / globalThis.process must be undefined inside the vm.
  const probe = `class Js extends JsProgram {
  async run() {
    return {
      process: typeof process,
      require: typeof require,
      fs: typeof fs,
      globalProcess: typeof globalThis.process,
    };
  }
}`
  const result = await runWrapped(wrapProgram(BASE, probe, Date.now() + 60_000), { js: {} })
  assert.equal(result.ok, true)
  assert.deepEqual(JSON.parse(result.value), {
    process: 'undefined',
    require: 'undefined',
    fs: 'undefined',
    globalProcess: 'undefined',
  })
})

test('WHAT[REPOSITORY-PROGRAMMING-006] JS054_1_sync_infinite_loop_is_killed_by_vm_timeout', async () => {
  const loop = `class Js extends JsProgram {
  async run() { while (true) {} }
}`
  const wrapped = wrapProgram(BASE, loop, Date.now() + 60_000)
  const result = resultOf(await run(wrapped, { js: {} }, 200, 1 << 20))
  assert.equal(result.ok, false)
  assert.equal(failureCode(result.error), 'PROGRAM_TIMEOUT')
})

test('WHAT[REPOSITORY-PROGRAMMING-006] JS054_1_async_deadline_proxy_aborts_api_calls_after_deadline', async () => {
  // deadline in the past → first api call throws __PROGRAM_TIMEOUT__ → wrapper
  // classifies it as a program failure carrying the timeout marker.
  const past = Date.now() - 1000
  const program = `class Js extends JsProgram {
  async run() { await this.file('a.txt'); return { ok: true }; }
}`
  const api = { js: { read: async () => ({ text: 'x' }) } }
  const result = await runWrapped(wrapProgram(BASE, program, past), api)
  assert.equal(result.ok, false)
  assert.equal(failureCode(result.error), 'PROGRAM_TIMEOUT')
})

test('WHAT[REPOSITORY-PROGRAMMING-018] JS019_invalid_javascript_is_invalid_program', async () => {
  const bad = `class Js extends JsProgram { async run() { return { broken: } } }`
  const result = await runWrapped(wrapProgram(BASE, bad, Date.now() + 60_000), { js: {} })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result.error), 'INVALID_PROGRAM')
})

test('WHAT[REPOSITORY-PROGRAMMING-018] JS019_program_throw_is_program_failed', async () => {
  const throwing = `class Js extends JsProgram {
  async run() { throw new Error('boom'); }
}`
  const result = await runWrapped(wrapProgram(BASE, throwing, Date.now() + 60_000), { js: {} })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result.error), 'PROGRAM_FAILED')
})

test('WHAT[REPOSITORY-PROGRAMMING-011] JS010_circular_return_is_invalid_return_value', async () => {
  const circular = `class Js extends JsProgram {
  async run() { const a = {}; a.self = a; return a; }
}`
  const result = await runWrapped(wrapProgram(BASE, circular, Date.now() + 60_000), { js: {} })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result.error), 'INVALID_RETURN_VALUE')
})

test('WHAT[REPOSITORY-PROGRAMMING-006] JS054_2_output_bound_rejects_oversized_results', async () => {
  const big = `class Js extends JsProgram {
  async run() { return { data: 'x'.repeat(1000) }; }
}`
  const result = await runWrapped(wrapProgram(BASE, big, Date.now() + 60_000), { js: {} }, { outputBound: 100 })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result.error), 'RESULT_TOO_LARGE')
})
