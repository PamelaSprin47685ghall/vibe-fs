import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawn } from 'node:child_process';
import { PLUGIN_JS, gitInit } from './git.js';
import { releaseLock } from './lock.js';
import { createMockLLM } from '../mock-llm.js';
import { hostSingletonManager } from '../harness-bootstrap.js';

const E2E_META = '.wanxiangzhen-e2e-meta.json';
const NDJSON = '.wanxiangshu.ndjson';

function resolveAuthToken(authToken, meta) {
  if (authToken === '__NO_AUTH__') return null;
  return (!authToken) ? meta.token : authToken;
}

function buildConfigJson(llmUrl) {
  const pluginUrl = PLUGIN_JS;
  return {
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
        options: { apiKey: 'test-key', baseURL: `${llmUrl}/v1` },
      },
    },
    plugin: [[pluginUrl, { e2e: true }]],
  };
}

function spawnOpencode(tmpDir, home, config, llmUrl) {
    const E2E_CACHE_HOME = process.env.WANXIANG_E2E_CACHE_HOME || path.join(os.tmpdir(), 'wanxiang-e2e-cache');
    const xdg = path.join(home, 'xdg');
    
    // Set variables on process.env directly to ensure propagation through Bun wrappers
    process.env.HOME = process.env.HOME || process.env.USERPROFILE || home;
    process.env.XDG_CACHE_HOME = E2E_CACHE_HOME;
    process.env.XDG_DATA_HOME = xdg;
    process.env.XDG_CONFIG_HOME = xdg;
    process.env.XDG_STATE_HOME = xdg;
    process.env.OPENCODE_DISABLE_AUTOUPDATE = '1';
    process.env.OPENCODE_DISABLE_AUTOCOMPACT = '1';
    process.env.OPENCODE_DISABLE_MODELS_FETCH = '1';
    process.env.OPENCODE_AUTH_CONTENT = '{}';
    process.env.OPENCODE_EXPERIMENTAL_EVENT_SYSTEM = 'true';
    process.env.OPENCODE_PRINT_LOGS = '1';
    process.env.WANXIANGZHEN_E2E = '1';
    process.env.WANXIANG_E2E_SANDBOX = '1';
    process.env.OPENCODE_PLUGIN = PLUGIN_JS;
    process.env.OPENCODE_CONFIG_CONTENT = JSON.stringify(config);
    process.env.OLLAMA_API_BASE = `${llmUrl}/api`;
    process.env.OLLAMA_API_KEY = 'test-key';

  return spawn('opencode', ['serve', '--port', '0', '--hostname', '127.0.0.1'], { cwd: tmpDir, env: process.env, stdio: ['ignore', 'pipe', 'pipe'] });
}

function spawnOpencodeChildAndGetPort(tmpDir, home, llmUrl) {
  return new Promise((resolve, reject) => {
    const config = buildConfigJson(llmUrl);
    const child = spawnOpencode(tmpDir, home, config, llmUrl);

    let buf = '';
    const onData = (chunk) => {
      buf += chunk.toString();
      process.stderr.write(`[opencode-stdout] ${chunk.toString()}`);
      const m = buf.match(/opencode server listening on http:\/\/127\.0\.0\.1:(\d+)/) || buf.match(/http:\/\/127\.0\.0\.1:(\d+)/);
      if (m) {
        child.stdout.removeListener('data', onData);
        console.log(`[Wanxiangzhen] spawnOpencodeChildAndGetPort resolved, port = ${m[1]}`);
        resolve({ child, port: Number(m[1]) });
      }
    };
    child.stdout.on('data', onData);
    child.stderr.on('data', (chunk) => {
      process.stderr.write(`[opencode-stderr] ${chunk.toString()}`);
    });
    child.on('error', (err) => reject(err));
    child.on('exit', (code) => reject(new Error(`opencode exited with code ${code}`)));
    setTimeout(() => reject(new Error('opencode serve timeout')), 30000);
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

export function assembleServeHarness(tmpDir, home, child, meta, llmHandle) {
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
      try { fs.rmSync(path.join(tmpDir, NDJSON), { force: true }); } catch {}
    },
  };
}

async function warmupOpencodeChild(port, tmpDir) {
  const warmupUrl = `http://127.0.0.1:${port}/api/session`;
  try {
    console.log(`[Wanxiangzhen] Calling warmupUrl: ${warmupUrl}`);
    const res = await fetch(warmupUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'x-opencode-directory': tmpDir },
      body: JSON.stringify({ model: { id: 'test-model', providerID: 'test' } }),
    });
    console.log(`[Wanxiangzhen] warmupUrl resolved: status=${res.status}`);

    // Trigger InstanceContextMiddleware by calling /session/status
    const statusUrl = `http://127.0.0.1:${port}/session/status`;
    console.log(`[Wanxiangzhen] Calling statusUrl to trigger plugin loading: ${statusUrl}`);
    const statusRes = await fetch(statusUrl, {
      method: 'GET',
      headers: { 'x-opencode-directory': tmpDir },
    });
    console.log(`[Wanxiangzhen] statusUrl resolved: status=${statusRes.status}`);
  } catch (fetchErr) {
    console.error('WARMUP FETCH ERROR:', fetchErr);
  }
}

async function spawnWanxiangzhenHost(opts) {
  const llm = createMockLLM();
  const llmHandle = await llm.start();
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'wxz-e2e-home-'));
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxz-e2e-'));
  const metaDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxz-e2e-meta-'));
  process.env.WANXIANGZHEN_E2E_META_DIR = metaDir;
  gitInit(tmpDir, opts);

  const nodeModulesSource = path.resolve('node_modules');
  if (fs.existsSync(nodeModulesSource)) {
    try { fs.symlinkSync(nodeModulesSource, path.join(tmpDir, 'node_modules'), 'dir'); } catch {}
    try { fs.symlinkSync(nodeModulesSource, path.join(home, 'node_modules'), 'dir'); } catch {}
    try { fs.copyFileSync(path.resolve('package.json'), path.join(tmpDir, 'package.json')); } catch {}
    try { fs.copyFileSync(path.resolve('package-lock.json'), path.join(tmpDir, 'package-lock.json')); } catch {}
    try { fs.copyFileSync(path.resolve('package.json'), path.join(home, 'package.json')); } catch {}
    try { fs.copyFileSync(path.resolve('package-lock.json'), path.join(home, 'package-lock.json')); } catch {}
  }

  let child;
  let port;
  try {
    ({ child, port } = await spawnOpencodeChildAndGetPort(tmpDir, home, llmHandle.url));
    await warmupOpencodeChild(port, tmpDir);
  } catch (e) {
    await llmHandle.stop().catch(() => {});
    console.log(`[Wanxiangzhen] keeping tmpDir: ${tmpDir} home: ${home}`);
    throw e;
  }

    const metaPath = path.join(metaDir, E2E_META);
    let meta;
    try {
      meta = await waitForMetaFile(metaPath, child);
  } catch (e) {
    await llmHandle.stop().catch(() => {});
    child.kill('SIGKILL');
    console.log(`[Wanxiangzhen] keeping tmpDir: ${tmpDir} home: ${home}`);
    throw e;
  }
  return { child, mockLLM: llmHandle, tmpDir, home, meta, port };
}

export async function startOpencode(opts) {
  const sharedHost = await hostSingletonManager.getHost('wanxiangzhen', () => spawnWanxiangzhenHost(opts));
  return assembleServeHarness(sharedHost.tmpDir, sharedHost.home, sharedHost.child, sharedHost.meta, sharedHost.mockLLM);
}