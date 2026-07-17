/**
 * scenario.js — Orchestration helper for OpenCode E2E tests.
 *
 * Two modes:
 * 1. `scenario()` — standalone, creates its own host per test (isolated)
 * 2. `startSuite()` + `suiteScenario()` + `endSuite()` — shared host for 一条龙
 *
 * Suite mode is preferred because opencode serve starts slowly.
 * Per the goal: "一个 Scenario = 一个临时 HOME + 一个临时 XDG + 一个临时工作区
 *                + 一个 mock provider + 一个 opencode serve 进程"
 * Suite mode gives each test its own session but shares the process.
 */

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { StrictMockProvider } from './strict-mock-provider.js';
import { ProcessHost } from './process-host.js';
import { EventProbe } from './event-probe.js';
import { dumpDiagnostics } from './diagnostics.js';

// ─── Helpers ─────────────────────────────────────────────────────────────────

function tmpDir() {
  return fs.mkdtempSync(path.join(os.tmpdir(), 'oc-e2e-'));
}

async function gitInit(dir) {
  const { execSync } = await import('node:child_process');
  if (!fs.existsSync(path.join(dir, '.git'))) {
    execSync('git init', { cwd: dir, stdio: 'ignore' });
    execSync('git config user.email test@example.com', { cwd: dir, stdio: 'ignore' });
    execSync('git config user.name test', { cwd: dir, stdio: 'ignore' });
    fs.writeFileSync(path.join(dir, 'AGENTS.md'), '- e2e workspace\n');
    execSync('git add -A', { cwd: dir, stdio: 'ignore' });
    execSync('git commit -m init', { cwd: dir, stdio: 'ignore' });
  }
}

function resolvePluginPath(variant) {
  let file = 'Plugin.js';
  if (variant === 'mimocode') file = 'PluginMimo.js';
  if (variant === 'mimotui') file = 'PluginMimoTui.js';
  const candidates = [
    path.resolve(`build/src/Hosts/OpenCode/${file}`),
    path.resolve(`../wanxiangshu/build/src/Hosts/OpenCode/${file}`),
  ];
  for (const c of candidates) {
    if (fs.existsSync(c)) return c;
  }
  return candidates[0];
}

// ─── FS Oracle ───────────────────────────────────────────────────────────────

export class FsOracle {
  constructor(workDir) {
    this._workDir = workDir;
  }
  expectFile(relPath) {
    if (!fs.existsSync(path.join(this._workDir, relPath)))
      throw new Error(`FS: expected file missing: ${relPath}`);
  }
  expectNoFile(relPath) {
    if (fs.existsSync(path.join(this._workDir, relPath)))
      throw new Error(`FS: unexpected file exists: ${relPath}`);
  }
  expectFileContent(relPath, expected) {
    const abs = path.join(this._workDir, relPath);
    if (!fs.existsSync(abs)) throw new Error(`FS: file not found: ${relPath}`);
    const actual = fs.readFileSync(abs);
    const expBuf = typeof expected === 'string' ? Buffer.from(expected, 'utf8') : expected;
    if (!actual.equals(expBuf)) {
      const a = actual.toString('utf8').slice(0, 200);
      const e = expBuf.toString('utf8').slice(0, 200);
      throw new Error(`FS: content mismatch: ${relPath}\n  exp: ${JSON.stringify(e)}\n  got: ${JSON.stringify(a)}`);
    }
  }
  expectFileContains(relPath, substr) {
    const content = fs.readFileSync(path.join(this._workDir, relPath), 'utf8');
    if (!content.includes(substr)) throw new Error(`FS: ${relPath} does not contain: ${substr}`);
  }
}

// ─── HTTP Client ─────────────────────────────────────────────────────────────

