import { execSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createMockLLM } from './mock-llm.js';

const originalFetch = global.fetch;
global.fetch = async (url, options) => {
  if (typeof url === 'string' && url.startsWith('https://ollama.com/api')) {
    const json = () => url.includes('web_search') ? ({ results: [{ title: 'Test Search Title', url: 'http://example.com', content: 'Test search content for E2E.' }] }) : ({ title: 'Example Domain', byline: 'IANA', length: 500, content: 'Example Domain\n\nThis domain is for use in documentation examples.' });
    return { ok: true, status: 200, json: async () => json() };
  }
  return typeof originalFetch === 'function' ? originalFetch(url, options) : Promise.reject(new Error(`fetch not stubbed: ${url}`));
};

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// In build/ the plugin lives at ../build/src/Mux/Plugin.js; in source layout the
// harness runs against build/ so the relative path resolves there.
let WANXIANG_ROOT = path.resolve(__dirname, '..');
let PLUGIN_JS = path.resolve(WANXIANG_ROOT, 'build/src/Mux/Plugin.js');
if (!fs.existsSync(PLUGIN_JS)) {
  WANXIANG_ROOT = path.resolve(__dirname, '../..');
  PLUGIN_JS = path.resolve(WANXIANG_ROOT, 'build/src/Mux/Plugin.js');
}
const PLUGIN_URL = pathToFileURL(PLUGIN_JS).href;

function gitInit(dir) {
  execSync('git init', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.email test@example.com', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.name test', { cwd: dir, stdio: 'ignore' });
  fs.writeFileSync(path.join(dir, 'README.md'), '# test\n');
  fs.writeFileSync(path.join(dir, 'AGENTS.md'), '- e2e test workspace\n');
  execSync('git add README.md AGENTS.md', { cwd: dir, stdio: 'ignore' });
  execSync('git commit -m init', { cwd: dir, stdio: 'ignore' });
}

async function loadPlugin() {
  return import(PLUGIN_URL);
}

/**
 * Create mock event helpers (second argument to eventHook).
 *
 * In production these call back into Mux to nudge or read todos. In the
 * harness they record calls for assertion.
 */
function buildMockHelpers() {
  const nudges = [];
  let todoList = [];
  return {
    nudges,
    _setTodoList: (list) => { todoList = list; },
    _helpersObj: {
      getTodos: () => Promise.resolve(todoList),
      nudge: (_ws, msg) => {
        nudges.push(msg);
        return Promise.resolve(true);
      },
    },
  };
}

function getRegProp(reg, key) {
  return reg[key];
}

function findTool(reg, name) {
  const tools = reg.tools;
  if (!Array.isArray(tools)) return null;
  return tools.find((t) => t && t.name === name) || null;
}

