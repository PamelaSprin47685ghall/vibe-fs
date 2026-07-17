/**
 * scenario.js — Orchestration helper for OpenCode E2E tests.
 *
 * Provides the `scenario()` function that ties together:
 * - Isolated temp directory
 * - Mock LLM provider (strict)
 * - opencode serve process
 * - Event probe
 * - HTTP client
 * - Cleanup with leak detection
 *
 * Usage:
 *   import { scenario } from './harness/scenario.js';
 *
 *   await scenario('OC-FILE-001 writes exact bytes', async (t) => {
 *     await t.host.start({ plugin: true });
 *     const session = await t.client.createSession();
 *     t.provider.expectToolCall({ id: 'write-call', tool: 'write', args: ... });
 *     t.provider.expectText({ id: 'final-answer', text: 'done' });
 *     await t.client.prompt(session.id, 'Write hello.txt');
 *     await t.events.awaitEvent(e => e.type === 'session.idle' && e.sessionID === session.id);
 *     t.fs.expectFile('hello.txt', Buffer.from('hello'));
 *     t.fs.expectFileContent('hello.txt', 'hello');
 *     t.provider.expectSatisfied();
 *     await t.cleanup();
 *   });
 */

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { StrictMockProvider } from './strict-mock-provider.js';
import { ProcessHost } from './process-host.js';
import { EventProbe } from './event-probe.js';

// ─── Helpers ─────────────────────────────────────────────────────────────────

function tmpScenarioDir() {
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

class FsOracle {
  constructor(workDir) {
    this._workDir = workDir;
  }

  /**
   * Assert a file exists at the given relative path.
   */
  expectFile(relPath) {
    const abs = path.join(this._workDir, relPath);
    if (!fs.existsSync(abs)) {
      throw new Error(`FS Oracle: expected file does not exist: ${relPath}`);
    }
  }

  /**
   * Assert a file does not exist.
   */
  expectNoFile(relPath) {
    const abs = path.join(this._workDir, relPath);
    if (fs.existsSync(abs)) {
      throw new Error(`FS Oracle: unexpected file exists: ${relPath}`);
    }
  }

  /**
   * Assert file content matches expected buffer/string.
   */
  expectFileContent(relPath, expected) {
    const abs = path.join(this._workDir, relPath);
    if (!fs.existsSync(abs)) {
      throw new Error(`FS Oracle: file does not exist for content check: ${relPath}`);
    }
    const actual = fs.readFileSync(abs);
    const expectedBuf = typeof expected === 'string' ? Buffer.from(expected, 'utf8') : expected;

    if (!actual.equals(expectedBuf)) {
      // Show diff for diagnostics
      const actualStr = actual.toString('utf8');
      const expectedStr = expectedBuf.toString('utf8');
      const maxShow = 200;
      throw new Error(
        `FS Oracle: file content mismatch: ${relPath}\n` +
        `  expected (${expectedBuf.length} bytes): ${JSON.stringify(expectedStr.slice(0, maxShow))}\n` +
        `  actual   (${actual.length} bytes): ${JSON.stringify(actualStr.slice(0, maxShow))}`
      );
    }
  }

  /**
   * Assert file content contains substring.
   */
  expectFileContains(relPath, substring) {
    const abs = path.join(this._workDir, relPath);
    if (!fs.existsSync(abs)) {
      throw new Error(`FS Oracle: file does not exist: ${relPath}`);
    }
    const content = fs.readFileSync(abs, 'utf8');
    if (!content.includes(substring)) {
      throw new Error(`FS Oracle: file ${relPath} does not contain: ${substring}`);
    }
  }
}

// ─── HTTP Client ─────────────────────────────────────────────────────────────

class HttpClient {
  constructor(baseUrl) {
    this._baseUrl = baseUrl;
  }

  async request(method, urlPath, opts = {}) {
    const qs = opts.query ? '?' + new URLSearchParams(opts.query).toString() : '';
    const url = this._baseUrl + urlPath + qs;
    const res = await fetch(url, {
      method,
      headers: {
        'Content-Type': 'application/json',
        ...(opts.headers || {}),
      },
      body: opts.body ? JSON.stringify(opts.body) : undefined,
    });
    const text = await res.text();
    let data;
    try { data = JSON.parse(text); } catch { data = text; }
    return { status: res.status, ok: res.ok, data };
  }

  async createSession(body = { model: { id: 'test-model', providerID: 'test' } }) {
    return this.request('POST', '/api/session', { body });
  }

  async prompt(sessionID, text, timeoutMs = 120000) {
    const ac = new AbortController();
    const timer = setTimeout(() => ac.abort(), timeoutMs);
    try {
      const res = await fetch(`${this._baseUrl}/session/${sessionID}/prompt_async`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          parts: [{ type: 'text', text }],
          model: { providerID: 'test', modelID: 'test-model' },
        }),
        signal: ac.signal,
      });
      const text = await res.text();
      return { status: res.status, ok: res.ok, data: text };
    } catch (err) {
      return { status: 0, ok: false, data: err.message };
    } finally {
      clearTimeout(timer);
    }
  }

  async messages(sessionID) {
    return this.request('GET', `/session/${sessionID}/message`);
  }

  async sessionStatus(sessionID) {
    return this.request('GET', `/session/${sessionID}`);
  }

  async runCommand(sessionID, command, args = '') {
    return this.request('POST', `/session/${sessionID}/command`, {
      body: { command, arguments: args },
    });
  }

  async abort(sessionID) {
    return this.request('POST', `/session/${sessionID}/abort`, { body: {} });
  }
}

