import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, writeFileSync, rmSync, mkdirSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { createApi, api: apiOf, stagedCount, stagedKinds, run } = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");
const { generate } = await import("../../../dist/Repository/Programming/Js/GeneratorSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bindings-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const coderSurface = () => generate('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'], 'en')

test('WHAT[REPOSITORY-PROGRAMMING-006] JS011_sandbox_program_uses_bindings_end_to_end', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const binding = createApi(dir)
    const surface = coderSurface()
    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', view.text('^', 'begin') + 'goodbye' + view.text('end', '$'));
    return { before: view.text() };
  }
}`
    const result = await run(surface.baseClassSource, program, apiOf(binding), 2000, Date.now() + 60_000, 1 << 20)
    assert.equal(result.ok, true)
    assert.deepEqual(JSON.parse(result.value), { before: 'hello world' })
    assert.equal(stagedCount(binding), 1, 'rewrite staged through the binding')
    const disk = apiOf(binding).js.read('a.txt')
    assert.equal(disk.text, 'hello world')
  } finally {
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { run } = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");

const BASE = `class JsProgram {
  constructor(api) { this._api = api; }
  file(path) { return this._api.js.read(path); }
}`
const runWrapped = async (
  program,
  api,
  { deadlineMs = 2000, outputBound = 1 << 20, deadlineEpochMs = Date.now() + 60_000 } = {},
) => run(BASE, program, api, deadlineMs, deadlineEpochMs, outputBound)
const failureCode = (result) => result.code
const PROGRAM = `class Js extends JsProgram {
  async run() {
    return { sum: 1 + 2, text: (await this.file('a.txt')).text };
  }
}`

test('WHAT[REPOSITORY-PROGRAMMING-006] JS011_program_runs_and_returns_json', async () => {
  const api = { js: { read: async (path) => ({ path, text: 'hello' }) } }
  const result = await runWrapped(PROGRAM, api)
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
  const result = await runWrapped(probe, { js: {} })
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
  const result = await runWrapped(loop, { js: {} }, { deadlineMs: 200 })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result), 'PROGRAM_TIMEOUT')
})
test('WHAT[REPOSITORY-PROGRAMMING-006] JS054_1_async_deadline_proxy_aborts_api_calls_after_deadline', async () => {
  // deadline in the past → first api call throws __PROGRAM_TIMEOUT__ → wrapper
  // classifies it as a program failure carrying the timeout marker.
  const program = `class Js extends JsProgram {
  async run() { await this.file('a.txt'); return { ok: true }; }
}`
  const api = { js: { read: async () => ({ text: 'x' }) } }
  const result = await runWrapped(program, api, { deadlineEpochMs: Date.now() - 1000 })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result), 'PROGRAM_TIMEOUT')
})
test('WHAT[REPOSITORY-PROGRAMMING-006] JS054_2_output_bound_rejects_oversized_results', async () => {
  const big = `class Js extends JsProgram {
  async run() { return { data: 'x'.repeat(1000) }; }
}`
  const result = await runWrapped(big, { js: {} }, { outputBound: 100 })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result), 'RESULT_TOO_LARGE')
})
}