export async function start(opts = {}) {
  const llm = createMockLLM();
  const llmHandle = await llm.start();

  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'mux-e2e-'));
  const workDir = path.join(home, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });
  gitInit(workDir);

  let getChatHistoryCalled = false;

  const mockTaskService = {
    create: async (input) => {
      return { success: true, data: { taskId: 'task-123' } };
    },
    waitForAgentReport: async (taskId, opts) => {
      return { reportMarkdown: mockTaskService._reportMarkdown };
    },
    _reportMarkdown: 'Accepted: Pre-review passed.',
  };

  const plugin = await loadPlugin();
  const deps = {
    loadConfigOrDefault: () => ({}),
    findWorkspaceEntry: () => ({ workspace: null }),
    resolveAgentFrontmatter: () => Promise.resolve({}),
    getChatHistory: (workspaceId) => {
      getChatHistoryCalled = true;
      const messages = opts.chatHistory || [];
      return Promise.resolve(workspaceId === (opts.workspaceId || 'mux-e2e-session') ? messages : []);
    },
    directory: workDir,
    taskService: mockTaskService,
  };
  const reg = plugin.createRegistration(deps);

  const helpers = buildMockHelpers();
  const slashCommands = regSlashCommands(reg);

  const api = {
    port: 0,
    mockLLM: llmHandle,
    workDir,
    home,
    registration: reg,
    helpers,
    getChatHistoryCalled: () => getChatHistoryCalled,
    setMockReportMarkdown: (markdown) => { mockTaskService._reportMarkdown = markdown; },

    // Event hook -----------------------------------------------------------
    async fireEvent(event) {
      const hook = getRegProp(reg, 'eventHook');
      if (!hook) throw new Error('registration has no eventHook');
      await hook(event, helpers._helpersObj);
      // Allow microtask chain used by NudgeRuntime to settle.
      await new Promise((r) => setTimeout(r, 0));
      return event;
    },

    fireStreamEnd(workspaceId, textParts = ['done']) {
      return api.fireEvent({
        type: 'stream-end',
        workspaceId,
        properties: { parts: textParts.map((t) => ({ type: 'text', text: t })) },
      });
    },

    fireStreamAbort(workspaceId) {
      return api.fireEvent({ type: 'stream-abort', workspaceId });
    },

    // Message transform ----------------------------------------------------
    async runMessageTransform(input, output) {
      const transform = getRegProp(reg, 'messagesTransform');
      if (!transform) throw new Error('registration has no messagesTransform');
      await transform(input, output);
      return output;
    },

    async runSystemTransform(input, output) {
      const transform = getRegProp(reg, 'systemTransform');
      if (!transform) throw new Error('registration has no systemTransform');
      await transform(input, output);
      return output;
    },

    // Tool schema ----------------------------------------------------------
    getToolDefinition(name) {
      return findTool(reg, name);
    },

    getToolSchema(name) {
      const t = findTool(reg, name);
      return t ? t.parameters : null;
    },

    getToolRequired(name) {
      const s = api.getToolSchema(name);
      if (!s || !Array.isArray(s.required)) return [];
      return s.required;
    },

    // Execute tool ---------------------------------------------------------
    async executeTool(name, args, config = {}) {
      const toolDef = findTool(reg, name);
      if (!toolDef) throw new Error(`tool ${name} not found in registration`);
      if (!toolDef.execute) throw new Error(`tool ${name} has no execute function`);
      const toolConfig = {
        directory: workDir,
        sessionID: opts.workspaceId || 'mux-e2e-session',
        workspaceId: opts.workspaceId || 'mux-e2e-session',
        taskService: mockTaskService,
        ...config,
      };
      // If tool.execute.before exists, run it first.
      const before = getRegProp(reg, 'tool.execute.before');
      const outputHolder = { args: { ...args } };
      if (before) {
        await before({ tool: name, args: outputHolder.args, sessionID: toolConfig.sessionID }, outputHolder);
        if (outputHolder.error) return JSON.stringify({ error: outputHolder.error });
      }
      const raw = await toolDef.execute(toolConfig, outputHolder.args);
      // If tool.execute.after exists, run it.
      const after = getRegProp(reg, 'tool.execute.after');
      const finalOutput = { output: typeof raw === 'string' ? raw : JSON.stringify(raw) };
      if (after) {
        await after(
          { tool: name, sessionID: toolConfig.sessionID, args: outputHolder.args },
          finalOutput,
        );
        if (finalOutput.error) return JSON.stringify({ error: finalOutput.error, output: finalOutput.output });
      }
      return finalOutput.output;
    },

    // Slash commands -------------------------------------------------------
    getSlashCommands() {
      return slashCommands;
    },

    async runSlashCommand(key, ...args) {
      const cmd = slashCommands.find((c) => c.key === key);
      if (!cmd) throw new Error(`slash command ${key} not found`);
      return cmd.execute(...args);
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

    // File helpers ---------------------------------------------------------
    readFile(relPath) {
      return fs.readFileSync(path.join(workDir, relPath), 'utf8');
    },

    fileExists(relPath) {
      return fs.existsSync(path.join(workDir, relPath));
    },

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

function regSlashCommands(reg) {
  const src = reg.slashCommands;
  if (!Array.isArray(src)) return [];
  return src.map((c) => ({ key: c.key, description: c.description, execute: c.execute }));
}
