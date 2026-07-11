import { execSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createMockLLM } from './mock-llm.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

let WANXIANG_ROOT = path.resolve(__dirname, '..');
function getPluginPath(variant) {
  let file = 'Plugin.js';
  if (variant === 'mimocode') file = 'PluginMimo.js';
  if (variant === 'mimotui') file = 'PluginMimoTui.js';
  
  let p = path.resolve(WANXIANG_ROOT, `build/src/Opencode/${file}`);
  if (!fs.existsSync(p)) {
    let altRoot = path.resolve(__dirname, '../..');
    p = path.resolve(altRoot, `build/src/Opencode/${file}`);
  }
  return p;
}

function gitInit(dir, agentsContent = '- e2e test workspace\n') {
  execSync('git init', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.email test@example.com', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.name test', { cwd: dir, stdio: 'ignore' });
  fs.writeFileSync(path.join(dir, 'README.md'), '# test\n');
  fs.writeFileSync(path.join(dir, 'AGENTS.md'), agentsContent);
  execSync('git add README.md AGENTS.md', { cwd: dir, stdio: 'ignore' });
  execSync('git commit -m init', { cwd: dir, stdio: 'ignore' });
}

async function loadPlugin(variant) {
  const pluginUrl = pathToFileURL(getPluginPath(variant)).href;
  return import(pluginUrl);
}

function buildMockClient(messages = [], opts = {}) {
  const messagesFn = async () => ({ data: messages });
  return {
    session: {
      messages: messagesFn,
      create: async () => ({ data: { id: 'mock-child-session' } }),
      prompt: async () => undefined,
      abort: async () => ({}),
      ...(opts.mockSessionClient || {}),
    },
    ...(opts.mockClientExtra || {}),
  };
}

class OpencodePluginHarness {
  constructor(workDir, home, sessionId, plugin, mockLLM) {
    this.workDir = workDir;
    this.home = home;
    this.sessionId = sessionId;
    this.plugin = plugin;
    this.mockLLM = mockLLM;
    this.port = 0;
  }

  getPlugin() { return this.plugin; }
  getToolNames() { return this.plugin.tool ? Object.keys(this.plugin.tool) : []; }
  getToolEntry(name) { return this.plugin.tool ? (this.plugin.tool[name] || null) : null; }

  async runToolDefinition(toolID) {
    const hook = this.plugin['tool.definition'];
    if (!hook) throw new Error('plugin has no tool.definition hook');
    const output = {};
    const entry = this.plugin.tool?.[toolID];
    if (entry?.args) output.args = entry.args;
    await hook({ toolID }, output);
    return output;
  }

  getSandboxDir(sessId) {
    const finalSessId = sessId || this.sessionId;
    const sandbox = path.join(this.workDir, 'sandboxes', finalSessId);
    if (!fs.existsSync(sandbox)) {
      fs.mkdirSync(sandbox, { recursive: true });
      gitInit(sandbox);
    }
    return sandbox;
  }

  async createSession(body = {}, query = {}) {
    const id = 'opencode-e2e-session-' + Math.random().toString(36).slice(2, 8);
    this.sessionId = id;
    return { ok: true, data: { data: { id } } };
  }

  async executePluginTool(name, args = {}, extraCtx = {}) {
    const tool = this.plugin.tool;
    if (!tool || !tool[name]) throw new Error(`tool ${name} not found`);
    const entry = tool[name];
    if (!entry.execute) throw new Error(`tool ${name} has no execute function`);
    const sess = extraCtx.sessionID || this.sessionId;
    const ctx = {
      directory: this.getSandboxDir(sess),
      sessionID: sess,
      agent: 'build',
      ...extraCtx,
    };
    const raw = await entry.execute(args, ctx);
    return typeof raw === 'string' ? raw : JSON.stringify(raw);
  }

  async runToolWithHooks(name, args = {}, extraCtx = {}) {
    const before = this.plugin['tool.execute.before'];
    const after = this.plugin['tool.execute.after'];
    const outputHolder = { args: { ...args } };
    const sess = extraCtx.sessionID || this.sessionId;
    if (before) {
      await before({ tool: name, args: outputHolder.args, sessionID: sess }, outputHolder);
      if (outputHolder.error) return JSON.stringify({ error: outputHolder.error });
    }
    const raw = await this.executePluginTool(name, outputHolder.args, extraCtx);
    if (after) {
      const finalOutput = { output: typeof raw === 'string' ? raw : JSON.stringify(raw) };
      await after({ tool: name, sessionID: sess, args: outputHolder.args }, finalOutput);
      if (finalOutput.error) return JSON.stringify({ error: finalOutput.error, output: finalOutput.output });
      return finalOutput.output;
    }
    return typeof raw === 'string' ? raw : JSON.stringify(raw);
  }