// ─── Context Object ──────────────────────────────────────────────────────────

/**
 * Context object passed to each scenario function.
 */
class ScenarioContext {
  constructor() {
    this._scenarioDir = null;
    this._provider = null;
    this._host = null;
    this._events = null;
    this._client = null;
    this._fs = null;
    this._sessionIds = [];
  }

  get provider() { return this._provider; }
  get host() { return this._host; }
  get events() { return this._events; }
  get client() { return this._client; }
  get fs() { return new FsOracle(this._host?.workDir || ''); }

  get scenarioDir() { return this._scenarioDir; }

  /**
   * Must be called by scenario runner to initialize.
   */
  async _init(opts = {}) {
    this._scenarioDir = tmpScenarioDir();

    // Create workspace
    const workDir = path.join(this._scenarioDir, 'workspace');
    fs.mkdirSync(workDir, { recursive: true });

    // Setup initial project files
    if (opts.project) {
      for (const [file, content] of Object.entries(opts.project)) {
        const absPath = path.join(workDir, file);
        fs.mkdirSync(path.dirname(absPath), { recursive: true });
        fs.writeFileSync(absPath, content);
      }
    }

    // Init git
    await gitInit(workDir);

    // Start mock provider
    this._provider = new StrictMockProvider();
    const providerUrl = await this._provider.start();
    const llmUrl = `${providerUrl}/v1`;

    // Start opencode host
    this._host = new ProcessHost();
    const pluginPaths = opts.plugin
      ? [resolvePluginPath(opts.variant || 'opencode')]
      : [];
    await this._host.start({
      scenarioDir: this._scenarioDir,
      providerUrl: llmUrl,
      pluginPaths,
      contextLimit: opts.contextLimit,
    });

    // Connect event probe
    this._events = new EventProbe(this._host.baseUrl, this._host.workDir);
    await this._events.connect();
  }

