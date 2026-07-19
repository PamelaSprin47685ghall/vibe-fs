import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import readline from 'node:readline';
import { spawn, spawnSync } from 'node:child_process';
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
    throw new Error('bun not found. Install bun (or set BUN env var)');
  })();
}

function buildConfig(mockLlmUrl, tempHome) {
  const agentDir = path.join(tempHome, '.mux', 'agent');
  fs.mkdirSync(agentDir, { recursive: true });
  return { agentDir };
}

function buildCommandQueue(child) {
  const responseQueue = [];
  const waitingResolvers = [];
  let workdirResolver = null;
  let isReady = false;
  const workdirPromise = new Promise((resolve) => { workdirResolver = resolve; });
  const rl = readline.createInterface({ input: child.stdout, terminal: false });
  rl.on('line', (line) => {
    console.log('[mux-driver-stdout] ' + line);
    const trimmed = line.trim();
    if (!isReady) {
      if (trimmed.startsWith('ready')) {
        isReady = true;
        const parts = trimmed.split('|');
        workdirResolver(parts[1] || '');
      }
      return;
    }
    if (waitingResolvers.length > 0) waitingResolvers.shift()(line);
    else responseQueue.push(line);
  });
  rl.on('close', () => {
    while (waitingResolvers.length > 0) {
      waitingResolvers.shift()(null);
    }
  });
  return {
    workdirPromise,
    rl,
    async send(cmd) {
      child.stdin.write(JSON.stringify(cmd) + '\n');
      const line = await new Promise((resolve) => {
        if (responseQueue.length > 0) resolve(responseQueue.shift());
        else waitingResolvers.push(resolve);
      });
      try { return JSON.parse(line); }
      catch (e) { throw new Error(`Failed to parse driver response: ${line}`); }
    }
  };
}

function startDriverProcess(muxRepo, pluginPath, mockLlmUrl) {
  const driverPath = path.resolve(__dirname, 'mux-driver.ts');
  const child = spawn(resolveBun(), ['run', driverPath], {
    cwd: muxRepo,
    env: {
      ...process.env,
      WANXIANGSHU_PLUGIN_PATH: pluginPath,
      WANXIANGSHU_MUX_REPO: muxRepo,
      MOCK_LLM_URL: mockLlmUrl,
    },
    stdio: ['pipe', 'pipe', 'pipe'],
    windowsHide: true,
  });
  child.stderr.on('data', (chunk) => {
    process.stderr.write(`[mux-driver-stderr] ${chunk}`);
  });
  return child;
}

class MuxHarness {
  constructor(sharedHost, sessionId) {
    this.port = 0;
    this.mockLLM = sharedHost.mockLLM;
    this.sessionId = sessionId;
    this.workDir = path.join(sharedHost.workdir, 'sandboxes', sessionId);
    this.home = sharedHost.home;
    this.queue = sharedHost.queue;
    this.currentReviewTaskRef = { value: null };
    this.nudgesList = [];
    this.helpers = {
      nudges: this.nudgesList,
      _setTodoList: (todos) => {
        this.queue.send({ type: 'setTodoList', todos, sessionId: this.sessionId }).catch(() => {});
      }
    };
    
    this.registration = {
      tools: sharedHost.toolNames.map((name) => ({ name })),
      eventHook: this.fireEvent.bind(this),
      messagesTransform: this.runMessageTransform.bind(this),
      slashCommands: sharedHost.commands,
    };
    this.reviewStoreSurface = {
      getReviewTask: () => this.currentReviewTaskRef.value
    };
    this.getToolSchemaCache = sharedHost.getToolSchemaCache;
  }

  getToolSchema(name) { return this.getToolSchemaCache.get(name) || null; }
  getToolRequired(name) {
    const s = this.getToolSchema(name);
    return (s && Array.isArray(s.required)) ? s.required : [];
  }

  async executeTool(name, args, config = {}) {
    const sess = config.sessionID || this.sessionId;
    const res = await this.queue.send({ type: 'executeTool', name, args, sessionId: sess });
    await this._syncNudges();
    if (!res.ok) throw new Error(res.error || `executeTool ${name} failed`);
    return typeof res.data === 'string' ? res.data : JSON.stringify(res.data);
  }

  async runSlashCommand(key, ...args) {
    const sessionId = args[0];
    const rest = args.slice(1);
    const res = await this.queue.send({ type: 'runCommand', name: key, args: rest.join(' '), sessionId });
    await this._syncNudges();
    if (!res.ok) throw new Error(res.error || `runSlashCommand ${key} failed`);
    return res.data;
  }

  async runMessageTransform(input, output) {
    const res = await this.queue.send({ type: 'transformMessages', messages: output.messages, workspaceId: input.workspaceId || this.sessionId });
    if (res.ok) output.messages = res.data;
    return output;
  }

  async runSystemTransform(input, output) {
    const res = await this.queue.send({ type: 'systemTransform', system: output.system });
    if (res.ok) output.system = res.data.system;
    return output;
  }