  async runToolExecuteHooks(name, args = {}, rawOutput = "") {
    const before = this.plugin['tool.execute.before'];
    const after = this.plugin['tool.execute.after'];
    const outputHolder = { args: { ...args } };
    if (before) {
      await before({ tool: name, args: outputHolder.args, sessionID: this.sessionId }, outputHolder);
      if (outputHolder.error) return { error: outputHolder.error };
    }
    if (after) {
      const finalOutput = { output: rawOutput };
      await after({ tool: name, sessionID: this.sessionId, args: outputHolder.args }, finalOutput);
      return { output: finalOutput.output, error: finalOutput.error, args: outputHolder.args };
    }
    return { output: rawOutput };
  }

  async runCommandExecuteBefore(command, args = '') {
    const hook = this.plugin['command.execute.before'];
    if (!hook) throw new Error('plugin has no command.execute.before hook');
    const input = { command, arguments: args, sessionID: this.sessionId };
    const output = { parts: [] };
    await hook(input, output);
    return output;
  }

  async runMessageTransform(input, messages) {
    const hook = this.plugin['experimental.chat.messages.transform'];
    if (!hook) throw new Error('plugin has no experimental.chat.messages.transform hook');
    const output = { messages: messages || [] };
    await hook(input, output);
    return output;
  }

  async runSystemTransform(input) {
    const hook = this.plugin['experimental.chat.system.transform'];
    if (!hook) throw new Error('plugin has no experimental.chat.system.transform hook');
    const output = {};
    await hook(input, output);
    return output;
  }

  async runLifecycleHook(name, input, output = {}) {
    const hook = this.plugin[name];
    if (!hook) throw new Error(`plugin has no ${name} hook`);
    await hook(input, output);
    return output;
  }

  async runConfigHook(cfg) {
    const hook = this.plugin['config'];
    if (!hook) throw new Error('plugin has no config hook');
    return await hook(cfg);
  }

  async runSessionPost(input) {
    const hook = this.plugin['session.post'];
    if (!hook) throw new Error('plugin has no session.post hook');
    const output = {};
    await hook(input, output);
    return output;
  }

  async runSessionUserQueryPost(input) {
    const hook = this.plugin['session.userQuery.post'];
    if (!hook) throw new Error('plugin has no session.userQuery.post hook');
    const output = {};
    await hook(input, output);
    return output;
  }

  async runTui(apiObj) {
    const tui = this.plugin['tui'];
    if (!tui) throw new Error('plugin has no tui export');
    return await tui(apiObj);
  }

  async fireEvent(event) {
    const hook = this.plugin['event'];
    if (!hook) throw new Error('plugin has no event hook');
    await hook(event);
    await new Promise((r) => setTimeout(r, 0));
    return event;
  }

  fireStreamAbort(sessId = this.sessionId) {
    return this.fireEvent({
      event: {
        type: 'stream-abort',
        properties: { sessionID: sessId },
      },
    });
  }

  getReviewStore() { return this.plugin.__reviewStore || null; }
  getFallbackRuntime() { return this.plugin.__fallbackRuntime || null; }

  readPartsText(output) {
    const parts = output?.parts;
    if (!Array.isArray(parts)) return '';
    return parts
      .filter((p) => p && p.type === 'text')
      .map((p) => p.text || '')
      .join('\n');
  }

  getLastLlmRequest() {
    if (!this.mockLLM.calls || this.mockLLM.calls.length === 0) return null;
    return this.mockLLM.calls[this.mockLLM.calls.length - 1];
  }

  getLlmCalls() {
    return this.mockLLM.calls || [];
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

  expectTool(t, a) { this.mockLLM.expectTool(t, a); }
  expectText(t) { this.mockLLM.expectText(t); }
  resetMock() { this.mockLLM.reset(); }

  readFile(relPath) {
    return fs.readFileSync(path.join(this.getSandboxDir(this.sessionId), relPath), 'utf8');
  }

  fileExists(relPath) {
    return fs.existsSync(path.join(this.getSandboxDir(this.sessionId), relPath));
  }

  async waitForFile(relPath, timeoutMs = 1000) {
    const deadline = Date.now() + timeoutMs;
    const absPath = path.join(this.getSandboxDir(this.sessionId), relPath);
    while (Date.now() < deadline) {
      if (fs.existsSync(absPath)) return true;
      await new Promise((r) => setTimeout(r, 50));
    }
    return false;
  }

  async dispose() {
    await this.mockLLM.stop().catch(() => {});
    try {
      const lockPath = path.join(this.workDir, '.wanxiangshu.ndjson.lock');
      if (fs.existsSync(lockPath)) fs.rmSync(lockPath);
    } catch {}
    try { fs.rmSync(this.home, { recursive: true, force: true }); } catch {}
  }
}

export async function start(opts = {}) {
  const llm = createMockLLM();
  const llmHandle = await llm.start();

  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'opencode-e2e-'));
  const workDir = path.join(home, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });
  gitInit(workDir, opts.agentsContent);

  const messages = opts.messages || [];
  const client = buildMockClient(messages, opts);

  const plugin = await loadPlugin(opts.variant);
  const result = await plugin.default({
    directory: workDir,
    client,
    workdir: workDir,
    ...(opts.pluginCtxExtra || {}),
  });

  const sessionId = opts.sessionId || 'opencode-e2e-session';
  return new OpencodePluginHarness(workDir, home, sessionId, result, llmHandle);
}