/**
 * isolated-env.js — Per-scenario environment isolation.
 *
 * Creates a fully isolated environment with temporary HOME, XDG dirs, TMPDIR,
 * and overrides variables that could leak state between scenarios.
 *
 * Usage:
 *   const env = createIsolatedEnv({
 *     scenarioDir: '/tmp/scenario-xxx',
 *     llmUrl: 'http://127.0.0.1:9999/v1',
 *   });
 *   const child = spawn('opencode', ['serve', '--port', '0'], {
 *     env: { ...process.env, ...env },
 *   });
 *
 * NOTE: The returned object only contains vars that MUST be overridden.
 * Callers should spread process.env first, then spread this result.
 * To truly isolate, callers may also omit process.env entirely and use
 * a minimal set of base vars (PATH, etc).
 *
 * Per the E2E contract:
 *   One scenario = one temp HOME + one temp XDG + one workspace
 *                + one mock provider + one opencode process.
 */

import fs from 'node:fs';
import path from 'node:path';
import { provisionPluginDependency } from './plugin-dependency.js';

/**
 * Generate the OpenCode config for a mock provider.
 */
function makeConfig(llmUrl, pluginPaths = [], opts = {}) {
  const modelDef = {
    id: 'test-model',
    name: 'Test Model',
    attachment: false,
    reasoning: false,
    temperature: false,
    tool_call: true,
    release_date: '2025-01-01',
    limit: {
      context: opts.contextLimit ?? 100000,
      input: opts.contextLimit ?? 100000,
      output: 10000,
    },
    cost: { input: 0, output: 0 },
    options: {},
  };
  const modelBDef = {
    ...modelDef,
    id: 'test-model-b',
    name: 'Test Model B',
  };
  return {
    formatter: false,
    lsp: false,
    permission: { '*': 'allow' },
    model: opts.model || 'test/test-model',
    provider: {
      test: {
        name: 'Test',
        id: 'test',
        env: [],
        npm: '@ai-sdk/openai-compatible',
        models: { 'test-model': { ...modelDef }, 'test-model-b': { ...modelBDef } },
        options: { apiKey: opts.apiKey || 'test-key', baseURL: `${llmUrl}` },
      },
      opencode: {
        name: 'OpenCode',
        id: 'opencode',
        env: [],
        npm: '@ai-sdk/openai-compatible',
        models: { 'test-model': { ...modelDef } },
        options: { apiKey: opts.apiKey || 'test-key', baseURL: `${llmUrl}` },
      },
    },
    plugin: pluginPaths,
  };
}

/**
 * Create a fixture uvx shim that redirects to the stealth MCP fixture.
 */
function createFixtureUvx(scenarioDir, fixturePath) {
  const dir = path.join(scenarioDir, 'mcp-bin');
  fs.mkdirSync(dir, { recursive: true });
  const shim = path.join(dir, process.platform === 'win32' ? 'uvx.cmd' : 'uvx');
  const body = process.platform === 'win32'
    ? `@echo off\r\nnode "${fixturePath}"\r\n`
    : `#!/usr/bin/env bash\nset -euo pipefail\nexec node "${fixturePath}"\n`;
  fs.writeFileSync(shim, body, 'utf8');
  if (process.platform !== 'win32') fs.chmodSync(shim, 0o755);
  return dir;
}

/**
 * Build a fully isolated environment object.
 *
 * Returns ONLY the variables that MUST be overridden for a clean scenario.
 *
 * @param {object} opts
 * @param {string} opts.scenarioDir - Root directory for this scenario
 * @param {string} opts.llmUrl - Mock LLM base URL (e.g. "http://127.0.0.1:PORT/v1")
 * @param {string[]} [opts.pluginPaths] - Paths to plugin JS files
 * @param {object} [opts.config] - Extra config overrides
 * @param {number} [opts.contextLimit] - Provider context limit
 * @param {string} [opts.model] - Model ID string (default "test/test-model")
 * @param {string} [opts.apiKey] - Provider API key
 * @param {string} [opts.mcpFixturePath] - Path to stealth MCP fixture
 * @param {object} [opts.extraEnv] - Additional env vars for this test
 * @returns {object} Environment key-value object to merge over process.env
 */
