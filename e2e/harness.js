import http from 'node:http';
import { execSync, spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createMockLLM } from './mock-llm.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const WANXIANG_ROOT = path.resolve(__dirname, '..');
const PLUGIN_JS = path.resolve(WANXIANG_ROOT, 'build/src/Opencode/Plugin.js');
const PLUGIN_URL = pathToFileURL(PLUGIN_JS).href;
const FIXTURE_MCP = path.resolve(__dirname, 'stealth-mcp-fixture.js');

function createFixtureUvx(home) {
  const dir = path.join(home, 'mcp-bin');
  fs.mkdirSync(dir, { recursive: true });
  const shim = path.join(dir, process.platform === 'win32' ? 'uvx.cmd' : 'uvx');
  const body = process.platform === 'win32'
    ? `@echo off\r\nnode "%STEALTH_BROWSER_MCP_FIXTURE%"\r\n`
    : `#!/usr/bin/env bash\nset -euo pipefail\nexec node "$STEALTH_BROWSER_MCP_FIXTURE"\n`;
  fs.writeFileSync(shim, body, 'utf8');
  if (process.platform !== 'win32') fs.chmodSync(shim, 0o755);
  return dir;
}

function gitInit(dir) {
  execSync('git init', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.email test@example.com', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.name test', { cwd: dir, stdio: 'ignore' });
  fs.writeFileSync(path.join(dir, 'README.md'), '# test\n');
  fs.writeFileSync(path.join(dir, 'AGENTS.md'), '- e2e test workspace\n');
  execSync('git add README.md AGENTS.md', { cwd: dir, stdio: 'ignore' });
  execSync('git commit -m init', { cwd: dir, stdio: 'ignore' });
}

function isolatedEnv(home, llmUrl, opts = {}) {
  const xdg = path.join(home, 'xdg');
  const fixtureUvxDir = createFixtureUvx(home);
  const config = {
    formatter: false,
    lsp: false,
    model: 'test/test-model',
    provider: {
      test: {
        name: 'Test',
        id: 'test',
        env: [],
        npm: '@ai-sdk/openai-compatible',
        models: {
          'test-model': {
            id: 'test-model',
            name: 'Test Model',
            attachment: false,
            reasoning: false,
            temperature: false,
            tool_call: true,
            release_date: '2025-01-01',
            limit: { context: 100000, output: 10000 },
            cost: { input: 0, output: 0 },
            options: {},
          },
        },
        options: { apiKey: 'test-key', baseURL: llmUrl },
      },
    },
    plugin: opts.plugin ? [PLUGIN_URL] : [],
  };
  return {
    OPENCODE_TEST_HOME: home,
    HOME: home,
    XDG_DATA_HOME: xdg,
    XDG_CACHE_HOME: xdg,
    XDG_CONFIG_HOME: xdg,
    XDG_STATE_HOME: xdg,
    OPENCODE_DISABLE_AUTOUPDATE: '1',
    OPENCODE_DISABLE_AUTOCOMPACT: '1',
    OPENCODE_DISABLE_MODELS_FETCH: '1',
    OPENCODE_AUTH_CONTENT: '{}',
    OPENCODE_EXPERIMENTAL_EVENT_SYSTEM: 'true',
    OPENCODE_CONFIG_CONTENT: JSON.stringify(config),
    PATH: `${fixtureUvxDir}${path.delimiter}${process.env.PATH ?? ''}`,
    STEALTH_BROWSER_MCP_FIXTURE: FIXTURE_MCP,
  };
}

async function waitForListening(stdout, child, timeoutMs = 30000) {
  let buf = '';
  let exitHandler;
  return await new Promise((resolve, reject) => {
    const deadline = Date.now() + timeoutMs;
    exitHandler = (code, signal) => {
      reject(new Error(`opencode serve exited with code=${code} signal=${signal}`));
    };
    child.on('exit', exitHandler);
    const check = () => {
      if (buf.includes('opencode server listening on http://')) {
        resolve(buf);
        return;
      }
      if (Date.now() > deadline) {
        reject(new Error('timed out waiting for opencode serve'));
        return;
      }
      setTimeout(check, 50);
    };
    stdout.on('data', (chunk) => {
      const chunkStr = chunk.toString();
      buf += chunkStr;
      const idx = buf.indexOf('\n');
      if (idx !== -1) {
        const line = buf.slice(0, idx).trim();
        buf = buf.slice(idx + 1);
        if (line.includes('opencode server listening on http://')) {
          resolve(line);
        }
      }
    });
    check();
  }).finally(() => {
    child.removeListener('exit', exitHandler);
  });
}

export async function start(opts = {}) {
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

  const m = listenLine.match(/http:\/\/[?1h =[^:]+:(\d+)/) || listenLine.match(/http:\/\/127.0.0.1:(\d+)/) || listenLine.match(/http:\/\/localhost:(\d+)/) || listenLine.match(/:(\d+)/);
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

    async sendPrompt(sessionID, text, query = {}, timeoutMs = 15000) {
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
      return request('GET', `/api/session/${sessionID}/message`, { query });
    },

    async getSessions(query = {}) {
      return request('GET', '/api/session', { query });
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
      await llmHandle.stop().catch(() => {});
      child.kill('SIGKILL');
      await new Promise((r) => child.on('exit', r));
      try { fs.rmSync(home, { recursive: true, force: true }); } catch {}
    },
  };

  // opencode >=1.17 blocks the first prompt on a one-time `@opencode-ai/plugin`
  // dependency install (arborist reify) during plugin bootstrap. Pre-pay that
  // cost here with a generous timeout so per-test prompts can keep a tight one.
  if (opts.plugin) {
    const warmSession = await api.createSession().then((r) => r.data?.data?.id);
    if (warmSession) {
      llmHandle.expectText('warmup');
      await api.sendPrompt(warmSession, 'warmup', {}, 90000);
      llmHandle.reset();
    }
  }

  return api;
}

export function containsReadTool(msgsRes) {
  if (!msgsRes.ok) return false;
  return msgsRes.data.some((m) =>
    m.type === 'assistant' &&
    Array.isArray(m.content) &&
    m.content.some((p) => p.type === 'tool' && p.name === 'read')
  );
}
