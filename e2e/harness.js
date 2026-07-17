import { execSync, spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { StrictMockProvider } from './opencode/harness/strict-mock-provider.js';
import {
  E2E_LOCK,
  releaseE2eLock,
  gitInit,
  isolatedEnv,
  waitForListening,
  hostSingletonManager,
} from './harness-bootstrap.js';

// ─── MockLLMAdapter — wraps StrictMockProvider to match existing harness API ──
// F# tests call: mockLLM.expectTool(tool, args), mockLLM.expectText(text),
//                mockLLM.calls, mockLLM.reset(), mockLLM.getRemainingExpectations()

class MockLLMAdapter {
  constructor(provider) {
    this._provider = provider;
  }

  // accept both string form (existing callers) and {text} object form
  expectText(text) {
    if (typeof text === 'string') {
      this._provider.expectText({ text });
    } else {
      this._provider.expectText(text);
    }
  }

  // map legacy expectTool(toolName, args) → expectToolCall({tool, args})
  expectTool(tool, args) {
    this._provider.expectToolCall({ tool, args: args ?? {} });
  }

  reset() {
    this._provider.reset();
  }

  getRemainingExpectations() {
    return this._provider.remainingExpectations;
  }

  // expose StrictMockProvider._requests mapped to {body: r} shape
  get calls() {
    return (this._provider.requests || []).map((r) => ({ body: r }));
  }

  get url() {
    return this._provider.url;
  }

  async start() {
    return await this._provider.start();
  }

  async stop() {
    return await this._provider.stop();
  }
}

// ─── Permission SSE helper ───────────────────────────────────────────────────

async function replyToPermission(baseUrl, workDir, sessionID, id) {
  const url = `${baseUrl}/session/${sessionID}/permissions/${id}`;
  const res = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'x-opencode-directory': workDir
    },
    body: JSON.stringify({ response: 'once' })
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Permission reply failed: status=${res.status} body=${text}`);
  }
}

async function handleSseLine(line, baseUrl, workDir) {
  const trimmed = line.trim();
  if (!trimmed.startsWith('data:')) return;
  const jsonStr = trimmed.slice(5).trim();
  if (!jsonStr) return;
  const parsed = JSON.parse(jsonStr);
  if (parsed?.type === 'permission.asked') {
    const sessionID = parsed?.properties?.sessionID;
    const id = parsed?.properties?.id;
    if (sessionID && id) {
      await replyToPermission(baseUrl, workDir, sessionID, id);
    }
  }
}

async function processBuffer(buffer, baseUrl, workDir) {
  const lines = buffer.split('\n');
  const remaining = lines.pop() || '';
  for (const line of lines) {
    await handleSseLine(line, baseUrl, workDir);
  }
  return remaining;
}

async function readSseStream(body, baseUrl, workDir, abortSignal, hostObj) {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';
  try {
    for (;;) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      buffer = await processBuffer(buffer, baseUrl, workDir);
    }
    if (buffer.trim()) {
      await handleSseLine(buffer, baseUrl, workDir);
    }
  } catch (err) {
    if (err.name === 'AbortError' || err.message?.includes('aborted') || abortSignal.aborted) {
      return;
    }
    hostObj.permissionError = err;
    throw err;
  } finally {
    try { reader.releaseLock(); } catch {}
  }
}

// ─── Stderr ring-buffer helper ───────────────────────────────────────────────

class StderrRingBuffer {
  constructor(maxLines = 200) {
    this._lines = [];
    this._maxLines = maxLines;
  }
  push(line) {
    this._lines.push(line);
    if (this._lines.length > this._maxLines) this._lines.shift();
  }
  get log() { return this._lines.join('\n'); }
}

function initHostDirAndSpawn(llmHandle, opts) {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiang-e2e-'));
  const workDir = path.join(home, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });
  gitInit(workDir);

  const nodeModulesSource = path.resolve('node_modules');
  if (fs.existsSync(nodeModulesSource)) {
    try { fs.symlinkSync(nodeModulesSource, path.join(workDir, 'node_modules'), 'dir'); } catch {}
    try { fs.symlinkSync(nodeModulesSource, path.join(home, 'node_modules'), 'dir'); } catch {}
    try { fs.copyFileSync(path.resolve('package.json'), path.join(workDir, 'package.json')); } catch {}
    try { fs.copyFileSync(path.resolve('package-lock.json'), path.join(workDir, 'package-lock.json')); } catch {}
    try { fs.copyFileSync(path.resolve('package.json'), path.join(home, 'package.json')); } catch {}
    try { fs.copyFileSync(path.resolve('package-lock.json'), path.join(home, 'package-lock.json')); } catch {}
  }

  const env = isolatedEnv(home, `${llmHandle.url}/v1`, opts);
  const stderrBuffer = new StderrRingBuffer();
  const child = spawn('opencode', ['serve', '--port', '0', '--hostname', '127.0.0.1'], {
    cwd: workDir,
    env: { ...process.env, ...env },
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  });
  child.stderr.on('data', (chunk) => {
    for (const line of chunk.toString().split('\n')) {
      if (line.trim()) stderrBuffer.push(line.trim());
    }
  });
  return { home, workDir, child, stderrBuffer };
}

async function connectPermissionResponder(hostObj) {
  try {
    const sseUrl = `${hostObj.baseUrl}/event`;
    const res = await fetch(sseUrl, {
      headers: {
        'Accept': 'text/event-stream',
        'x-opencode-directory': hostObj.workDir
      },
      signal: hostObj.permissionAbort.signal
    });
    if (!res.ok) {
      throw new Error(`GET /event failed with status ${res.status}`);
    }
    hostObj.permissionPromise = readSseStream(
      res.body,
      hostObj.baseUrl,
      hostObj.workDir,
      hostObj.permissionAbort.signal,
      hostObj
    );
  } catch (err) {
    hostObj.child.kill('SIGKILL');
    await hostObj.mockLLM.stop().catch(() => {});
    throw err;
  }
}

async function spawnOpencodeHost(variant, opts) {
  const provider = new StrictMockProvider();
  const llmHandle = new MockLLMAdapter(provider);
  const url = await provider.start();
  const { home, workDir, child, stderrBuffer } = initHostDirAndSpawn(llmHandle, opts);

  let listenLine = await waitForListening(child.stdout, child);
  if (!listenLine) {
    child.kill('SIGKILL');
    await provider.stop().catch(() => {});
    throw new Error('waitForListening returned empty/undefined');
  }

  const m = listenLine.match(/http:\/\/127\.0\.0\.1:(\d+)/) || listenLine.match(/http:\/\/localhost:(\d+)/) || listenLine.match(/:(\d+)/);
  const port = m ? Number(m[1]) : 0;
  const baseUrl = `http://127.0.0.1:${port}`;

  const permissionAbort = new AbortController();
  const hostObj = {
    child,
    mockLLM: llmHandle,
    workDir,
    home,
    baseUrl,
    port,
    permissionAbort,
    permissionPromise: null,
    permissionError: null,
    warmSession: null,
    stderrBuffer,
  };

  await connectPermissionResponder(hostObj);

  return hostObj;
}

