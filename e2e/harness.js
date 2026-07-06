import { execSync, spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createMockLLM } from './mock-llm.js';
import {
  E2E_LOCK,
  releaseE2eLock,
  gitInit,
  isolatedEnv,
  waitForListening,
} from './harness-bootstrap.js';

export async function start(opts = {}) {
  // Exclusive lock: parallel e2e runs kill each other's opencode serve via
  // pkill. Acquire before any pkill so a second start() dies immediately
  // instead of reaping the first run's server.
  try {
    fs.openSync(E2E_LOCK, 'wx');
  } catch (e) {
    if (e.code === 'EEXIST') {
      throw new Error('e2e already running (lock exists at ' + E2E_LOCK + ')');
    }
    throw e;
  }

  process.once('exit', releaseE2eLock);

  // Stale opencode serve from a prior e2e run that failed dispose can hold the
  // port and cause the next start() to bind on a dying process. Reap them
  // before spawn; pkill -f exits non-zero when nothing matches, so swallow.
  try { execSync("pkill -f 'opencode serve'", { stdio: 'ignore' }); } catch {}
  await new Promise((r) => setTimeout(r, 500));

  const llm = createMockLLM();
  const llmHandle = await llm.start();

  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiang-e2e-'));
  const workDir = path.join(home, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });
  gitInit(workDir);

  const env = isolatedEnv(home, `${llmHandle.url}/v1`, opts);

  const child = spawn('opencode', ['serve', '--port', '0', '--hostname', '127.0.0.1'], {
    cwd: workDir,
    env: { ...process.env, ...env },
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  });

  let listenLine;
  try {
    listenLine = await waitForListening(child.stdout, child);
  } catch (err) {
    child.kill('SIGKILL');
    const stderr = await new Promise((resolve) => {
      const t = setTimeout(() => resolve(''), 2000);
      child.stderr.once('data', (c) => { clearTimeout(t); resolve(c.toString()); });
    });
    throw new Error('opencode serve failed: ' + err.message + '\nstderr: ' + stderr);
  }

  if (!listenLine) {
    child.kill('SIGKILL');
    throw new Error('waitForListening returned empty/undefined');
  }

  const m = listenLine.match(/http:\/\/127\.0\.0\.1:(\d+)/) || listenLine.match(/http:\/\/localhost:(\d+)/) || listenLine.match(/:(\d+)/);
  const port = m ? Number(m[1]) : 0;
  const baseUrl = `http://127.0.0.1:${port}`;

  // 5. HTTP helpers
  async function request(method, urlPath, { query, body, headers } = {}) {
    const qs = query ? '?' + new URLSearchParams(query).toString() : '';
    const url = baseUrl + urlPath + qs;
    const opts = {
      method,
      headers: {
        'Content-Type': 'application/json',
        'x-opencode-directory': workDir,
        ...(headers || {}),
      },
    };
    if (body !== undefined) opts.body = JSON.stringify(body);
    const res = await fetch(url, opts);
    const text = await res.text();
    let data;
    try { data = JSON.parse(text); } catch { data = text; }
    return { status: res.status, ok: res.ok, data };
  }

  const api = {
    port,
    baseUrl,
    mockLLM: llmHandle,
    workDir,
    home,

    async createSession(body = { model: { id: 'test-model', providerID: 'test' } }, query = {}) {
      return request('POST', '/api/session', { query, body });
    },

    async sendPrompt(sessionID, text, query = {}, timeoutMs = 120000) {
      const qs = query ? '?' + new URLSearchParams(query).toString() : '';
      const url = baseUrl + `/session/${sessionID}/message` + qs;
      const body = { parts: [{ type: 'text', text }], model: { providerID: 'test', modelID: 'test-model' } };
      const ac = new AbortController();
      const timeout = setTimeout(() => ac.abort(), timeoutMs);
      try {
        const res = await fetch(url, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'x-opencode-directory': workDir },
          body: JSON.stringify(body),
          signal: ac.signal,
        });
        const reader = res.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';
        for (;;) {
          const { value, done } = await reader.read();
          if (done) break;
          buffer += decoder.decode(value, { stream: true });
          if (buffer.includes('data: [DONE]')) break;
        }
        return { status: res.status, ok: res.ok, data: buffer };
      } catch (e) {
        return { status: 0, ok: false, data: e.message };
      } finally {
        clearTimeout(timeout);
      }
    },

    async getMessages(sessionID, query = {}) {
      return request('GET', `/session/${sessionID}/message`, { query });
    },

    async getSessions(query = {}) {
      return request('GET', '/api/session', { query });
    },

    async listCommands(query = {}) {
      return request('GET', '/command', { query });
    },

    async runSessionCommand(sessionID, command, args = '', query = {}) {
      return request('POST', `/session/${sessionID}/command`, {
        query,
        body: { command, arguments: args },
      });
    },

    async abortSession(sessionID, query = {}) {
      return request('POST', `/session/${sessionID}/abort`, { query, body: {} });
    },

    ndjsonPath() {
      return path.join(workDir, '.wanxiangshu.ndjson');
    },

    async readNdjson() {
      const p = path.join(workDir, '.wanxiangshu.ndjson');
      if (!fs.existsSync(p)) return '';
      return fs.readFileSync(p, 'utf8');
    },

    async waitForNdjson(minLines = 1, timeoutMs = 15000) {
      const p = path.join(workDir, '.wanxiangshu.ndjson');
      const deadline = Date.now() + timeoutMs;
      while (Date.now() < deadline) {
        if (fs.existsSync(p)) {
          const text = fs.readFileSync(p, 'utf8').trim();
          const lines = text ? text.split('\n').filter((l) => l.trim()) : [];
          if (lines.length >= minLines) return true;
        }
        await new Promise((r) => setTimeout(r, 200));
      }
      return false;
    },

    partsText(data) {
      const parts = data?.parts ?? data?.data?.parts;
      if (!Array.isArray(parts)) return JSON.stringify(data ?? '');
      return parts
        .map((p) => (typeof p?.text === 'string' ? p.text : ''))
        .filter(Boolean)
        .join('\n');
    },

    allMessagesText(messagesPayload) {
      const data = messagesPayload?.data ?? messagesPayload;
      const list = Array.isArray(data) ? data : [];
      const chunks = [];
      for (const msg of list) {
        const parts = msg?.parts;
        if (!Array.isArray(parts)) continue;
        for (const p of parts) {
          if (typeof p?.text === 'string' && p.text) chunks.push(p.text);
        }
      }
      return chunks.join('\n');
    },

    async waitForCalls(count, timeoutMs = 15000) {
      const deadline = Date.now() + timeoutMs;
      while (llmHandle.calls.length < count) {
        if (Date.now() > deadline) {
          throw new Error(`timed out waiting for ${count} llm calls; saw ${llmHandle.calls.length}`);
        }
        await new Promise((r) => setTimeout(r, 50));
      }
      return llmHandle.calls.length;
    },

    async readFile(relPath) {
      return fs.readFileSync(path.join(workDir, relPath), 'utf8');
    },

    async fileExists(relPath) {
      return fs.existsSync(path.join(workDir, relPath));
    },

    async waitForFile(relPath, timeoutMs = 10000) {
      const deadline = Date.now() + timeoutMs;
      const absPath = path.join(workDir, relPath);
      while (Date.now() < deadline) {
        if (fs.existsSync(absPath)) return true;
        await new Promise((r) => setTimeout(r, 500));
      }
      return false;
    },

    async dispose() {
      releaseE2eLock();
      await llmHandle.stop().catch(() => {});
      try {
        if (child.exitCode === null) {
          child.kill('SIGTERM');
          await new Promise((resolve) => {
            const t = setTimeout(() => {
              try { child.kill('SIGKILL'); } catch {}
              resolve();
            }, 5000);
            child.once('exit', () => { clearTimeout(t); resolve(); });
          });
        }
      } catch {}
      try {
        const lockPath = path.join(workDir, '.wanxiangshu.ndjson.lock');
        if (fs.existsSync(lockPath)) fs.rmSync(lockPath);
      } catch {}
      try { fs.rmSync(home, { recursive: true, force: true }); } catch {}
    },
  };

  // opencode >=1.17 blocks the first prompt on a one-time `@opencode-ai/plugin`
  // dependency install (arborist reify) during plugin bootstrap. Pre-pay that
  // cost here with a generous timeout so per-test prompts can keep a tight one.
  if (opts.plugin) {
    try {
      const warmSession = await api.createSession().then((r) => r.data?.data?.id);
      if (warmSession) {
        llmHandle.expectText('warmup');
        await api.sendPrompt(warmSession, 'warmup', {}, 90000);
        llmHandle.reset();
      }
    } catch (e) {
      releaseE2eLock();
      throw e;
    }
  }

  if (process.env.WANXIANG_E2E_VERBOSE === '1') {
    child.stdout.on('data', (data) => process.stdout.write(data));
    child.stderr.on('data', (data) => process.stderr.write(data));
  }

  return api;
}

