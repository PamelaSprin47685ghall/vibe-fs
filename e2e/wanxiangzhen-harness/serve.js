import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { PLUGIN_JS, gitInit } from './git.js';
import { releaseLock } from './lock.js';
import { waitForListening } from '../harness-bootstrap.js';

const E2E_META = '.wanxiangzhen-e2e-meta.json';
const NDJSON = '.wanxiangshu.ndjson';

function resolveAuthToken(authToken, meta) {
  if (authToken === '__NO_AUTH__') return null;
  return (!authToken) ? meta.token : authToken;
}

export function spawnOpencodeChildAndGetPort(tmpDir, home) {
  return new Promise((resolve, reject) => {
    const pluginUrl = new URL('file://' + PLUGIN_JS).href;
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
          options: { apiKey: 'test-key', baseURL: 'http://127.0.0.1:0' },
        },
      },
      plugin: [pluginUrl],
    };
    const env = {
      ...process.env,
      HOME: home,
      OPENCODE_DISABLE_AUTOUPDATE: '1',
      OPENCODE_DISABLE_AUTOCOMPACT: '1',
      OPENCODE_DISABLE_MODELS_FETCH: '1',
      OPENCODE_AUTH_CONTENT: '{}',
      OPENCODE_EXPERIMENTAL_EVENT_SYSTEM: 'true',
      WANXIANGZHEN_E2E: '1',
      OPENCODE_PLUGIN: PLUGIN_JS,
      OPENCODE_CONFIG_CONTENT: JSON.stringify(config),
    };
    const child = spawn('opencode', ['serve', '--port', '0', '--hostname', '127.0.0.1'], { cwd: tmpDir, env, stdio: ['ignore', 'pipe', 'pipe'] });
    
    let buf = '';
    const onData = (chunk) => {
      buf += chunk.toString();
      const m = buf.match(/opencode server listening on http:\/\/127\.0\.0\.1:(\d+)/) || buf.match(/http:\/\/localhost:(\d+)/) || buf.match(/:(\d+)/);
      if (m) {
        const port = Number(m[1]);
        child.stdout.removeListener('data', onData);
        resolve({ child, port });
      }
    };
    child.stdout.on('data', onData);
    child.on('error', (err) => reject(err));
    child.on('exit', (code) => {
      reject(new Error(`opencode exited with code ${code}`));
    });
    setTimeout(() => reject(new Error('opencode port listen timeout')), 30000);
  });
}

export async function waitForMetaFile(metaPath, child) {
  return new Promise((resolve, reject) => {
    const to = setTimeout(() => reject(new Error('opencode serve timeout')), 30000);
    const iv = setInterval(() => {
      if (fs.existsSync(metaPath)) {
        clearTimeout(to);
        clearInterval(iv);
        try {
          resolve(JSON.parse(fs.readFileSync(metaPath, 'utf-8')));
        } catch (e) { reject(e); }
      }
    }, 100);
    child.on('exit', (code) => {
      clearTimeout(to);
      clearInterval(iv);
      reject(new Error(`opencode serve exited with code ${code}`));
    });
  });
}

async function coordinatorGet(meta, p, authToken) {
  const token = resolveAuthToken(authToken, meta);
  const headers = token ? { Authorization: `Bearer ${token}` } : {};
  const res = await fetch(`${meta.coordinatorUrl}${p}`, { method: 'GET', headers });
  const body = await res.json().catch(() => ({}));
  return { status: res.status, body };
}

async function coordinatorPost(meta, p, body, authToken) {
  const token = resolveAuthToken(authToken, meta);
  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
  const res = await fetch(`${meta.coordinatorUrl}${p}`, { method: 'POST', headers, body: JSON.stringify(body) });
  const respBody = await res.json().catch(() => ({}));
  return { status: res.status, body: respBody };
}

export function assembleServeHarness(tmpDir, home, child, meta) {
  return {
    mode: 'opencode',
    tmpDir,
    child,
    token: meta.token,
    url: meta.coordinatorUrl,
    coordinatorGet: (p, tok) => coordinatorGet(meta, p, tok),
    coordinatorPost: (p, body, tok) => coordinatorPost(meta, p, body, tok),
    readMeta: () => {
      const ndjsonPath = path.join(tmpDir, NDJSON);
      return fs.existsSync(ndjsonPath) ? fs.readFileSync(ndjsonPath, 'utf-8') : '';
    },
    waitForMeta: async () => {
      const ndjsonPath = path.join(tmpDir, NDJSON);
      for (let i = 0; i < 5000; i++) {
        if (fs.existsSync(ndjsonPath) && fs.readFileSync(ndjsonPath, 'utf-8').trim().length > 0) {
          break;
        }
        await Promise.resolve();
      }
      return fs.existsSync(ndjsonPath) ? fs.readFileSync(ndjsonPath, 'utf-8') : '';
    },
    dispose: async () => {
      try { child.kill('SIGTERM'); } catch {}
      try { fs.rmSync(path.join(tmpDir, NDJSON), { force: true }); } catch {}
      try { fs.rmSync(tmpDir, { recursive: true, force: true }); } catch {}
      try { fs.rmSync(home, { recursive: true, force: true }); } catch {}
      releaseLock();
    },
  };
}

export async function startOpencode(opts) {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'wxz-e2e-home-'));
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxz-e2e-'));
  gitInit(tmpDir, opts);

  let child;
  let port;
  try {
    ({ child, port } = await spawnOpencodeChildAndGetPort(tmpDir, home));
  } catch (e) {
    try { fs.rmSync(tmpDir, { recursive: true, force: true }); } catch {}
    try { fs.rmSync(home, { recursive: true, force: true }); } catch {}
    throw e;
  }

  const metaPath = path.join(tmpDir, E2E_META);

  let meta;
  try {
    const warmupUrl = `http://127.0.0.1:${port}/api/session`;
    await fetch(warmupUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'x-opencode-directory': tmpDir },
      body: JSON.stringify({ model: { id: 'test-model', providerID: 'test' } }),
    }).catch(() => {});

    meta = await waitForMetaFile(metaPath, child);
  } catch (e) {
    try { child.kill('SIGKILL'); } catch {}
    try { fs.rmSync(tmpDir, { recursive: true, force: true }); } catch {}
    try { fs.rmSync(home, { recursive: true, force: true }); } catch {}
    throw e;
  }
  return assembleServeHarness(tmpDir, home, child, meta);
}