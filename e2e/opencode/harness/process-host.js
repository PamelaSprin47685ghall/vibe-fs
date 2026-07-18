/**
 * process-host.js — Manages an opencode serve process with proper lifecycle.
 *
 * stop() is async and fails loud: a port still listening, a surviving PID,
 * or a leaked child process tree after stop() throws an error.
 *
 * Side-effect-free helpers (child lifecycle, socket/PID checks, git init,
 * listen-port parsing) live in process-host-utils.js and process-host-checks.js
 * so this file stays under the 200-line Kolmogorov line budget.
 */

import fs from 'node:fs';
import path from 'node:path';
import { createIsolatedEnv } from './isolated-env.js';
import {
  SIGTERM_GRACE_MS,
  READY_POLL_INTERVAL_MS,
  READY_POLL_MAX_TRIES,
  parseListenPort,
  ringPush,
  terminateChild,
  initGitWorkspace,
  spawnOpencodeServe,
} from './process-host-utils.js';
import {
  isPidAlive,
  checkSocketClosed,
  checkProcessTree,
} from './process-host-checks.js';

const READY_SENTINEL = 'opencode server listening on http://';
const LISTEN_POLL_INTERVAL_MS = 50;
const LISTEN_POLL_INITIAL_DELAY_MS = 100;
const STDOUT_RING_MAX = 100;
const SIGKILL_GRACE_MS = 1000;
const SOCKET_CHECK_TIMEOUT_MS = 2000;
const PROCESS_TREE_TIMEOUT_MS = 2000;

export class ProcessHost {
  constructor() {
    this._child = null;
    this._pid = null;
    this._baseUrl = null;
    this._port = null;
    this._stderrBuffer = [];
    this._stdoutBuffer = [];
    this._scenarioDir = null;
    this._workDir = null;
    this._env = null;
    this._started = false;
    this._stopped = false;
    this._exitInfo = null;
  }

  get baseUrl() { return this._baseUrl; }
  get port() { return this._port; }
  get workDir() { return this._workDir; }
  get stderrLog() { return this._stderrBuffer.join(''); }
  get stdoutLog() { return this._stdoutBuffer.join(''); }
  get pid() { return this._child?.pid || null; }
  get scenarioDir() { return this._scenarioDir; }
  get exitInfo() { return this._exitInfo; }

  async start(opts = {}) {
    if (this._started) throw new Error('ProcessHost already started');
    this._started = true;
    this._scenarioDir = opts.scenarioDir;
    this._workDir = ensureWorkspace(opts.scenarioDir);
    await initGitWorkspace(this._workDir);
    this._env = buildEnv(opts);
    this._child = spawnOpencodeServe(this._workDir, this._env, {
      onStdoutChunk: this._onStdout.bind(this),
      onStderrChunk: this._onStderr.bind(this),
      onExit: this._onChildExit.bind(this),
    });
    const startTimeout = opts.startTimeoutMs || 30000;
    const listenLine = await this._waitForListening(startTimeout);
    if (!listenLine) {
      try { this._child.kill('SIGKILL'); } catch {}
      throw new Error('opencode serve did not output listening line within timeout');
    }
    this._pid = this._child.pid;
    this._port = parseListenPort(listenLine);
    this._baseUrl = `http://127.0.0.1:${this._port}`;
    await this._waitForHealth(startTimeout);
  }

  async _waitForHealth(timeoutMs) {
    const deadline = Date.now() + timeoutMs;
    for (let i = 0; i < READY_POLL_MAX_TRIES; i++) {
      try {
        const res = await fetch(`${this._baseUrl}/api/session`, { method: 'GET' });
        if (res.ok || res.status === 200) return;
      } catch {}
      if (Date.now() > deadline) {
        throw new Error('Health-check failed: server not responding');
      }
      await new Promise((r) => setTimeout(r, READY_POLL_INTERVAL_MS));
    }
  }

  async stop({ assert = true } = {}) {
    if (this._stopped) return;
    this._stopped = true;
    if (!this._child) {
      // Already stopped, but reset flags so a fresh host can be started.
      this._started = false;
      this._stopped = false;
      return;
    }
    await terminateChild(this._child, SIGTERM_GRACE_MS, SIGKILL_GRACE_MS);
    try { this._child.stdout.destroy(); } catch {}
    try { this._child.stderr.destroy(); } catch {}
    try { this._child.stdin.destroy(); } catch {}
    this._child = null;
    if (!assert) {
      // Clean up state for reuse but keep _pid/_port until the caller
      // explicitly asserts or re-starts.
      this._started = false;
      this._stopped = false;
      return;
    }
    await this.assertNoLeak();
    // Allow the same ProcessHost instance to be re-used in a future
    // scenario. New scenarios must always get a fresh instance via
    // `new ProcessHost()`, but resetting here keeps the API forgiving.
    this._started = false;
    this._stopped = false;
    this._baseUrl = null;
    this._port = null;
    this._pid = null;
    this._exitInfo = null;
  }

  async assertNoLeak() {
    const errors = [];
    const pid = this._pid;
    if (this._port && !(await checkSocketClosed(this._port, SOCKET_CHECK_TIMEOUT_MS))) {
      errors.push(`port ${this._port} still listening`);
    }
    if (pid && isPidAlive(pid) && !this._exitInfo) errors.push(`pid ${pid} still alive`);
    const tree = await checkProcessTree(pid, PROCESS_TREE_TIMEOUT_MS);
    if (tree) errors.push(`process tree leaked: ${tree}`);
    if (errors.length > 0) {
      throw new Error(`ProcessHost leak detected: ${errors.join('; ')}`);
    }
  }

  _onStdout(s) { ringPush(this._stdoutBuffer, s, STDOUT_RING_MAX); }
  _onStderr(s) { ringPush(this._stderrBuffer, s, STDOUT_RING_MAX); }
  _onChildExit(code, signal) {
    this._exitInfo = { code, signal, time: Date.now() };
    if (!this._stopped) {
      this._stderrBuffer.push(`\n[PROCESS] Unexpected exit: code=${code} signal=${signal}\n`);
    }
  }

  async _waitForListening(timeoutMs) {
    return new Promise((resolve) => {
      const deadline = Date.now() + timeoutMs;
      let buf = '';
      const handler = (chunk) => { buf += chunk.toString(); tryResolve(); };
      const tryResolve = () => {
        if (!buf.includes(READY_SENTINEL)) return false;
        this._child.stdout.removeListener('data', handler);
        const lines = buf.split('\n');
        const listenLine = lines.find(l => l.includes(READY_SENTINEL));
        resolve(listenLine ? listenLine.trim() : buf.trim());
        return true;
      };
      this._child.stdout.on('data', handler);
      const poll = () => {
        if (tryResolve()) return;
        if (Date.now() > deadline) {
          this._child.stdout.removeListener('data', handler);
          resolve(null);
          return;
        }
        setTimeout(poll, LISTEN_POLL_INTERVAL_MS);
      };
      setTimeout(poll, LISTEN_POLL_INITIAL_DELAY_MS);
    });
  }
}

function ensureWorkspace(scenarioDir) {
  const workDir = path.join(scenarioDir, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });
  return workDir;
}

function buildEnv(opts) {
  const envOverrides = createIsolatedEnv({
    scenarioDir: opts.scenarioDir,
    llmUrl: opts.providerUrl,
    pluginPaths: opts.pluginPaths,
    contextLimit: opts.contextLimit,
    extraEnv: opts.extraEnv,
  });
  return { ...process.env, ...envOverrides };
}
