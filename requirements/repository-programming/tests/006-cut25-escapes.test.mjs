import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, writeFileSync, existsSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

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
  const program = `class Js extends JsProgram { async run() { ${reach(`globalThis.constructor.constructor('return process')()`)} } }`
  const result = await runWrapped(program, { ls: async () => [] })
  assert.equal(result.ok, false)
  assert.match(String(result.reason || ''), /process is not defined/i)
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