export class HttpClient {
  constructor(baseUrl, workDir) {
    this._baseUrl = baseUrl;
    this._workDir = workDir;
    this.onSessionCreated = null; // callback(sessionId) for tracking
  }
  async request(method, urlPath, opts = {}) {
    const qs = opts.query ? '?' + new URLSearchParams(opts.query).toString() : '';
    const res = await fetch(this._baseUrl + urlPath + qs, {
      method,
      headers: { 'Content-Type': 'application/json', 'x-opencode-directory': this._workDir, ...(opts.headers || {}) },
      body: opts.body ? JSON.stringify(opts.body) : undefined,
    });
    const text = await res.text();
    try { return { status: res.status, ok: res.ok, data: JSON.parse(text) }; } catch { return { status: res.status, ok: res.ok, data: text }; }
  }
  async createSession(model = { id: 'test-model', providerID: 'test' }) {
    const body = typeof model === 'object' && model !== null && (model.model || model.providerID)
      ? { model }
      : { model: { id: 'test-model', providerID: 'test' } };
    const res = await this.request('POST', '/api/session', { body });
    if (res.ok && this.onSessionCreated) {
      const sid = res.data?.data?.data?.id || res.data?.data?.id;
      if (sid) this.onSessionCreated(sid);
    }
    return res;
  }
  async prompt(sessionID, text, model, timeoutMs = 120000) {
    const ac = new AbortController();
    const promptModel = model || { providerID: 'test', modelID: 'test-model' };
    const timer = setTimeout(() => ac.abort(), timeoutMs);
    try {
      const res = await fetch(`${this._baseUrl}/session/${sessionID}/prompt_async`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'x-opencode-directory': this._workDir },
        body: JSON.stringify({ parts: [{ type: 'text', text }], model: promptModel }),
        signal: ac.signal,
      });
      const bodyText = await res.text();
      return { status: res.status, ok: res.ok, data: bodyText };
    } catch (err) {
      return { status: 0, ok: false, data: err.message };
    } finally { clearTimeout(timer); }
  }
  async messages(sessionID) { return this.request('GET', `/session/${sessionID}/message`); }
  async sessionStatus(sessionID) { return this.request('GET', `/session/${sessionID}`); }
  async runCommand(sessionID, command, args = '', timeoutMs = 10000) {
    const ac = new AbortController();
    const timer = setTimeout(() => ac.abort(), timeoutMs);
    try {
      return await this.requestWithSignal('POST', `/session/${sessionID}/command`, { body: { command, arguments: args } }, ac.signal);
    } finally { clearTimeout(timer); }
  }
  async requestWithSignal(method, urlPath, opts, signal) {
    const qs = opts.query ? '?' + new URLSearchParams(opts.query).toString() : '';
    try {
      const res = await fetch(this._baseUrl + urlPath + qs, {
        method, signal,
        headers: { 'Content-Type': 'application/json', 'x-opencode-directory': this._workDir, ...(opts.headers || {}) },
        body: opts.body ? JSON.stringify(opts.body) : undefined,
      });
      const text = await res.text();
      try { return { status: res.status, ok: res.ok, data: JSON.parse(text) }; } catch { return { status: res.status, ok: res.ok, data: text }; }
    } catch (err) {
      return { status: 0, ok: false, data: err.message };
    }
  }
  async abort(sessionID) { return this.request('POST', `/session/${sessionID}/abort`, { body: {} }); }
}

// ─── Suite ───────────────────────────────────────────────────────────────────

/**
 * Shared test suite: one host, one provider, all tests share them.
 */
export class Suite {
  constructor(scenarioDir, host, provider, client, events) {
    this.scenarioDir = scenarioDir;
    this.host = host;
    this.provider = provider;
    this.client = client;
    this.events = events;
    this.sessionIds = [];
    this.result = { passed: 0, failed: 0 };
  }

  get fs() { return new FsOracle(this.host.workDir); }
}

/**
 * Start a shared E2E suite (one opencode process, all tests share it).
 * 一条龙模式。
 */
