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

function gitInit(dir) {
  execSync('git init', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.email test@example.com', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.name test', { cwd: dir, stdio: 'ignore' });
  fs.writeFileSync(path.join(dir, 'README.md'), '# test\n');
  fs.writeFileSync(path.join(dir, 'AGENTS.md'), '- e2e test workspace\n');
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

export async function start(opts = {}) {
  const llm = createMockLLM();
  const llmHandle = await llm.start();

  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'opencode-e2e-'));
  const workDir = path.join(home, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });
  gitInit(workDir);

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

  const api = {
    port: 0,
    mockLLM: llmHandle,
    workDir,
    home,
    sessionId,
    plugin: result,

    getPlugin() { return result; },
    getToolNames() { return result.tool ? Object.keys(result.tool) : []; },
    getToolEntry(name) { return result.tool ? (result.tool[name] || null) : null; },

    async runToolDefinition(toolID) {
      const hook = result['tool.definition'];
      if (!hook) throw new Error('plugin has no tool.definition hook');
      const output = {};
      const entry = result.tool?.[toolID];
      if (entry?.args) output.args = entry.args;
      await hook({ toolID }, output);
      return output;
    },

    async executePluginTool(name, args = {}, extraCtx = {}) {
      const tool = result.tool;
      if (!tool || !tool[name]) throw new Error(`tool ${name} not found`);
      const entry = tool[name];
      if (!entry.execute) throw new Error(`tool ${name} has no execute function`);
      const ctx = {
        directory: workDir,
        sessionID: sessionId,
        agent: 'build',
        ...extraCtx,
      };
      const raw = await entry.execute(args, ctx);
      return typeof raw === 'string' ? raw : JSON.stringify(raw);
    },

    async runToolWithHooks(name, args = {}, extraCtx = {}) {
      const before = result['tool.execute.before'];
      const after = result['tool.execute.after'];
      const outputHolder = { args: { ...args } };
      if (before) {
        await before({ tool: name, args: outputHolder.args, sessionID: sessionId }, outputHolder);
        if (outputHolder.error) return JSON.stringify({ error: outputHolder.error });
      }
      const raw = await api.executePluginTool(name, outputHolder.args, extraCtx);
      if (after) {
        const finalOutput = { output: typeof raw === 'string' ? raw : JSON.stringify(raw) };
        await after({ tool: name, sessionID: sessionId, args: outputHolder.args }, finalOutput);
        if (finalOutput.error) return JSON.stringify({ error: finalOutput.error, output: finalOutput.output });
        return finalOutput.output;
      }
      return typeof raw === 'string' ? raw : JSON.stringify(raw);
    },

    /** Bypasses tool execution body; runs before/after hooks directly for testing hook validation and output rewriting. */
    async runToolExecuteHooks(name, args = {}, rawOutput = "") {
      const before = result['tool.execute.before'];
      const after = result['tool.execute.after'];
      const outputHolder = { args: { ...args } };
      if (before) {
        await before({ tool: name, args: outputHolder.args, sessionID: sessionId }, outputHolder);
        if (outputHolder.error) return { error: outputHolder.error };
      }
      if (after) {
        const finalOutput = { output: rawOutput };
        await after({ tool: name, sessionID: sessionId, args: outputHolder.args }, finalOutput);
        return { output: finalOutput.output, error: finalOutput.error, args: outputHolder.args };
      }
      return { output: rawOutput };
    },

    async runCommandExecuteBefore(command, args = '') {
      const hook = result['command.execute.before'];
      if (!hook) throw new Error('plugin has no command.execute.before hook');
      const input = { command, arguments: args, sessionID: sessionId };
      const output = { parts: [] };
      await hook(input, output);
      return output;
    },

    async runMessageTransform(input, messages) {
      const hook = result['experimental.chat.messages.transform'];
      if (!hook) throw new Error('plugin has no experimental.chat.messages.transform hook');
      const output = { messages: messages || [] };
      await hook(input, output);
      return output;
    },

    async runSystemTransform(input) {
      const hook = result['experimental.chat.system.transform'];
      if (!hook) throw new Error('plugin has no experimental.chat.system.transform hook');
      const output = {};
      await hook(input, output);
      return output;
    },

    async runLifecycleHook(name, input, output = {}) {
      const hook = result[name];
      if (!hook) throw new Error(`plugin has no ${name} hook`);
      await hook(input, output);
      return output;
    },

    async runConfigHook(cfg) {
      const hook = result['config'];
      if (!hook) throw new Error('plugin has no config hook');
      return await hook(cfg);
    },

    async runSessionPost(input) {
      const hook = result['session.post'];
      if (!hook) throw new Error('plugin has no session.post hook');
      const output = {};
      await hook(input, output);
      return output;
    },

    async runSessionUserQueryPost(input) {
      const hook = result['session.userQuery.post'];
      if (!hook) throw new Error('plugin has no session.userQuery.post hook');
      const output = {};
      await hook(input, output);
      return output;
    },

    async runTui(apiObj) {
      const tui = result['tui'];
      if (!tui) throw new Error('plugin has no tui export');
      return await tui(apiObj);
    },

    async fireEvent(event) {
      const hook = result['event'];
      if (!hook) throw new Error('plugin has no event hook');
      // Event shape: { event: { type, properties: { sessionID } } }
      await hook(event);
      // Allow microtask chain to settle (NudgeRuntime, etc.)
      await new Promise((r) => setTimeout(r, 0));
      return event;
    },

    fireStreamAbort(sessId = sessionId) {
      return api.fireEvent({
        event: {
          type: 'stream-abort',
          properties: { sessionID: sessId },
        },
      });
    },

    getReviewStore() { return result.__reviewStore || null; },

    // Parts text extraction from output.parts ------------------------------
    readPartsText(output) {
      const parts = output?.parts;
      if (!Array.isArray(parts)) return '';
      return parts
        .filter((p) => p && p.type === 'text')
        .map((p) => p.text || '')
        .join('\n');
    },

    // LLM introspection ----------------------------------------------------
    getLastLlmRequest() {
      if (!llmHandle.calls || llmHandle.calls.length === 0) return null;
      return llmHandle.calls[llmHandle.calls.length - 1];
    },

    getLlmCalls() {
      return llmHandle.calls || [];
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

    expectTool: llmHandle.expectTool,
    expectText: llmHandle.expectText,
    resetMock: llmHandle.reset,

    readFile(relPath) { return fs.readFileSync(path.join(workDir, relPath), 'utf8'); },
    fileExists(relPath) { return fs.existsSync(path.join(workDir, relPath)); },

    async waitForFile(relPath, timeoutMs = 10000) {
      const deadline = Date.now() + timeoutMs;
      const absPath = path.join(workDir, relPath);
      while (Date.now() < deadline) {
        if (fs.existsSync(absPath)) return true;
        await new Promise((r) => setTimeout(r, 500));
      }
      return false;
    },

    // Disposal -------------------------------------------------------------
    async dispose() {
      await llmHandle.stop().catch(() => {});
      try {
        const lockPath = path.join(workDir, '.wanxiangshu.ndjson.lock');
        if (fs.existsSync(lockPath)) fs.rmSync(lockPath);
      } catch {}
      try { fs.rmSync(home, { recursive: true, force: true }); } catch {}
    },
  };

  return api;
}
