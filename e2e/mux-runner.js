import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import readline from 'node:readline';
import { spawn, spawnSync } from 'node:child_process';
import { createMockLLM } from './mock-llm.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const MUX_E2E_LOCK = '/tmp/wanxiang-mux-e2e.lock';

function releaseLock() {
  try { fs.unlinkSync(MUX_E2E_LOCK); } catch {}
}

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

async function initMockLLMAndHome() {
  const mockLlmInstance = createMockLLM();
  const mockLlm = await mockLlmInstance.start();
  const tempHome = fs.mkdtempSync(path.join(os.tmpdir(), 'mux-runner-home-'));
  buildConfig(mockLlm.url, tempHome);
  return { mockLlmInstance, mockLlm, tempHome };
}

async function executeToolImpl(queue, api, name, args, config) {
  const res = await queue.send({ type: 'executeTool', name, args, sessionId: config.sessionID });
  await api._syncNudges();
  if (!res.ok) throw new Error(res.error || `executeTool ${name} failed`);
  return typeof res.data === 'string' ? res.data : JSON.stringify(res.data);
}

async function runSlashCommandImpl(queue, api, key, args) {
  const res = await queue.send({ type: 'runCommand', name: key, args: args.join(' ') });
  await api._syncNudges();
  if (!res.ok) throw new Error(res.error || `runSlashCommand ${key} failed`);
  return res.data;
}

async function getChatHistoryCalledImpl(queue) {
  const res = await queue.send({ type: 'getChatHistoryCalled' });
  return res.ok ? res.data.called : false;
}

async function setMockReportMarkdownImpl(queue, markdown) {
  await queue.send({ type: 'setMockReportMarkdown', markdown });
}

async function disposeImpl(cleanupAll, queue, child) {
  cleanupAll();
  try { queue.rl.close(); } catch {}
  try { await queue.send({ type: 'dispose' }); } catch {}
  try { child.kill('SIGKILL'); } catch {}
}

async function runMsgTransImpl(queue, input, output) {
  const res = await queue.send({ type: 'transformMessages', messages: output.messages, workspaceId: input.workspaceId });
  if (res.ok) output.messages = res.data;
  return output;
}

async function runSysTransImpl(queue, input, output) {
  const res = await queue.send({ type: 'systemTransform', system: output.system });
  if (res.ok) output.system = res.data.system;
  return output;
}

async function fireEventImpl(queue, api, event) {
  const res = await queue.send({ type: 'emit', eventType: event.type, event, sessionId: event.workspaceId });
  await api._syncNudges();
  return res.data;
}

async function fireStreamEndImpl(queue, api, workspaceId, textParts) {
  const res = await queue.send({
    type: 'emit',
    eventType: 'stream-end',
    sessionId: workspaceId,
    event: { parts: (textParts || []).map((t) => ({ type: 'text', text: t })) }
  });
  await api._syncNudges();
  return res;
}

async function fireStreamAbortImpl(queue, api, workspaceId) {
  const res = await queue.send({ type: 'emit', eventType: 'stream-abort', sessionId: workspaceId });
  await api._syncNudges();
  return res;
}

async function readNdjsonImpl(queue) { return (await queue.send({ type: 'readNdjson' })).data.content; }
async function readFileImpl(queue, p) { return (await queue.send({ type: 'readFile', path: p })).data.content; }
async function fileExistsImpl(queue, p) { return (await queue.send({ type: 'fileExists', path: p })).data.exists; }
async function waitForNdjsonImpl(queue, min, maxMs) { return (await queue.send({ type: 'waitForNdjson', min, maxMs })).data.ready; }
async function syncNudgesImpl(queue, nudgesList, currentReviewTaskRef) {
  const res = await queue.send({ type: 'getNudges' });
  if (res.ok && Array.isArray(res.data.nudges)) {
    nudgesList.length = 0;
    nudgesList.push(...res.data.nudges);
  }
  const taskRes = await queue.send({ type: 'getReviewTask', sessionId: 'mux-e2e-session' });
  if (taskRes.ok) {
    currentReviewTaskRef.value = taskRes.data.task;
  }
}