export function createIsolatedEnv(opts) {
  const { scenarioDir, llmUrl } = opts;
  const home = path.join(scenarioDir, 'home');
  const xdgData = path.join(scenarioDir, 'xdg', 'data');
  const xdgConfig = path.join(scenarioDir, 'xdg', 'config');
  const xdgCache = path.join(scenarioDir, 'xdg', 'cache');
  const xdgState = path.join(scenarioDir, 'xdg', 'state');
  const tmpDir = path.join(scenarioDir, 'tmp');

  for (const dir of [home, xdgData, xdgConfig, xdgCache, xdgState, tmpDir]) {
    fs.mkdirSync(dir, { recursive: true });
  }
  provisionPluginDependency(path.join(xdgConfig, 'opencode'));

  const realCacheDir = process.env.XDG_CACHE_HOME || path.join(process.env.HOME || '', '.cache');
  if (fs.existsSync(realCacheDir)) {
    const src = path.join(realCacheDir, 'tree-sitter-language-pack');
    const dest = path.join(xdgCache, 'tree-sitter-language-pack');
    if (fs.existsSync(src) && !fs.existsSync(dest)) {
      try { fs.symlinkSync(src, dest, 'dir'); } catch {}
    }
  }

  const repoNodeModules = path.resolve(process.cwd(), 'node_modules');
  if (fs.existsSync(repoNodeModules)) {
    const destNodeModules = path.join(scenarioDir, 'node_modules');
    if (!fs.existsSync(destNodeModules)) {
      try { fs.symlinkSync(repoNodeModules, destNodeModules, 'dir'); } catch {}
    }
  }

  const mockApiBase = llmUrl.replace(/\/v1$/, '') + '/api';
  const config = makeConfig(llmUrl, opts.pluginPaths, opts);
  const fixturePath = opts.mcpFixturePath || '';
  const fixtureUvxDir = opts.mcpFixturePath
    ? createFixtureUvx(scenarioDir, opts.mcpFixturePath)
    : null;

  const extraEnv = opts.extraEnv || {};
  return {
    // extraEnv first, so non-isolation variables pass through; isolation vars below will always win.
    ...extraEnv,
    // Isolation dirs
    HOME: home,
    USERPROFILE: home,
    XDG_DATA_HOME: xdgData,
    XDG_CONFIG_HOME: xdgConfig,
    XDG_CACHE_HOME: xdgCache,
    XDG_STATE_HOME: xdgState,
    TMPDIR: tmpDir,
    TMP: tmpDir,
    TEMP: tmpDir,

    // OpenCode configuration
    NO_PROXY: '*',
    OPENCODE_OFFLINE: '1',
    OPENCODE_TELEMETRY_DISABLED: '1',
    CHECK_UPDATE: '0',
    DISABLE_TELEMETRY: '1',
    OPENCODE_DISABLE_AUTOUPDATE: '1',
    OPENCODE_DISABLE_AUTOCOMPACT: '1',
    OPENCODE_DISABLE_MODELS_FETCH: '1',
    OPENCODE_AUTH_CONTENT: '{}',
    OPENCODE_EXPERIMENTAL_EVENT_SYSTEM: 'true',
    OPENCODE_ENABLE_EXA: '0',
    OPENCODE_CONFIG_CONTENT: JSON.stringify(config),
    OPENCODE_PERMISSION: JSON.stringify({ '*': 'allow' }),

    // Mock LLM
    OLLAMA_API_KEY: opts.apiKey || 'test-key',
    OLLAMA_API_BASE: mockApiBase,

    // Prevent Bun/Node debug ports and heap profiling from leaking into spawned opencode
    NODE_PATH: `${path.join(scenarioDir, 'node_modules')}${path.delimiter}${repoNodeModules}${path.delimiter}${extraEnv.NODE_PATH || process.env.NODE_PATH || ''}`,
    NODE_OPTIONS: '',
    BUN_OPTIONS: '',

    // Zero-out the fallback continuation rate-limit governor in E2E.
    WANXIANGSHU_TEST: 'true',

    // Optional MCP fixture
    STEALTH_BROWSER_MCP_FIXTURE: fixturePath,
    PATH: fixtureUvxDir
      ? `${fixtureUvxDir}${path.delimiter}${extraEnv.PATH || process.env.PATH || ''}`
      : (extraEnv.PATH || process.env.PATH || ''),
  };
}