class OpencodeHarness {
  constructor(sharedHost) {
    this.port = sharedHost.port;
    this.baseUrl = sharedHost.baseUrl;
    this.mockLLM = sharedHost.mockLLM;
    this.workDir = sharedHost.workDir;
    this.home = sharedHost.home;
    this.activeSessionId = null;
    this._trackedSessionIds = [];
    this._permissionAbort = sharedHost.permissionAbort || null;
    this._permissionPromise = sharedHost.permissionPromise || null;
    this._child = sharedHost.child || null;
    this._stderrBuffer = sharedHost.stderrBuffer || null;
  }

  // expose stderr ring buffer for diagnostics
  get stderrLog() {
    return this._stderrBuffer ? this._stderrBuffer.log : '';
  }

  async request(method, urlPath, { query, body, headers } = {}) {
    const qs = query ? '?' + new URLSearchParams(query).toString() : '';
    const url = this.baseUrl + urlPath + qs;

    const opts = {
      method,
      headers: {
        'Content-Type': 'application/json',
        'x-opencode-directory': this.workDir,
        ...(headers || {}),
      },
    };
    if (body !== undefined) opts.body = JSON.stringify(body);
    const res = await fetch(url, opts);
    const text = await res.text();
    let data;
    try { data = JSON.parse(text); } catch { data = text; }

    if (urlPath === '/api/session' && method === 'POST' && res.ok) {
      const newSessId = data?.data?.data?.id || data?.data?.id;
      if (newSessId) {
        this.activeSessionId = newSessId;
        if (!this._trackedSessionIds.includes(newSessId)) {
          this._trackedSessionIds.push(newSessId);
        }
      }
    }

    return { status: res.status, ok: res.ok, data };
  }

