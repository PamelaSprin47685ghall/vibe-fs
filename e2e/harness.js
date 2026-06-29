import http from 'node:http';
import { execSync, spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createMockLLM } from './mock-llm.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const WANXIANG_ROOT = path.resolve(__dirname, '..');
const PLUGIN_JS = path.resolve(WANXIANG_ROOT, 'build/src/Opencode/Plugin.js');

function createPluginStub(targetDir) {
  const pluginDir = path.join(targetDir, '.opencode', 'plugin');
  fs.mkdirSync(pluginDir, { recursive: true });
  fs.writeFileSync(
    path.join(pluginDir, 'wanxiangshu.js'),
    `export { plugin } from '${PLUGIN_JS.replace(/\\/g, '\\\\')}';
`,
    'utf-8',
  );
}

function gitInit(dir) {
  execSync('git init', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.email test@example.com', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.name test', { cwd: dir, stdio: 'ignore' });
  fs.writeFileSync(path.join(dir, 'README.md'), '# test\n');
  execSync('git add README.md', { cwd: dir, stdio: 'ignore' });
  execSync('git commit -m init', { cwd: dir, stdio: 'ignore' });
}

function isolatedEnv(home, llmUrl) {
  const xdg = path.join(home, 'xdg');
  return {
    OPENCODE_TEST_HOME: home,
    HOME: home,
    XDG_DATA_HOME: xdg,
    XDG_CACHE_HOME: xdg,
    XDG_CONFIG_HOME: xdg,
    XDG_STATE_HOME: xdg,
    OPENCODE_CONFIG_CONTENT: JSON.stringify({
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
    }),
    OPENCODE_DISABLE_PROJECT_CONFIG: '1',
    OPENCODE_PURE: '1',
    OPENCODE_DISABLE_AUTOUPDATE: '1',
    OPENCODE_DISABLE_AUTOCOMPACT: '1',
    OPENCODE_DISABLE_MODELS_FETCH: '1',
    OPENCODE_AUTH_CONTENT: '{}',
    OPENCODE_EXPERIMENTAL_EVENT_SYSTEM: 'true',
  };
}

async function waitForListening(stdout, timeoutMs = 30000) {
  let buf = '';
  return await new Promise((resolve, reject) => {
    const deadline = Date.now() + timeoutMs;
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
      buf += chunk.toString();
      const idx = buf.indexOf('\n');
      if (idx !== -1) {
        const line = buf.slice(0, idx).trim();
        buf = buf.slice(idx + 1);
        if (line.includes('opencode server listening on http://')) {
          resolve(line);
        }
      }
    });
  });
}

export async function start(_opencodeIndex) {
  // 1. mock LLM
  const llm = createMockLLM();
  const llmHandle = await llm.start();

  // 2. workspace
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiang-e2e-'));
  const workDir = path.join(home, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });
  gitInit(workDir);
  createPluginStub(workDir);

  // 3. env
  const env = isolatedEnv(home, `${llmHandle.url}/v1`);

  // 4. spawn opencode serve
  const child = spawn('opencode', ['serve', '--port', '0', '--hostname', '127.0.0.1'], {
    cwd: workDir,
    env: { ...process.env, ...env },
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  });

  let listenLine;
  try {
    listenLine = await waitForListening(child.stdout);
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
  const m = listenLine.match(/http:\/\/[^:]+:(\d+)/);
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

    async sendPrompt(sessionID, text, query = {}) {
      return request('POST', `/session/${sessionID}/message`, {
        query,
        body: { parts: [{ type: 'text', text }], model: { providerID: 'test', modelID: 'test-model' } },
      });
    },

    async getMessages(sessionID, query = {}) {
      return request('GET', `/api/session/${sessionID}/message`, { query });
    },

    async getSessions(query = {}) {
      return request('GET', '/api/session', { query });
    },

    async dispose() {
      await llmHandle.stop().catch(() => {});
      child.kill('SIGKILL');
      await new Promise((r) => child.on('exit', r));
      try { fs.rmSync(home, { recursive: true, force: true }); } catch {}
    },
  };

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
