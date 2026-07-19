import { execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import * as utils from './opencode-plugin-harness-utils.js';

function gitInit(dir, agentsContent = '- e2e test workspace\n') {
  execSync('git init', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.email test@example.com', { cwd: dir, stdio: 'ignore' });
  execSync('git config user.name test', { cwd: dir, stdio: 'ignore' });
  fs.writeFileSync(path.join(dir, 'README.md'), '# test\n');
  fs.writeFileSync(path.join(dir, 'AGENTS.md'), agentsContent);
  execSync('git add README.md AGENTS.md', { cwd: dir, stdio: 'ignore' });
  execSync('git commit -m init', { cwd: dir, stdio: 'ignore' });
}

export class OpencodePluginHarness {
  constructor(workDir, home, sessionId, plugin, mockLLM, seams = null) {
    this.workDir = workDir;
    this.home = home;
    this.sessionId = sessionId;
    this.plugin = plugin;
    this.mockLLM = mockLLM;
    this.port = 0;
    this.seams = seams;
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
    const ctx = { directory: this.workDir, sessionID: sess, agent: 'build', ...extraCtx };
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
      event: { type: 'stream-abort', properties: { sessionID: sessId } },
    });
  }

  getReviewStore() {
    if (this.seams && this.seams.ReviewStore) return this.seams.ReviewStore;
    return this.plugin.__reviewStore || null;
  }

  getFallbackRuntime() {
    if (this.seams && this.seams.FallbackRuntime) return this.seams.FallbackRuntime;
    return this.plugin.__fallbackRuntime || null;
  }

  readPartsText(output) { return utils.readPartsText(output); }
  getLastLlmRequest() { return utils.getLastLlmRequest(this.mockLLM); }
  getLlmCalls() { return utils.getLlmCalls(this.mockLLM); }
  async waitForCalls(count, timeoutMs) { return utils.waitForCalls(this.mockLLM, count, timeoutMs); }
  expectTool(t, a) { utils.expectTool(this.mockLLM, t, a); }
  expectText(t) { utils.expectText(this.mockLLM, t); }
  resetMock() { utils.resetMock(this.mockLLM); }
  readFile(relPath) { return utils.readFile(this.workDir, relPath); }
  fileExists(relPath) { return utils.fileExists(this.workDir, relPath); }
  async waitForFile(relPath, timeoutMs) { return utils.waitForFile(this.getSandboxDir.bind(this), this.sessionId, relPath, timeoutMs); }
  async dispose() { return utils.disposeHarness(this.mockLLM, this.workDir, this.home); }
}