  async createSession(body = { model: { id: 'test-model', providerID: 'test' } }, query = {}) {
    return this.request('POST', '/api/session', { query, body });
  }

  async sendPrompt(sessionID, text, query = {}, timeoutMs = 120000) {
    if (sessionID) this.activeSessionId = sessionID;

    const qs = query ? '?' + new URLSearchParams(query).toString() : '';
    const url = this.baseUrl + `/session/${sessionID}/prompt_async` + qs;
    const body = { parts: [{ type: 'text', text }], model: { providerID: 'test', modelID: 'test-model' } };
    const ac = new AbortController();
    const timeout = setTimeout(() => ac.abort(), timeoutMs);
    try {
      const res = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'x-opencode-directory': this.workDir },
        body: JSON.stringify(body),
        signal: ac.signal,
      });
      const data = await res.text();
      return { status: res.status, ok: res.ok, data };
    } catch (e) {
      return { status: 0, ok: false, data: e.message };
    } finally {
      clearTimeout(timeout);
    }
  }

  async getMessages(sessionID, query = {}) {
    if (sessionID) this.activeSessionId = sessionID;
    return this.request('GET', `/session/${sessionID}/message`, { query });
  }

  async getSession(sessionID, query = {}) {
    if (sessionID) this.activeSessionId = sessionID;
    return this.request('GET', `/session/${sessionID}`, { query });
  }

  async listProviders() {
    return this.request('GET', '/provider', {});
  }

  contextBudgetClient() {
    return {
      session: {
        get: async ({ path: { id } }) => {
          const response = await this.request('GET', `/session/${id}`);
          return { data: response.data };
        },
      },
      provider: {
        list: async (options = {}) => {
          const response = await this.request('GET', '/provider', { query: options.query });
          return { data: response.data };
        },
      },
    };
  }

  async getSessions(query = {}) {
    return this.request('GET', '/api/session', { query });
  }

  async listCommands(query = {}) {
    return this.request('GET', '/command', { query });
  }

  async runSessionCommand(sessionID, command, args = '', query = {}) {
    if (sessionID) this.activeSessionId = sessionID;
    return this.request('POST', `/session/${sessionID}/command`, {
      query,
      body: { command, arguments: args },
    });
  }

  async abortSession(sessionID, query = {}) {
    if (sessionID) this.activeSessionId = sessionID;
    return this.request('POST', `/session/${sessionID}/abort`, { query, body: {} });
  }

  ndjsonPath() {
    return path.join(this.workDir, '.wanxiangshu.ndjson');
  }

  async readNdjson() {
    const p = this.ndjsonPath();
    if (!fs.existsSync(p)) return '';
    return fs.readFileSync(p, 'utf8');
  }

  async waitForNdjson(minLines = 1, timeoutMs = 1000) {
    const p = this.ndjsonPath();
    const deadline = Date.now() + timeoutMs;
    while (Date.now() < deadline) {
      if (fs.existsSync(p)) {
        const text = fs.readFileSync(p, 'utf8').trim();
        const lines = text ? text.split('\n').filter((l) => l.trim()) : [];
        if (lines.length >= minLines) return true;
      }
      await new Promise((r) => setTimeout(r, 50));
    }
    return false;
  }

  async waitForIdle(sessionID, timeoutMs = 1000) {
    const deadline = Date.now() + timeoutMs;
    let sawNonIdle = false;
    while (Date.now() < deadline) {
      const res = await this.request('GET', '/session/status', {});
      if (res.ok && res.data) {
        const statusMap = res.data.data || res.data;
        const status = statusMap[sessionID];
        if (status) {
          if (status.type === 'idle') {
            if (sawNonIdle) return true;
          } else {
            sawNonIdle = true;
          }
        } else if (sawNonIdle) {
          // session was observed as non-idle and has now disappeared — treat as idle
          return true;
        }
      }
      await new Promise((r) => setTimeout(r, 100));
    }
    return false;
  }

  partsText(data) {
    const parts = data?.parts ?? data?.data?.parts;
    if (!Array.isArray(parts)) return JSON.stringify(data ?? '');
    return parts
      .map((p) => (typeof p?.text === 'string' ? p.text : ''))
      .filter(Boolean)
      .join('\n');
  }

  allMessagesText(messagesPayload) {
    const data = messagesPayload?.data ?? messagesPayload;
    const list = Array.isArray(data) ? data : [];
    const chunks = [];
    for (const msg of list) {
      const parts = msg?.parts;
      if (!Array.isArray(parts)) continue;
      for (const p of parts) {
        if (typeof p?.text === 'string' && p.text) {
          chunks.push(p.text);
        } else {
          const output = p?.state?.output ?? p?.output;
          if (typeof output === 'string' && output) {
            chunks.push(output);
          } else if (output && typeof output.content === 'string' && output.content) {
            chunks.push(output.content);
          }
          const error = p?.state?.error ?? p?.error;
          if (typeof error === 'string' && error) {
            chunks.push(error);
          }
        }
      }
    }
    return chunks.join('\n');
  }

  async waitForCalls(count, timeoutMs = 1000) {
    if (process.env.WANXIANG_E2E_DEBUG) {
      console.error('[HARNESS_DEBUG]', Date.now(), 'waitForCalls start, want=', count, 'have=', this.mockLLM.calls.length);
    }
    const deadline = Date.now() + timeoutMs;
    while (this.mockLLM.calls.length < count) {
      if (Date.now() > deadline) {
        throw new Error(`timed out waiting for ${count} llm calls; saw ${this.mockLLM.calls.length}`);
      }
      await new Promise((r) => setTimeout(r, 20));
    }
    return this.mockLLM.calls.length;
  }

  async readFile(relPath) {
    const p = path.join(this.workDir, relPath);
    return fs.readFileSync(p, 'utf8');
  }

  async fileExists(relPath) {
    const p = path.join(this.workDir, relPath);
    return fs.existsSync(p);
  }

  async waitForFile(relPath, timeoutMs = 1000) {
    const deadline = Date.now() + timeoutMs;
    const absPath = path.join(this.workDir, relPath);
    while (Date.now() < deadline) {
      if (fs.existsSync(absPath)) return true;
      await new Promise((r) => setTimeout(r, 50));
    }
    return false;
  }

  async dispose() {
    // 1. Stop mock LLM
    if (this.mockLLM?.stop) {
      await this.mockLLM.stop().catch(() => {});
    }

    // 2. Abort SSE permission responder
    if (this._permissionAbort) this._permissionAbort.abort();
    if (this._permissionPromise) await this._permissionPromise.catch(() => {});

    // 3. Abort tracked sessions
    for (const sid of this._trackedSessionIds) {
      try { await this.abortSession(sid); } catch {}
    }

    // 4. SIGTERM → wait → SIGKILL
    if (this._child) {
      try { this._child.kill('SIGTERM'); } catch {}
      try {
        await new Promise((resolve) => {
          const timeout = setTimeout(() => {
            this._child.kill('SIGKILL');
            resolve();
          }, 5000);
          this._child.on('exit', () => {
            clearTimeout(timeout);
            resolve();
          });
        });
      } catch {}
    }

    // 5. Delete temp dir
    if (this.home) {
      try { fs.rmSync(this.home, { recursive: true, force: true }); } catch {}
    }
  }
}

export async function start(opts = {}) {
  const variant = opts.variant || 'opencode';
  // When cleanEnv / freshHost / fresh is set, always spawn a fresh host instead of
  // using the singleton.  cleanEnv defaults to true in isolatedEnv(), so a plain call
  // with no opts also gets a fresh host.
  const forceFresh = (opts.cleanEnv ?? true) || opts.freshHost || opts.fresh;
  let sharedHost;
  if (forceFresh) {
    sharedHost = await spawnOpencodeHost(variant, opts);
  } else {
    sharedHost = await hostSingletonManager.getHost(variant, () => spawnOpencodeHost(variant, opts));
  }
  return new OpencodeHarness(sharedHost);
}
