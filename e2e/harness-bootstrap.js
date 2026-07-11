import { execSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
let WANXIANG_ROOT = path.resolve(__dirname, '..');
let PLUGIN_JS = path.resolve(WANXIANG_ROOT, 'build/src/Opencode/Plugin.js');
if (!fs.existsSync(PLUGIN_JS)) {
  WANXIANG_ROOT = path.resolve(__dirname, '../..');
  PLUGIN_JS = path.resolve(WANXIANG_ROOT, 'build/src/Opencode/Plugin.js');
}
const PLUGIN_URL = pathToFileURL(PLUGIN_JS).href;
const FIXTURE_MCP = path.resolve(__dirname, 'stealth-mcp-fixture.js');
export const E2E_LOCK = '/tmp/wanxiang-e2e.lock';

export function releaseE2eLock() {
  try { fs.unlinkSync(E2E_LOCK); } catch {}
}

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

export function gitInit(dir) {
  execSync('git init', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.email test@example.com', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.name test', { cwd: dir, stdio: 'ignore' });
  fs.writeFileSync(path.join(dir, 'README.md'), '# test\n');
  fs.writeFileSync(path.join(dir, 'AGENTS.md'), '- e2e test workspace\n');
  execSync('git add README.md AGENTS.md', { cwd: dir, stdio: 'ignore' });
  execSync('git commit -m init', { cwd: dir, stdio: 'ignore' });
}

export function isolatedEnv(home, llmUrl, opts = {}) {
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
            limit: { context: opts.contextLimit ?? 100000, input: opts.contextLimit ?? 100000, output: 10000 },
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

export async function waitForListening(stdout, child, timeoutMs = 30000) {
  let buf = '';
  let exitHandler;
  let dataHandler;
  return await new Promise((resolve, reject) => {
    const deadline = Date.now() + timeoutMs;
    exitHandler = (code, signal) => {
      reject(new Error(`opencode serve exited with code=${code} signal=${signal}`));
    };
    child.on('exit', exitHandler);
    dataHandler = (chunk) => {
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
    };
    stdout.on('data', dataHandler);
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
    check();
  }).finally(() => {
    child.removeListener('exit', exitHandler);
    stdout.removeListener('data', dataHandler);
  });
}