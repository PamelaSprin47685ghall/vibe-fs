/**
 * process-host.js — Manages an opencode serve process with proper lifecycle.
 *
 * Handles:
 * - Spawning with fully isolated environment
 * - Waiting for listening port
 * - Capturing stderr (ring buffer)
 * - Proper cleanup: SSE abort → session abort → SIGTERM → SIGKILL
 * - Resource leak detection
 *
 * Usage:
 *   const host = new ProcessHost();
 *   await host.start({ scenarioDir: '/tmp/scenario-x', providerUrl: 'http://127.0.0.1:PORT/v1' });
 *   // ... test ...
 *   await host.stop();
 */

import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import { createIsolatedEnv } from './isolated-env.js';

export class ProcessHost {
  constructor() {
    this._child = null;
    this._baseUrl = null;
    this._port = null;
    this._stderrBuffer = [];
    this._stdoutBuffer = [];
    this._scenarioDir = null;
    this._workDir = null;
    this._env = null;
    this._started = false;
    this._stopped = false;
  }

  get baseUrl() { return this._baseUrl; }
  get port() { return this._port; }
  get workDir() { return this._workDir; }
  get stderrLog() { return this._stderrBuffer.join(''); }
  get stdoutLog() { return this._stdoutBuffer.join(''); }
  get pid() { return this._child?.pid || null; }
  get scenarioDir() { return this._scenarioDir; }

  /**
   * Start an opencode serve process.
   *
   * @param {object} opts
   * @param {string} opts.scenarioDir - Temporary directory for this scenario
   * @param {string} opts.providerUrl - Mock LLM provider URL
   * @param {string[]} [opts.pluginPaths] - Plugin JS paths
   * @param {number} [opts.contextLimit] - Provider context limit
   * @param {number} [opts.startTimeoutMs=30000] - Timeout for startup
   * @param {object} [opts.extraEnv] - Extra env vars
   */
  async start(opts = {}) {
    if (this._started) throw new Error('ProcessHost already started');
    this._started = true;
    this._scenarioDir = opts.scenarioDir;

    const workDir = path.join(opts.scenarioDir, 'workspace');
    fs.mkdirSync(workDir, { recursive: true });
    this._workDir = workDir;

    // Initialize git repo
    try {
      const gitDir = path.join(workDir, '.git');
      if (!fs.existsSync(gitDir)) {
        const { execSync } = await import('node:child_process');
        execSync('git init', { cwd: workDir, stdio: 'ignore' });
        execSync('git config user.email test@example.com', { cwd: workDir, stdio: 'ignore' });
        execSync('git config user.name test', { cwd: workDir, stdio: 'ignore' });
        fs.writeFileSync(path.join(workDir, 'AGENTS.md'), '- e2e workspace\n');
        execSync('git add -A', { cwd: workDir, stdio: 'ignore' });
        execSync('git commit -m init', { cwd: workDir, stdio: 'ignore' });
      }
    } catch (err) {
      // Non-fatal if git init fails
    }

    // Create isolated env
    const envOverrides = createIsolatedEnv({
      scenarioDir: opts.scenarioDir,
      llmUrl: opts.providerUrl,
      pluginPaths: opts.pluginPaths,
      contextLimit: opts.contextLimit,
      extraEnv: opts.extraEnv,
    });

    this._env = { ...process.env, ...envOverrides };

    const child = spawn('opencode', ['serve', '--port', '0', '--hostname', '127.0.0.1'], {
      cwd: workDir,
      env: this._env,
      stdio: ['pipe', 'pipe', 'pipe'],
      windowsHide: true,
    });

    this._child = child;

    // Capture stdout (ring buffer)
    child.stdout.on('data', (chunk) => {
      this._stdoutBuffer.push(chunk.toString());
      if (this._stdoutBuffer.length > 100) this._stdoutBuffer.shift();
    });

    // Capture stderr (ring buffer)
    child.stderr.on('data', (chunk) => {
      this._stderrBuffer.push(chunk.toString());
      if (this._stderrBuffer.length > 100) this._stderrBuffer.shift();
    });

    child.on('exit', (code, signal) => {
      if (!this._stopped) {
        this._stderrBuffer.push(`\n[PROCESS] Unexpected exit: code=${code} signal=${signal}\n`);
      }
    });

    // Wait for listening
    const startTimeout = opts.startTimeoutMs || 30000;
    const listenLine = await this._waitForListening(startTimeout);

    if (!listenLine) {
      child.kill('SIGKILL');
      throw new Error('opencode serve did not output listening line within timeout');
    }

    const m = listenLine.match(/http:\/\/127\.0\.0\.1:(\d+)/)
      || listenLine.match(/http:\/\/localhost:(\d+)/)
      || listenLine.match(/:(\d+)/);
    this._port = m ? Number(m[1]) : 0;
    this._baseUrl = `http://127.0.0.1:${this._port}`;
  }

  /**
   * Stop the host process and clean up resources.
   *
   * Cleanup order:
   * 1. Stop sending mock responses (caller responsibility)
   * 2. SIGTERM to opencode
   * 3. Wait for graceful exit (short deadline)
   * 4. SIGKILL if still alive
   * 5. Verify no leak
   */
  async stop() {
    if (this._stopped) return;
    this._stopped = true;

    if (!this._child) return;

    const child = this._child;

    // SIGTERM first
    try { child.kill('SIGTERM'); } catch {}

    // Wait for exit with timeout
    try {
      await new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
          reject(new Error('SIGTERM timeout'));
        }, 5000);

        child.on('exit', (code, signal) => {
          clearTimeout(timeout);
          resolve({ code, signal });
        });

        child.on('error', (err) => {
          clearTimeout(timeout);
          reject(err);
        });
      });
    } catch {
      // SIGKILL fallback
      try { child.kill('SIGKILL'); } catch {}
      try {
        await new Promise((resolve) => {
          const timeout = setTimeout(resolve, 1000);
          child.on('exit', () => {
            clearTimeout(timeout);
            resolve();
          });
        });
      } catch {}
    }

    // Close stdio
    try { child.stdout.destroy(); } catch {}
    try { child.stderr.destroy(); } catch {}
    try { child.stdin.destroy(); } catch {}

    this._child = null;
  }

  /**
   * Check if a socket is still listening on the port (leak detection).
   */
  async checkSocketClosed(timeoutMs = 2000) {
    return new Promise((resolve) => {
      const socket = new net.Socket();
      socket.setTimeout(timeoutMs);
      socket.on('connect', () => {
        socket.destroy();
        resolve(false); // still open = leak
      });
      socket.on('error', () => {
        resolve(true); // closed = good
      });
      socket.on('timeout', () => {
        socket.destroy();
        resolve(false);
      });
      socket.connect(this._port, '127.0.0.1');
    });
  }

  async _waitForListening(timeoutMs) {
    return new Promise((resolve) => {
      const deadline = Date.now() + timeoutMs;
      let buf = '';

      const check = () => {
        const idx = buf.indexOf('\n');
        if (idx !== -1) {
          const line = buf.slice(0, idx).trim();
          if (line.includes('opencode server listening on http://')) {
            this._child.stdout.removeListener('data', handler);
            resolve(line);
            return;
          }
          buf = buf.slice(idx + 1);
        }
        if (Date.now() > deadline) {
          this._child.stdout.removeListener('data', handler);
          resolve(null);
          return;
        }
        setTimeout(check, 20);
      };

      const handler = (chunk) => {
        buf += chunk.toString();
      };

      this._child.stdout.on('data', handler);
      check();
    });
  }
}