function buildRunnerApi(child, queue, mockLlm, tempHome, cleanupAll, commands, getToolSchemaCache, nudgesList, currentReviewTaskRef) {
  const api = {
    port: 0,
    mockLLM: mockLlm,
    workDir: tempHome,
    home: tempHome,
    sessionId: 'mux-e2e-session',
    helpers: { nudges: nudgesList, _setTodoList: () => {} },
    registration: {
      tools: [],
      eventHook: () => {},
      messagesTransform: () => {},
      slashCommands: commands,
      __reviewStore: {
        getReviewTask: () => currentReviewTaskRef.value
      }
    },
    getToolSchema(name) { return getToolSchemaCache.get(name) || null; },
    getToolRequired(name) {
      const s = api.getToolSchema(name);
      return (s && Array.isArray(s.required)) ? s.required : [];
    },
    async executeTool(name, args, config = {}) { return executeToolImpl(queue, api, name, args, config); },
    async runSlashCommand(key, ...args) { return runSlashCommandImpl(queue, api, key, args); },
    async runMessageTransform(input, output) { return runMsgTransImpl(queue, input, output); },
    async runSystemTransform(input, output) { return runSysTransImpl(queue, input, output); },
    async fireEvent(event) { return fireEventImpl(queue, api, event); },
    async fireStreamEnd(wsId, textParts) { return fireStreamEndImpl(queue, api, wsId, textParts); },
    async fireStreamAbort(wsId) { return fireStreamAbortImpl(queue, api, wsId); },
    async readNdjson() { return readNdjsonImpl(queue); },
    async readFile(p) { return readFileImpl(queue, p); },
    async fileExists(p) { return fileExistsImpl(queue, p); },
    async waitForNdjson(min, maxMs) { return waitForNdjsonImpl(queue, min, maxMs); },
    getChatHistoryCalled() { return getChatHistoryCalledImpl(queue); },
    setMockReportMarkdown(markdown) { return setMockReportMarkdownImpl(queue, markdown); },
    getLastLlmRequest() { return mockLlm.calls.length > 0 ? mockLlm.calls[mockLlm.calls.length - 1] : null; },
    async _syncNudges() { await syncNudgesImpl(queue, nudgesList, currentReviewTaskRef); },
    async dispose() { await disposeImpl(cleanupAll, queue, child); },
  };
  return api;
}

async function populateRunnerApi(api, queue, getToolSchemaCache) {
  const namesRes = await queue.send({ type: 'getToolNames' });
  if (namesRes.ok && Array.isArray(namesRes.data.toolNames)) {
    api.registration.tools = namesRes.data.toolNames.map((name) => ({ name }));
    for (const name of namesRes.data.toolNames) {
      const schemaRes = await queue.send({ type: 'getToolSchema', name });
      if (schemaRes.ok) getToolSchemaCache.set(name, schemaRes.data.parameters);
    }
  }
  api.registration.eventHook = api.fireEvent;
  api.registration.messagesTransform = api.runMessageTransform;
}

export async function start(opts = {}) {
  try { fs.openSync(MUX_E2E_LOCK, 'wx'); }
  catch (e) {
    if (e.code === 'EEXIST') throw new Error('mux e2e already running (lock exists at ' + MUX_E2E_LOCK + ')');
    throw e;
  }
  const { mockLlmInstance, mockLlm, tempHome } = await initMockLLMAndHome();
  const cleanupAll = () => {
    releaseLock();
    mockLlm.stop().catch(() => {});
    try { fs.rmSync(tempHome, { recursive: true, force: true }); } catch {}
  };
  process.once('exit', cleanupAll);
  const muxRepo = process.env.WANXIANGSHU_MUX_REPO || path.resolve(__dirname, '..', '..', 'mux');
  const pluginPath = path.resolve(__dirname, '..', 'build', 'src', 'Mux', 'Plugin.js');
  const child = startDriverProcess(muxRepo, pluginPath, mockLlm.url);
  const queue = buildCommandQueue(child);
  const workdir = await queue.workdirPromise;
  const handlerRes = await queue.send({ type: 'getCommands' });
  const commands = handlerRes.ok ? handlerRes.data.slashCommands : [];
  const nudgesList = [];
  const getToolSchemaCache = new Map();
  const currentReviewTaskRef = { value: null };
  const api = buildRunnerApi(child, queue, mockLlm, tempHome, cleanupAll, commands, getToolSchemaCache, nudgesList, currentReviewTaskRef);
  await populateRunnerApi(api, queue, getToolSchemaCache);
  api.workDir = workdir || api.home;
  return api;
}

export default { start };
