import test from 'node:test'

test('WHAT[repository-programming-006] reset through the public runtime updates the actual membrane driver deadline', async () => {
  const assert = (await import('node:assert/strict')).default
  const { start } = await import('../../../dist/Repository/Programming/Js/RuntimeSurface.js')
  const base = `class JsProgram {
    constructor(api) { this._api = api; }
    wait() { return this._api.wait(); }
    probe() { return this._api.probe(); }
  }`
  const model = `class Js extends JsProgram {
    async run() { await this.wait(); return await this.probe(); }
  }`
  const handle = await start(base, model, {
    wait: () => new Promise((resolve) => setTimeout(resolve, 350)),
    probe: () => ({ alive: true }),
  }, 1000, Date.now() + 200, 4096)
  handle.resetDeadline(Date.now() + 1000)
  const result = await handle.promise
  assert.equal(result.ok, true, JSON.stringify(result))
  assert.deepEqual(JSON.parse(result.value), { alive: true })
})

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

test('WHAT[repository-programming-006] JS011_sandbox_program_uses_bindings_end_to_end', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const binding = createApi(dir)
    const surface = coderSurface()
    const program = `class Js extends JsProgram {
  async run() {
    const text = await this.cat('a.txt');
    const [begin, end] = await this.where(text, 'hello');
    this.write('a.txt', text.slice(0, begin) + 'goodbye' + text.slice(end));
    return { before: text };
  }
}`
    const result = await run(surface.baseClassSource, program, apiOf(binding), 2000, Date.now() + 60_000, 1 << 20)
    assert.equal(result.ok, true)
    assert.deepEqual(JSON.parse(result.value), { before: 'hello world' })
    assert.equal(stagedCount(binding), 1, 'rewrite recorded in the as-is ledger')
    const disk = apiOf(binding).cat('a.txt')
    assert.equal(disk.text, 'goodbye world')
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
  cat(path) { return this._api.cat(path); }
}`
const runWrapped = async (
  program,
  api,
  { deadlineMs = 2000, outputBound = 1 << 20, deadlineEpochMs = Date.now() + 60_000 } = {},
) => run(BASE, program, api, deadlineMs, deadlineEpochMs, outputBound)
const failureCode = (result) => result.code
const PROGRAM = `class Js extends JsProgram {
  async run() {
    return { sum: 1 + 2, text: (await this.cat('a.txt')).text };
  }
}`

test('WHAT[repository-programming-006] JS011_program_runs_and_returns_json', async () => {
  const api = { cat: async (path) => ({ path, text: 'hello' }) }
  const result = await runWrapped(PROGRAM, api)
  assert.equal(result.ok, true)
  assert.deepEqual(JSON.parse(result.value), { sum: 3, text: 'hello' })
})
test('WHAT[repository-programming-006] JS011_api_is_the_only_authority_in_the_context', async () => {
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
test('WHAT[repository-programming-006] JS054_1_sync_infinite_loop_is_killed_by_vm_timeout', async () => {
  const loop = `class Js extends JsProgram {
  async run() { while (true) {} }
}`
  const result = await runWrapped(loop, { js: {} }, { deadlineMs: 200 })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result), 'PROGRAM_TIMEOUT')
})
test('WHAT[repository-programming-006] JS054_1_async_deadline_proxy_aborts_api_calls_after_deadline', async () => {
  // deadline in the past → first api call throws __PROGRAM_TIMEOUT__ → wrapper
  // classifies it as a program failure carrying the timeout marker.
  const program = `class Js extends JsProgram {
  async run() { await this.cat('a.txt'); return { ok: true }; }
}`
  const api = { cat: async () => ({ text: 'x' }) }
  const result = await runWrapped(program, api, { deadlineEpochMs: Date.now() - 1000 })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result), 'PROGRAM_TIMEOUT')
})
test('WHAT[repository-programming-006] JS054_2_output_bound_rejects_oversized_results', async () => {
  const big = `class Js extends JsProgram {
  async run() { return { data: 'x'.repeat(1000) }; }
}`
  const result = await runWrapped(big, { js: {} }, { outputBound: 100 })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result), 'RESULT_TOO_LARGE')
})

test('WHAT[repository-programming-006] output budget measures UTF-8 bytes and accepts the exact boundary', async () => {
  const value = { text: '中文🙂' }
  const bytes = Buffer.byteLength(JSON.stringify(value), 'utf8')
  const program = `class Js extends JsProgram { async run() { return ${JSON.stringify(value)}; } }`
  assert.ok(bytes > JSON.stringify(value).length)
  const exact = await runWrapped(program, { js: {} }, { outputBound: bytes })
  assert.equal(exact.ok, true)
  assert.deepEqual(JSON.parse(exact.value), value)
  const over = await runWrapped(program, { js: {} }, { outputBound: bytes - 1 })
  assert.equal(over.ok, false)
  assert.equal(failureCode(over), 'RESULT_TOO_LARGE')
})
}

// post-await budget: covered in 006-cut25-escapes.test.mjs (CUT-25 membrane + afterEvaluate driver)
// Root cause of the old "memory exhaustion" todo: `JsSandbox` runs the
// program IN-PROCESS through `node:vm`
// (src/Wanxiangshu/Process/JsSandbox.fs -> wanxiangli/src/sandbox/Sandbox.fs).
// There is no separate worker thread, child process or isolate, so V8
// `resourceLimits` (`maxOldSpaceSize`) never applies and no memory-only fence
// can exist. `FailureCatalog` still declares `PROGRAM_RESOURCE_LIMIT`
// (JsFailure.ProgramResourceLimit), but no production code constructs it:
// the only in-process bounds are the vm `timeout` for the synchronous segment
// and the injected deadline proxy (`deadlineEpochMs`) checked on every api
// member call. A steady allocating loop is therefore contained by the
// DEADLINE fence, not by a memory bound - and the Host process must survive
// that containment and stay functional for the programs that follow.
{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

const BASE = `class JsProgram {
  constructor(api) { this._api = api; }
  cat(path) { return this._api.cat(path); }
}`

test('WHAT[repository-programming-006] JS054_3_steady_allocation_loop_is_contained_by_deadline_and_host_survives_for_subsequent_programs', async () => {
  const { spawnSync } = await import('node:child_process')
  const { mkdtempSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const directory = mkdtempSync(join(tmpdir(), 'wxs-steady-allocation-'))
  const allocating = `class Js extends JsProgram {
  async run() {
    const chunks = [];
    while (true) {
      chunks.push('x'.repeat(1024));
      if (chunks.length > 64) chunks.shift();
      await this.cat('a.txt');
    }
  }
}`
  // Sandbox SPEC[002] isolates vm timeouts from node:test's async-hook context.
  const script = `
    import assert from 'node:assert/strict';
    import { run } from ${JSON.stringify(new URL('../../../dist/Repository/Programming/Js/RuntimeSurface.js', import.meta.url).href)};
    const base = ${JSON.stringify(BASE)};
    const api = { cat: async () => ({ text: 'x' }) };
    const contained = await run(base, ${JSON.stringify(allocating)}, api, 2000, Date.now() + 50, 1 << 20);
    assert.equal(contained.ok, false);
    assert.equal(contained.code, 'PROGRAM_TIMEOUT');
    const healthy = await run(base, 'class Js extends JsProgram { async run() { return { sum: 1 + 2 }; } }', api, 2000, Date.now() + 60_000, 1 << 20);
    assert.equal(healthy.ok, true, 'the Host survives the contained program and remains functional');
    assert.deepEqual(JSON.parse(healthy.value), { sum: 3 });
    await new Promise((resolve) => setImmediate(resolve));
  `
  let failure
  try {
    const child = spawnSync(process.execPath, ['--input-type=module', '-e', script], {
      cwd: directory,
      encoding: 'utf8',
      timeout: 3000,
      killSignal: 'SIGKILL',
    })
    assert.equal(child.error, undefined, child.error?.message)
    assert.equal(child.signal, null, child.stderr || child.stdout)
    assert.equal(child.status, 0, child.stderr || child.stdout || 'child failed')
  } catch (error) {
    failure = { error }
  }
  try {
    rmSync(directory, { recursive: true, force: true })
  } catch (cleanupError) {
    if (failure) throw new AggregateError([failure.error, cleanupError], 'Steady allocation probe and fixture cleanup failed', { cause: failure.error })
    throw cleanupError
  }
  if (failure) throw failure.error
})
}

// Moved from tests/006-cut25-escapes.test.mjs (CUT-25 membrane escapes): clause [006]; the requirement tree only admits NNN.test.mjs.
{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, writeFileSync, existsSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { run } = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");

const BASE = `class JsProgram {
  constructor(api) { this._api = api; }
  cat(path) { return this._api.cat(path); }
  ls(path) { return this._api.ls(path); }
}`
const runWrapped = (program, api, opts = {}) =>
  run(BASE, program, api, opts.deadlineMs ?? 2000, opts.deadlineEpochMs ?? Date.now() + 60_000, opts.outputBound ?? (1 << 20))

const reach = (expr) =>
  `const p = (${expr}); return { pid: typeof (p && p.pid), canFs: typeof (p && p.getBuiltinModule) };`

test('WHAT[repository-programming-006] CUT25_escape_via_global_api_binding_is_denied', async () => {
  const program = `class Js extends JsProgram { async run() { ${reach(`api.constructor.constructor('return process')()`)} } }`
  const result = await runWrapped(program, { ls: async () => [] })
  assert.equal(result.ok, false)
  assert.match(String(result.reason || result.code), /process is not defined|PROGRAM_FAILED|not defined/i)
})

test('WHAT[repository-programming-006] CUT25_escape_via_deadline_ref_global_is_denied', async () => {
  const program = `class Js extends JsProgram { async run() { ${reach(`__deadlineRef.constructor.constructor('return process')()`)} } }`
  const result = await runWrapped(program, { ls: async () => [] })
  assert.equal(result.ok, false)
})

test('WHAT[repository-programming-006] CUT25_escape_via_globalThis_constructor_is_denied', async () => {
  const shape = await runWrapped('class Js extends JsProgram { async run() { return { constructor: typeof globalThis.constructor, prototype: Object.getPrototypeOf(globalThis) === null }; } }', { ls: async () => [] })
  assert.equal(shape.ok, true)
  assert.deepEqual(JSON.parse(shape.value), { constructor: 'undefined', prototype: true })
  const program = `class Js extends JsProgram { async run() { ${reach(`globalThis.constructor.constructor('return process')()`)} } }`
  const result = await runWrapped(program, { ls: async () => [] })
  assert.equal(result.ok, false)
  assert.equal(result.code, 'PROGRAM_FAILED')
  assert.equal(typeof result.reason, 'string')
  assert.notEqual(result.reason.trim(), '')
})

test('WHAT[repository-programming-006] CUT25_escape_via_returned_array_constructor_is_denied', async () => {
  const program = `class Js extends JsProgram {
  async run() {
    const r = await this.ls('.');
    ${reach(`r.constructor.constructor('return process')()`)}
  }
}`
  const result = await runWrapped(program, { ls: async () => ['a.txt'] })
  assert.equal(result.ok, false)
  assert.match(String(result.reason || ''), /process is not defined/i)
})

test('WHAT[repository-programming-006] CUT25_escape_cannot_write_outside_workspace', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'cut25-reg-'))
  writeFileSync(join(dir, 'a.txt'), 'x')
  const outside = join(dir, '..', 'cut25-reg-escaped-' + process.pid)
  const program = `class Js extends JsProgram {
  async run() {
    const r = await this.ls('.');
    const p = r.constructor.constructor('return process')();
    if (p && p.getBuiltinModule) p.getBuiltinModule('fs').writeFileSync(${JSON.stringify(outside)}, 'x');
    return { wrote: !!(p && p.getBuiltinModule) };
  }
}`
  try {
    const result = await runWrapped(program, { ls: async () => ['a.txt'] })
    assert.equal(result.ok, false)
    assert.equal(existsSync(outside), false, 'escaped write must not land outside the workspace')
  } finally {
    rmSync(outside, { force: true })
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[repository-programming-006] pending async work and post-await computation terminate within the execution budget', async () => {
  // afterEvaluate microtask drains interact badly with node:test async_hooks when the
  // timeout path fires in-process; run the probe in a child so the parent harness stays clean.
  const { spawnSync } = await import('node:child_process')
  const script = `
    import { run } from ${JSON.stringify(new URL('../../../dist/Repository/Programming/Js/RuntimeSurface.js', import.meta.url).pathname)};
    const BASE = \`class JsProgram { constructor(api) { this._api = api; } cat(path) { return this._api.cat(path); } }\`;
    const program = \`class Js extends JsProgram { async run() { await this.cat('a.txt'); while (true) {} } }\`;
    const t0 = Date.now();
    const result = await run(BASE, program, { cat: async () => ({ text: 'x' }) }, 200, Date.now() + 2000, 1 << 20);
    const elapsed = Date.now() - t0;
    if (!result || result.ok !== false || result.code !== 'PROGRAM_TIMEOUT' || elapsed >= 5000) {
      console.error(JSON.stringify({ result, elapsed }));
      process.exit(2);
    }
  `
  const r = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 10000 })
  assert.equal(r.status, 0, r.stderr || r.stdout || 'child failed')
})

