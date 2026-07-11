import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import readline from 'node:readline';
import { createMockLLM } from './mock-llm.js';
import { hostSingletonManager } from './harness-bootstrap.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

function resolveBun() {
  return process.env.BUN ?? (() => {
    const r = spawnSync(process.platform === 'win32' ? 'where' : 'which', ['bun'], { encoding: 'utf8' });
    if (r.status === 0 && r.stdout) {
      const first = r.stdout.trim().split('\n')[0];
      if (first) return first;
    }
    throw new Error('bun not found. Install bun: https://bun.sh/docs/installation (or set BUN env var)');
  })();
}

class OmpHarness {
  constructor(sharedHost, sessionId) {
    this.sessionId = sessionId;
    this.child = sharedHost.child;
    this.mockLLM = sharedHost.mockLLM;
    this.queue = sharedHost.queue;
    this.home = sharedHost.home;
    this.tools = [];
    this.handlers = sharedHost.handlers || {};
  }

  async getToolNames() {
    const res = await this.queue({ type: 'getToolNames' });
    if (!res.ok) throw new Error(res.error || 'getToolNames failed');
    return res.data.toolNames;
  }

  async getCommands() {
    const res = await this.queue({ type: 'getCommands' });
    if (!res.ok) throw new Error(res.error || 'getCommands failed');
    return res.data;
  }

  getRemainingExpectations() {
    return this.mockLLM.getRemainingExpectations();
  }

  get calls() {
    return this.mockLLM.calls;
  }

  expectText(text) {
    this.mockLLM.expectText(text);
  }

  expectTool(tool, args) {
    this.mockLLM.expectTool(tool, args);
  }

  async runCommand(name, args, sessionId) {
    const sess = sessionId || this.sessionId;
    const res = await this.queue({ type: 'runCommand', name, args, sessionId: sess });
    if (!res.ok) throw new Error(res.error || 'runCommand failed');
    return res.data;
  }

  async triggerTool(name, params, sessionId, extraCtx) {
    const sess = sessionId || this.sessionId;
    const res = await this.queue({ type: 'callTool', toolCallId: sess, name, params, sessionId: sess });
    if (!res.ok) throw new Error(res.error || 'callTool failed');
    return res.data;
  }

  async emitEvent(name, event, sessionId) {
    const sess = sessionId || this.sessionId;
    const res = await this.queue({ type: 'emit', eventType: name, event, sessionId: sess });
    if (!res.ok) throw new Error(res.error || 'emit failed');
    return res.data;
  }

  async readNdjson() {
    const res = await this.queue({ type: 'readNdjson', sessionId: this.sessionId });
    if (!res.ok) throw new Error(res.error || 'readNdjson failed');
    return res.data.content;
  }

  async readFile(p) {
    const res = await this.queue({ type: 'readFile', path: p, sessionId: this.sessionId });
    if (!res.ok) throw new Error(res.error || 'readFile failed');
    return res.data.content;
  }

  async fileExists(p) {
    const res = await this.queue({ type: 'fileExists', path: p, sessionId: this.sessionId });
    if (!res.ok) throw new Error(res.error || 'fileExists failed');
    return res.data.exists;
  }

  async waitForNdjson(min, maxMs) {
    const ms = Math.min(maxMs, 1000);
    const res = await this.queue({ type: 'waitForNdjson', min, maxMs: ms, sessionId: this.sessionId });
    if (!res.ok) throw new Error(res.error || 'waitForNdjson failed');
    return res.data.ready;
  }

  async dispose() {
    if (this.sessionId) {
      try {
        await this.queue({ type: 'cleanSandbox', sessionId: this.sessionId });
      } catch {}
    }
  }
}