  async fireEvent(event) {
    const res = await this.queue.send({ type: 'emit', eventType: event.type, event, sessionId: event.workspaceId || this.sessionId });
    await this._syncNudges();
    return res.data;
  }

  async fireStreamEnd(wsId, textParts) {
    const res = await this.queue.send({
      type: 'emit',
      eventType: 'stream-end',
      sessionId: wsId || this.sessionId,
      event: {
        properties: {
          parts: (textParts || []).map((t) => ({ type: 'text', text: t })),
          metadata: {
            finishReason: 'stop',
            muxStopReason: 'stop'
          }
        }
      }
    });
    await this._syncNudges();
    return res;
  }

  async fireStreamAbort(wsId) {
    const res = await this.queue.send({ type: 'emit', eventType: 'stream-abort', sessionId: wsId || this.sessionId });
    await this._syncNudges();
    return res;
  }

  async readNdjson() {
    return (await this.queue.send({ type: 'readNdjson', sessionId: this.sessionId })).data.content;
  }

  async readFile(p) {
    return (await this.queue.send({ type: 'readFile', path: p, sessionId: this.sessionId })).data.content;
  }

  async fileExists(p) {
    return (await this.queue.send({ type: 'fileExists', path: p, sessionId: this.sessionId })).data.exists;
  }

  async waitForNdjson(min, maxMs) {
    const ms = Math.min(maxMs, 1000);
    return (await this.queue.send({ type: 'waitForNdjson', min, maxMs: ms, sessionId: this.sessionId })).data.ready;
  }

  async getChatHistoryCalled() {
    const res = await this.queue.send({ type: 'getChatHistoryCalled' });
    return res.ok ? res.data.called : false;
  }

  setMockReportMarkdown(markdown) {
    return this.queue.send({ type: 'setMockReportMarkdown', markdown });
  }

  getLastLlmRequest() {
    return this.mockLLM.calls.length > 0 ? this.mockLLM.calls[this.mockLLM.calls.length - 1] : null;
  }

  async _syncNudges() {
    const res = await this.queue.send({ type: 'getNudges' });
    if (res.ok && Array.isArray(res.data.nudges)) {
      this.nudgesList.length = 0;
      this.nudgesList.push(...res.data.nudges);
    }
    const taskRes = await this.queue.send({ type: 'getReviewTask', sessionId: this.sessionId });
    if (taskRes.ok) {
      this.currentReviewTaskRef.value = taskRes.data.task;
    }
  }

  async waitForCalls(count, timeoutMs = 1000) {
    const deadline = Date.now() + timeoutMs;
    while (this.mockLLM.calls.length < count) {
      if (Date.now() > deadline) {
        throw new Error(`timed out waiting for ${count} llm calls; saw ${this.mockLLM.calls.length}`);
      }
      await new Promise((r) => setTimeout(r, 20));
    }
    return this.mockLLM.calls.length;
  }

  async waitForFile(relPath, timeoutMs = 1000) {
    const deadline = Date.now() + timeoutMs;
    while (Date.now() < deadline) {
      if (await this.fileExists(relPath)) return true;
      await new Promise((r) => setTimeout(r, 50));
    }
    return false;
  }

  async dispose() {
    if (this.sessionId) {
      try {
        await this.queue.send({ type: 'cleanSandbox', sessionId: this.sessionId });
      } catch {}
    }
  }
}

export async function start(opts = {}) {
  const sharedHost = await hostSingletonManager.getHost('mux', async () => {
    const mockLlmInstance = createMockLLM();
    const mockLlm = await mockLlmInstance.start();
    const tempHome = fs.mkdtempSync(path.join(os.tmpdir(), 'mux-runner-home-'));
    buildConfig(mockLlm.url, tempHome);

    const muxRepo = process.env.WANXIANGSHU_MUX_REPO || path.resolve(__dirname, '..', '..', 'mux');
    const pluginPath = path.resolve(__dirname, '..', 'build', 'src', 'Hosts', 'Mux', 'Plugin.js');
    const child = startDriverProcess(muxRepo, pluginPath, mockLlm.url);
    const queue = buildCommandQueue(child);
    const workdir = await queue.workdirPromise;

    const handlerRes = await queue.send({ type: 'getCommands' });
    const commands = handlerRes.ok ? handlerRes.data.slashCommands : [];
    const getToolSchemaCache = new Map();

    const namesRes = await queue.send({ type: 'getToolNames' });
    const toolNames = namesRes.ok ? namesRes.data.toolNames : [];
    for (const name of toolNames) {
      const schemaRes = await queue.send({ type: 'getToolSchema', name });
      if (schemaRes.ok) {
        const parameters = schemaRes.data.parameters;
        getToolSchemaCache.set(name, parameters?.jsonSchema ?? parameters);
      }
    }

    return {
      child,
      mockLLM: mockLlm,
      queue,
      workdir,
      home: tempHome,
      commands,
      getToolSchemaCache,
      toolNames
    };
  });

  const sessionId = opts.workspaceId || 'mux-e2e-session';
  return new MuxHarness(sharedHost, sessionId);
}

export default { start };