test('WHAT[repository-programming-006] CUT25_escape_via_patched_WeakMap_has_is_denied', async () => {
  const program = `class Js extends JsProgram {
  async run() {
    const isProc = (p) => !!(p && typeof p.pid === 'number' && typeof p.getBuiltinModule === 'function');
    let leaked = false;
    const original = WeakMap.prototype.has;
    WeakMap.prototype.has = function (key) {
      try { const p = key && key.constructor && key.constructor.constructor('return process')(); if (isProc(p)) leaked = true; } catch (_) {}
      return original.call(this, key);
    };
    try { await this.ls('.'); } finally { WeakMap.prototype.has = original; }
    return { leaked };
  }
}`
  const result = await runWrapped(program, { ls: async () => ['a.txt'] })
  assert.equal(result.ok, true, JSON.stringify(result))
  const value = typeof result.value === 'string' ? JSON.parse(result.value) : result.value
  assert.equal(value.leaked, false, 'patched WeakMap.prototype.has must not observe a host value')
})

test('WHAT[repository-programming-006] CUT25_escape_via_patched_WeakMap_set_is_denied', async () => {
  const program = `class Js extends JsProgram {
  async run() {
    const isProc = (p) => !!(p && typeof p.pid === 'number' && typeof p.getBuiltinModule === 'function');
    let leaked = false;
    const original = WeakMap.prototype.set;
    WeakMap.prototype.set = function (key, value) {
      try { const p = key && key.constructor && key.constructor.constructor('return process')(); if (isProc(p)) leaked = true; } catch (_) {}
      return original.call(this, key, value);
    };
    try { await this.ls('.'); } finally { WeakMap.prototype.set = original; }
    return { leaked };
  }
}`
  const result = await runWrapped(program, { ls: async () => ['a.txt'] })
  assert.equal(result.ok, true, JSON.stringify(result))
  const value = typeof result.value === 'string' ? JSON.parse(result.value) : result.value
  assert.equal(value.leaked, false, 'patched WeakMap.prototype.set must not observe a host value')
})
}