  /**
   * Cleanup: stop host, provider, remove temp dir.
   */
  async _cleanup(keepOnFailure = false) {
    const errors = [];

    // Close event probe
    try {
      if (this._events) this._events.close();
    } catch (e) {
      errors.push(`EventProbe close: ${e.message}`);
    }

    // Abort any remaining sessions
    for (const sid of this._sessionIds) {
      try {
        await this._client?.abort(sid);
      } catch {}
    }

    // Stop host
    try {
      if (this._host) await this._host.stop();
    } catch (e) {
      errors.push(`Host stop: ${e.message}`);
    }

    // Stop provider
    try {
      if (this._provider) await this._provider.stop();
    } catch (e) {
      errors.push(`Provider stop: ${e.message}`);
    }

    // Check socket leak
    if (this._host && this._host.port) {
      const closed = await this._host.checkSocketClosed(1000);
      if (!closed) {
        errors.push(`Port ${this._host.port} still listening after stop`);
      }
    }

    // Remove temp dir (unless keeping for failure analysis)
    if (!keepOnFailure && this._scenarioDir) {
      try {
        fs.rmSync(this._scenarioDir, { recursive: true, force: true });
      } catch (e) {
        errors.push(`Temp dir cleanup: ${e.message}`);
      }
    }

    if (errors.length > 0) {
      throw new Error(`Cleanup errors:\n${errors.map(e => `  - ${e}`).join('\n')}`);
    }
  }

  /**
   * Register a session for automatic cleanup.
   */
  _trackSession(sessionID) {
    this._sessionIds.push(sessionID);
  }
}

// ─── Scenario Runner ─────────────────────────────────────────────────────────

/**
 * Run an E2E test scenario.
 *
 * @param {string} name - Test name / spec ID
 * @param {function(ScenarioContext): Promise<void>} fn - Test function
 * @param {object} [opts]
 * @param {boolean} [opts.plugin=true] - Load plugin
 * @param {string} [opts.variant] - Plugin variant (opencode/mimocode/mimotui)
 * @param {number} [opts.timeoutMs=60000] - Overall test timeout
 * @param {number} [opts.contextLimit] - Provider context limit
 * @param {object} [opts.project] - Initial project files { "path": "content" }
 */
export async function scenario(name, fn, opts = {}) {
  const startTime = Date.now();
  const timeoutMs = opts.timeoutMs || 60000;
  const ctx = new ScenarioContext();

  console.log(`\n  ▶ ${name}`);

  try {
    await ctx._init({
      plugin: opts.plugin !== false,
      variant: opts.variant || 'opencode',
      contextLimit: opts.contextLimit,
      project: opts.project,
    });

    // Wrap fn to track sessions from createSession
    const originalCreateSession = ctx.client.createSession.bind(ctx.client);
    ctx.client.createSession = async (body) => {
      const result = await originalCreateSession(body);
      if (result.ok && result.data?.data?.data?.id) {
        ctx._trackSession(result.data.data.data.id);
      } else if (result.ok && result.data?.data?.id) {
        ctx._trackSession(result.data.data.id);
      }
      return result;
    };

    // Run with timeout
    await Promise.race([
      fn(ctx),
      new Promise((_, reject) =>
        setTimeout(() => reject(new Error(`Scenario timed out after ${timeoutMs}ms`)), timeoutMs)
      ),
    ]);

    await ctx._cleanup(false);
    console.log(`  ✓ ${name} (${Date.now() - startTime}ms)`);
    return true;
  } catch (err) {
    // Dump diagnostics on failure
    console.error(`  ✗ ${name}: ${err.message}`);
    if (ctx.events) {
      console.error(`\n  ── Last ${30} events ──`);
      console.error(ctx.events.dump(30));
    }
    if (ctx.host && ctx.host.stderrLog) {
      const stderr = ctx.host.stderrLog;
      if (stderr.trim()) {
        console.error(`\n  ── opencode stderr (last 2000 chars) ──`);
        console.error(stderr.slice(-2000));
      }
    }

    try { await ctx._cleanup(true); } catch {}
    return false;
  }
}

/**
 * Run multiple scenarios sequentially.
 */
export async function runScenarios(scenarios) {
  let passed = 0;
  let failed = 0;
  for (const { name, fn, opts } of scenarios) {
    const ok = await scenario(name, fn, opts);
    if (ok) passed++;
    else failed++;
  }
  console.log(`\n${passed} passed, ${failed} failed`);
  return failed === 0 ? 0 : 1;
}