export async function startSuite(opts = {}) {
  const scenarioDir = tmpDir();
  const workDir = path.join(scenarioDir, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });

  if (opts.project) {
    for (const [file, content] of Object.entries(opts.project)) {
      const absPath = path.join(workDir, file);
      fs.mkdirSync(path.dirname(absPath), { recursive: true });
      fs.writeFileSync(absPath, content);
    }
  }

  await gitInit(workDir);

  // Provider
  const provider = new StrictMockProvider();
  const providerUrl = await provider.start();
  const llmUrl = `${providerUrl}/v1`;

  // Host
  const host = new ProcessHost();
  const pluginPaths = opts.plugin !== false ? [resolvePluginPath(opts.variant || 'opencode')] : [];
  await host.start({
    scenarioDir,
    providerUrl: llmUrl,
    pluginPaths,
    contextLimit: opts.contextLimit,
  });

  // Client
  const client = new HttpClient(host.baseUrl, host.workDir);

  // Events
  const events = new EventProbe(host.baseUrl, host.workDir);
  await events.connect();

  const suite = new Suite(scenarioDir, host, provider, client, events);

  // Auto-track sessions created via this client
  client.onSessionCreated = (sid) => {
    if (!suite.sessionIds.includes(sid)) suite.sessionIds.push(sid);
  };

  return suite;
}

/**
 * End a shared suite: cleanup host, provider, temp dirs.
 */
export async function endSuite(suite, keepOnFailure = false) {
  const errors = [];

  try { suite.events.close(); } catch (e) { errors.push(`EventProbe: ${e.message}`); }

  for (const sid of suite.sessionIds) {
    try { await suite.client.abort(sid); } catch {}
  }

  try { await suite.host.stop(); } catch (e) { errors.push(`Host: ${e.message}`); }
  try { await suite.provider.stop(); } catch (e) { errors.push(`Provider: ${e.message}`); }

  try {
    const closed = await suite.host.checkSocketClosed(1000);
    if (!closed) errors.push(`Port ${suite.host.port} leak`);
  } catch {}

  if (!keepOnFailure) {
    try { fs.rmSync(suite.scenarioDir, { recursive: true, force: true }); } catch (e) { errors.push(`Cleanup: ${e.message}`); }
  }

  if (errors.length > 0) {
    console.error(`\n  ⚠ Cleanup warnings:\n${errors.map(e => `    ${e}`).join('\n')}`);
  }
}

/**
 * Run a single scenario within a shared suite.
 */
export async function suiteScenario(suite, name, timeoutMs = 120000) {
  const startTime = Date.now();
  console.log(`\n  ▶ ${name}`);
  try {
    await Promise.race([
      suite.fn(suite),
      new Promise((_, reject) => setTimeout(() => reject(new Error(`Timed out after ${timeoutMs}ms`)), timeoutMs)),
    ]);
    suite.result.passed++;
    console.log(`  ✓ ${name} (${Date.now() - startTime}ms)`);
  } catch (err) {
    suite.result.failed++;
    console.error(`  ✗ ${name}: ${err.message}`);
    console.error(`\n  ── Last 30 events ──`);
    console.error(suite.events.dump(30));
    const stderr = suite.host.stderrLog;
    if (stderr.trim()) console.error(`\n  ── opencode stderr ──\n${stderr.slice(-2000)}`);
  }
}

/**
 * Run multiple scenarios as one suite (一条龙).
 *
 * @param {object} opts - Suite options (plugin, variant, contextLimit, project)
 * @param {Array<{name:string, fn:(suite:Suite)=>Promise<void>}>} tests
 */