function writeAgentConfigs(agentDir, mockLlm) {
  const configContent = `model: openai/gpt-4o\n`;
  const modelsContent = `
providers:
  openai:
    baseUrl: "${mockLlm.url}/v1"
    apiKey: "test-key"
    api: "openai-completions"
    models:
      - id: "gpt-4o"
        name: "GPT-4o"
        api: "openai-completions"
        contextWindow: 128000
`;
  fs.writeFileSync(path.join(agentDir, 'config.yml'), configContent, 'utf8');
  fs.writeFileSync(path.join(agentDir, 'models.yml'), modelsContent, 'utf8');
}

function setupReadline(child) {
  const responseQueue = [];
  const waitingResolvers = [];
  const rl = readline.createInterface({ input: child.stdout, terminal: false });
  rl.on('line', (line) => {
    if (waitingResolvers.length > 0) waitingResolvers.shift()(line);
    else responseQueue.push(line);
  });

  function nextResponse() {
    if (responseQueue.length > 0) return Promise.resolve(responseQueue.shift());
    return new Promise((resolve) => waitingResolvers.push(resolve));
  }

  async function sendCommand(cmd) {
    child.stdin.write(JSON.stringify(cmd) + '\n');
    const line = await nextResponse();
    try { return JSON.parse(line); }
    catch (e) { throw new Error(`Failed to parse driver response: ${line}`); }
  }

  return { nextResponse, sendCommand };
}

function spawnProcess(ompRepo, pluginPath, mockLlm, agentDir) {
  return spawn(resolveBun(), ['run', process.env.WANXIANGSHU_OMP_DRIVER || path.resolve(__dirname, 'omp-driver.ts')], {
    cwd: ompRepo,
    env: {
      ...process.env,
      WANXIANGSHU_PLUGIN_PATH: pluginPath,
      MOCK_LLM_URL: mockLlm.url,
      PI_CODING_AGENT_DIR: agentDir,
      OPENAI_API_KEY: 'test-key',
      OLLAMA_API_KEY: 'test-key',
      OMP_REVIEW_GRACE_INITIAL_MS: '600000',
      OMP_REVIEW_GRACE_SUBSEQUENT_MS: '600000',
      OMP_EXECUTOR_MIN_WAIT: '5'
    },
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  });
}

async function spawnOmpHost(opts) {
  const mockLlmInstance = createMockLLM();
  const mockLlm = await mockLlmInstance.start();
  const tempHome = fs.mkdtempSync(path.join(os.tmpdir(), 'omp-runner-home-'));
  const agentDir = path.join(tempHome, '.omp', 'agent');
  fs.mkdirSync(agentDir, { recursive: true });
  writeAgentConfigs(agentDir, mockLlm);

  const ompRepo = process.env.WANXIANGSHU_OMP_REPO || path.resolve(__dirname, '..', '..', 'oh-my-pi');
  const pluginPath = path.resolve(__dirname, '..', 'build', 'src', 'Omp', 'Plugin.js');
  const child = spawnProcess(ompRepo, pluginPath, mockLlm, agentDir);
  child.stderr.on('data', (chunk) => process.stderr.write(`[omp-driver] ${chunk}`));

  const { nextResponse, sendCommand } = setupReadline(child);
  const readyLine = await nextResponse();
  if (readyLine.trim() !== 'ready') {
    child.kill('SIGKILL');
    mockLlm.stop().catch(() => {});
    try { fs.rmSync(tempHome, { recursive: true, force: true }); } catch {}
    throw new Error('Expected ready signal from driver, got: ' + readyLine);
  }

  const handlersRes = await sendCommand({ type: 'getHandlers' });
  const handlers = {};
  if (handlersRes.ok) {
    for (const key of handlersRes.data.handlerKeys) handlers[key] = true;
  }

  return { child, mockLLM: mockLlm, queue: sendCommand, home: tempHome, handlers };
}

export async function start(opts = {}) {
  const sharedHost = await hostSingletonManager.getHost('omp', () => spawnOmpHost(opts));
  const sessionId = 'omp-e2e-session-' + Math.random().toString(36).slice(2, 8);
  return new OmpHarness(sharedHost, sessionId);
}

export default { start };