export async function runSuite(opts, tests) {
  const suite = await startSuite(opts);
  console.log(`Suite started: ${suite.host.baseUrl}`);

  let passed = 0;
  let failed = 0;

  for (const { name, fn } of tests) {
    const startTime = Date.now();
    console.log(`\n  ▶ ${name}`);
    try {
      suite.fn = fn; // HACK: store fn for suiteScenario's access
      await Promise.race([
        fn({
          host: suite.host,
          provider: suite.provider,
          events: suite.events,
          client: suite.client,
          fs: suite.fs,
          scenarioDir: suite.scenarioDir,
        }),
        new Promise((_, reject) => setTimeout(() => reject(new Error(`Timed out`)), opts.timeoutMs || 120000)),
      ]);
      passed++;
      console.log(`  ✓ ${name} (${Date.now() - startTime}ms)`);
    } catch (err) {
      failed++;
      await dumpDiagnostics(suite, err);
    }
  }

  await endSuite(suite, failed > 0);
  console.log(`\n${passed} passed, ${failed} failed`);
  return failed === 0 ? 0 : 1;
}

// ── Legacy helpers ──

async function waitForSessionIdle(client, probe, sessionID, timeoutMs = 60000) {
  const deadline = Date.now() + timeoutMs;
  let sawNonIdle = false; // track if we ever observed a non-idle state
  while (Date.now() < deadline) {
    // Primary: event-based detection
    if (probe.bySession(sessionID).find(e => e.type === 'session.idle')) return true;

    // Secondary: HTTP status map
    const status = await client.request('GET', '/session/status');
    if (status.ok) {
      const smap = status.data?.data || status.data;
      const item = smap?.[sessionID];
      if (item) {
        if (item.type === 'idle') return true;
        sawNonIdle = true;
      } else if (sawNonIdle) {
        // Session disappeared from status map after being observed = idle
        return true;
      }
    }
    await new Promise(r => setTimeout(r, 100));
  }
  return false;
}

function getSessionId(sess) {
  return sess.data?.data?.data?.id || sess.data?.data?.id;
}

// ── Standalone Scenario (one host per test) ──

/**
 * Standalone scenario (creates its own host). Use only for isolation tests.
 * For most tests, use `runSuite()` instead.
 */
export async function scenario(name, fn, opts = {}) {
  const startTime = Date.now();
  const scenarioDir = tmpDir();
  const workDir = path.join(scenarioDir, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });

  if (opts.project) {
    for (const [file, content] of Object.entries(opts.project)) {
      fs.mkdirSync(path.dirname(path.join(workDir, file)), { recursive: true });
      fs.writeFileSync(path.join(workDir, file), content);
    }
  }

  await gitInit(workDir);

  const provider = new StrictMockProvider();
  const providerUrl = await provider.start();
  const host = new ProcessHost();
  const pluginPaths = opts.plugin !== false ? [resolvePluginPath(opts.variant || 'opencode')] : [];

  try {
    await host.start({ scenarioDir, providerUrl: `${providerUrl}/v1`, pluginPaths, contextLimit: opts.contextLimit });

    const client = new HttpClient(host.baseUrl, host.workDir);
    const events = new EventProbe(host.baseUrl, host.workDir);
    await events.connect();

    const ctx = {
      host, provider, events, client,
      fs: new FsOracle(host.workDir),
      scenarioDir,
      _sessionIds: [],
    };

    const timeoutMs = opts.timeoutMs || 60000;
    await Promise.race([
      fn(ctx),
      new Promise((_, reject) => setTimeout(() => reject(new Error(`Timed out after ${timeoutMs}ms`)), timeoutMs)),
    ]);

    events.close();
    await host.stop();
    await provider.stop();
    fs.rmSync(scenarioDir, { recursive: true, force: true });
    console.log(`  ✓ ${name} (${Date.now() - startTime}ms)`);
    return true;
  } catch (err) {
    console.error(`  ✗ ${name}: ${err.message}`);
    try { await host.stop(); } catch {}
    try { await provider.stop(); } catch {}
    try { fs.rmSync(scenarioDir, { recursive: true, force: true }); } catch {}
    return false;
  }
}

export { waitForSessionIdle, getSessionId };
