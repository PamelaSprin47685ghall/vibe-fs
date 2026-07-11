import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import readline from 'node:readline';
import { execSync } from 'node:child_process';

const originalFetch = global.fetch;
global.fetch = async (url, options) => {
  if (typeof url === 'string' && url.startsWith('https://ollama.com/api')) {
    const json = () => url.includes('web_search') ? ({ results: [{ title: 'Test Search Title', url: 'http://example.com', content: 'Test search content for E2E.' }] }) : ({ title: 'Example Domain', byline: 'IANA', length: 500, content: 'Example Domain\n\nThis domain is for use in documentation examples.' });
    return { ok: true, status: 200, json: async () => json() };
  }
  return typeof originalFetch === 'function' ? originalFetch(url, options) : Promise.reject(new Error(`fetch not stubbed: ${url}`));
};

function respond(ok: boolean, data?: unknown, error?: string) {
  const res: Record<string, unknown> = { ok };
  if (data !== undefined) res.data = data;
  if (error !== undefined) res.error = error;
  process.stdout.write(JSON.stringify(res) + '\n');
}

const commandQueue: Array<Record<string, any> | null> = [];
const waitingResolvers: Array<(cmd: Record<string, any> | null) => void> = [];
let rlStarted = false;

function dispatch(cmd: Record<string, any> | null) {
  if (waitingResolvers.length > 0) waitingResolvers.shift()!(cmd);
  else commandQueue.push(cmd);
}

function readStdinJson(): Promise<Record<string, any> | null> {
  if (!rlStarted) {
    rlStarted = true;
    const rl = readline.createInterface({ input: process.stdin, terminal: false });
    rl.on('line', (line) => {
      const trimmed = line.trim();
      if (!trimmed) { dispatch(null); return; }
      try { dispatch(JSON.parse(trimmed)); } catch { dispatch(null); }
    });
    rl.on('close', () => { while (waitingResolvers.length) waitingResolvers.shift()!(null); });
  }
  if (commandQueue.length) return Promise.resolve(commandQueue.shift()!);
  return new Promise((resolve) => waitingResolvers.push(resolve));
}

function prepareWorkspace() {
  const workdir = fs.mkdtempSync(path.join(os.tmpdir(), 'mux-driver-'));
  try {
    execSync('git init -q && git config user.email test@test && git config user.name test', { cwd: workdir, stdio: 'ignore' });
  } catch {
    fs.mkdirSync(path.join(workdir, '.git'), { recursive: true });
  }
  return { workdir };
}

let chatHistoryCalled = false;
let mockReportMarkdown = 'Accepted: Pre-review passed.';

function setupMocks(workdir: string, bindWanxiangshuHost: any) {
  const mockConfig = {
    rootDir: workdir,
    loadConfigOrDefault: () => ({
      projects: new Map([
        [workdir, { trusted: true, workspaces: [{ id: 'mux-e2e-session', name: 'mux-e2e-session', path: workdir }] }]
      ])
    }),
    getSessionDir: () => workdir,
    getServerSshHost: () => 'devbox',
  };

  const mockTaskService = {
    create: async () => ({ success: true, data: { taskId: `task-${Math.random().toString(36).slice(2, 8)}` } }),
    waitForAgentReport: async () => ({ reportMarkdown: mockReportMarkdown }),
  };

  bindWanxiangshuHost({
    config: mockConfig as any,
    aiService: {
      getWorkspaceMetadata: async () => ({
        success: true,
        data: {
          id: 'mux-e2e-session',
          projectName: 'workspace',
          projectPath: workdir,
          runtimeConfig: { type: 'local' },
          aiSettings: { model: 'openai:gpt-4o', thinkingLevel: 'low' },
        }
      }),
      on: () => {},
    } as any,
    workspaceService: {
      getGoalContinuationKickoffSendOptions: () => ({ model: 'openai/gpt-4o', agentId: 'build' }),
      sendMessage: async () => ({ success: true }),
    } as any,
    historyService: {
      getHistoryFromLatestBoundary: async () => {
        chatHistoryCalled = true;
        return { success: true, data: [] };
      }
    } as any,
    taskService: mockTaskService as any,
  });

  return mockTaskService;
}

const sessionToolsMap = new Map<string, { tools: Record<string, any>, allowlisted: Set<string> }>();

function getOrCreateToolsForSession(sessionId: string, bindings: any, mockTaskService: any, globalWorkdir: string) {
  if (sessionToolsMap.has(sessionId)) {
    return sessionToolsMap.get(sessionId)!;
  }
  const sessionWorkdir = path.join(globalWorkdir, 'sandboxes', sessionId);
  fs.mkdirSync(sessionWorkdir, { recursive: true });
  try {
    execSync('git init -q && git config user.email test@test && git config user.name test', { cwd: sessionWorkdir, stdio: 'ignore' });
  } catch {
    fs.mkdirSync(path.join(sessionWorkdir, '.git'), { recursive: true });
  }

  const tools: Record<string, any> = {};
  const allowlisted = new Set<string>();
  bindings.integrateWanxiangshuTools(tools, allowlisted, {
    cwd: sessionWorkdir,
    runtime: { getProject: () => undefined } as any,
    runtimeTempDir: os.tmpdir(),
    workspaceId: sessionId,
    sessionID: sessionId,
    taskService: mockTaskService,
  } as any, sessionId);

  const res = { tools, allowlisted };
  sessionToolsMap.set(sessionId, res);
  return res;
}

async function handleToolAndCommand(
  cmd: any,
  bindings: any,
  globalWorkdir: string,
  mockTaskService: any
) {
  const sessionId = cmd.sessionId ?? 'mux-e2e-session';
  const { tools, allowlisted } = getOrCreateToolsForSession(sessionId, bindings, mockTaskService, globalWorkdir);

  if (cmd.type === 'getToolNames') {
    respond(true, { toolNames: Array.from(allowlisted) });
  } else if (cmd.type === 'getToolSchema') {
    const t = tools[cmd.name];
    if (!t) respond(false, null, `Tool ${cmd.name} not found`);
    else respond(true, { parameters: t.inputSchema });
  } else if (cmd.type === 'getCommands') {
    respond(true, {
      commandNames: bindings.registration.slashCommands.map((c: any) => c.key),
      slashCommands: bindings.registration.slashCommands.map((c: any) => ({ key: c.key, description: c.description })),
    });
  } else if (cmd.type === 'executeTool') {
    const t = tools[cmd.name];
    if (!t) { respond(false, null, `Tool ${cmd.name} not found`); return; }
    let res: any, threw = true;
    try {
      res = await t.execute(cmd.args, { toolCallId: cmd.toolCallId ?? 'call-123' });
      threw = false;
    } catch (e) {
      res = e instanceof Error ? e.message : String(e);
    }
    respond(!threw, res);
  } else if (cmd.type === 'runCommand') {
    const res = await bindings.executeWanxiangshuSlashCommand(cmd.name, cmd.sessionId ?? 'mux-e2e-session', cmd.args ?? '');
    respond(true, res);
  }
}

async function handleTransformAndEvent(
  cmd: any,
  transformWanxiangshuMessages: any,
  runWanxiangshuSystemTransform: any,
  registration: any,
  eventHelpers: any
) {
  const sessionId = cmd.workspaceId || cmd.sessionId || 'mux-e2e-session';
  if (cmd.type === 'transformMessages') {
    const sessionWorkdir = path.join(eventHelpers.getTodos(), 'sandboxes', sessionId);
    fs.mkdirSync(sessionWorkdir, { recursive: true });
    const res = await transformWanxiangshuMessages({
      workspacePath: sessionWorkdir,
      workspaceId: sessionId,
      messages: cmd.messages,
    });
    respond(true, res);
  } else if (cmd.type === 'systemTransform') {
    const res = await runWanxiangshuSystemTransform({ system: cmd.system ?? null });
    respond(true, res);
  } else if (cmd.type === 'emit') {
    const res = await registration.eventHook(
      { type: cmd.eventType, workspaceId: sessionId, properties: cmd.event },
      eventHelpers
    );
    respond(true, res);
  }
}

async function handleFileOps(cmd: any, globalWorkdir: string) {
  const sessionId = cmd.sessionId || 'mux-e2e-session';
  const finalWorkdir = path.join(globalWorkdir, 'sandboxes', sessionId);
  fs.mkdirSync(finalWorkdir, { recursive: true });
  const ndjsonPath = path.join(finalWorkdir, '.wanxiangshu.ndjson');

  if (cmd.type === 'readNdjson') {
    let c = '';
    try { c = fs.readFileSync(ndjsonPath, 'utf8'); } catch {}
    respond(true, { content: c });
  } else if (cmd.type === 'readFile') {
    let c = '';
    try { c = fs.readFileSync(path.join(finalWorkdir, cmd.path), 'utf8'); }
    catch (e) { respond(false, null, `Read failed: ${e instanceof Error ? e.message : String(e)}`); return; }
    respond(true, { content: c });
  } else if (cmd.type === 'fileExists') {
    respond(true, { exists: fs.existsSync(path.join(finalWorkdir, cmd.path)) });
  } else if (cmd.type === 'waitForNdjson') {
    const min = cmd.min ?? 1;
    const deadline = Date.now() + (cmd.maxMs ?? 1000);
    let count = 0;
    while (Date.now() < deadline) {
      let c = '';
      try { c = fs.readFileSync(ndjsonPath, 'utf8'); } catch {}
      count = c.split('\n').filter(Boolean).length;
      if (count >= min) break;
      await new Promise((r) => setTimeout(r, 50));
    }
    respond(true, { ready: count >= min, count });
  }
}

function patchBoundConfig(bindings: any, globalWorkdir: string) {
  if (bindings.boundConfig) {
    bindings.boundConfig.getSessionDir = (workspaceId?: string) => {
      if (workspaceId) {
        const p = path.join(globalWorkdir, 'sandboxes', workspaceId);
        fs.mkdirSync(p, { recursive: true });
        return p;
      }
      return globalWorkdir;
    };
    bindings.boundConfig.rootDir = globalWorkdir;
  }
}

async function patchConfigPrototypesAsync(muxRepo: string, workdir: string) {
  try {
    const c1 = await import("@/node/config");
    c1.Config.prototype.getSessionDir = (workspaceId?: string) => {
      if (workspaceId) return path.join(workdir, 'sandboxes', workspaceId);
      return workdir;
    };
  } catch (e) {}
  try {
    const c2 = await import(`${muxRepo}/src/node/config`);
    c2.Config.prototype.getSessionDir = (workspaceId?: string) => {
      if (workspaceId) return path.join(workdir, 'sandboxes', workspaceId);
      return workdir;
    };
  } catch (e) {}
  try {
    const c3 = await import("../../mux/src/node/config");
    c3.Config.prototype.getSessionDir = (workspaceId?: string) => {
      if (workspaceId) return path.join(workdir, 'sandboxes', workspaceId);
      return workdir;
    };
  } catch (e) {}
}

async function initializePluginAndHost(muxRepo: string, workdir: string) {
  const pluginPath = process.env.WANXIANGSHU_PLUGIN_PATH;
  if (!pluginPath) {
    process.stderr.write('WANXIANGSHU_PLUGIN_PATH env var required\n');
    process.exit(1);
  }
  const mod = await import(pluginPath!);
  const factory = mod.default ?? mod.createRegistration;
  if (typeof factory !== 'function') {
    respond(false, null, `Plugin factory missing`);
    process.exit(1);
  }
  await patchConfigPrototypesAsync(muxRepo, workdir);
  const bindings = await import("@/node/services/wanxiangshuBinding");
  const mockTaskService = setupMocks(workdir, (deps: any) => {
    if (deps && deps.config) {
      deps.config.getSessionDir = (workspaceId?: string) => {
        if (workspaceId) return path.join(workdir, 'sandboxes', workspaceId);
        return workdir;
      };
      deps.config.rootDir = workdir;
    }
    return bindings.bindWanxiangshuHost(deps);
  });
  const tools: Record<string, any> = {};
  const allowlisted = new Set<string>();
  bindings.integrateWanxiangshuTools(tools, allowlisted, {
    cwd: workdir,
    runtime: { getProject: () => undefined } as any,
    runtimeTempDir: os.tmpdir(),
    workspaceId: 'mux-e2e-session',
    sessionID: 'mux-e2e-session',
    taskService: mockTaskService,
  } as any, 'mux-e2e-session');
  patchBoundConfig(bindings, workdir);
  return { tools, allowlisted, bindings, mockTaskService };
}

async function executeAction(action: any) {
  const prevStdout = process.stdout.write;
  let intercepted = false;
  process.stdout.write = (chunk: any) => { intercepted = true; return prevStdout.call(process.stdout, chunk); };
  await action();
  process.stdout.write = prevStdout;
  return intercepted;
}

const nudges: string[] = [];
let globalWorkdir = "";
const eventHelpers = {
  nudge: async (wsId: string, msg: string) => { nudges.push(msg); return true; },
  getTodos: () => globalWorkdir,
};

async function processCommand(cmd: any, bindings: any, globalWorkdir: string, mockTaskService: any, symlinkPath: string) {
  if (cmd.type === 'dispose') {
    try { fs.unlinkSync(symlinkPath); } catch {}
    try { fs.rmSync(globalWorkdir, { recursive: true, force: true }); } catch {}
    respond(true, { disposed: true });
    process.exit(0);
  }
  if (cmd.type === 'cleanSandbox') {
    const sessionId = cmd.sessionId;
    if (sessionId) {
      const finalWorkdir = path.join(globalWorkdir, 'sandboxes', sessionId);
      try { fs.rmSync(finalWorkdir, { recursive: true, force: true }); } catch {}
    }
    respond(true, { cleaned: true });
    return;
  }
  if (cmd.type === 'getChatHistoryCalled') {
    respond(true, { called: chatHistoryCalled });
    return;
  }
  if (cmd.type === 'getReviewTask') {
    const task = bindings.registration.__reviewStore.getReviewTask(cmd.sessionId ?? 'mux-e2e-session');
    respond(true, { task: task ?? null });
    return;
  }
  if (cmd.type === 'setMockReportMarkdown') {
    mockReportMarkdown = cmd.markdown;
    respond(true, { success: true });
    return;
  }
  if (cmd.type === 'getNudges') {
    respond(true, { nudges });
    return;
  }
  const categories = [
    () => handleToolAndCommand(cmd, bindings, globalWorkdir, mockTaskService),
    () => handleTransformAndEvent(cmd, bindings.transformWanxiangshuMessages, bindings.runWanxiangshuSystemTransform, bindings.registration, eventHelpers),
    () => handleFileOps(cmd, globalWorkdir)
  ];
  let matched = false;
  for (const action of categories) {
    if (await executeAction(action)) { matched = true; break; }
  }
  if (!matched) respond(false, null, `Unhandled command ${cmd.type}`);
}

async function main() {
  const muxRepo = process.env.WANXIANGSHU_MUX_REPO ?? process.cwd();
  const { workdir } = prepareWorkspace();
  globalWorkdir = workdir;
  const symlinkPath = path.join(muxRepo, 'mux-e2e-session');
  if (fs.existsSync(symlinkPath)) {
    try { fs.unlinkSync(symlinkPath); } catch {}
  }
  try { fs.symlinkSync(workdir, symlinkPath); } catch {}
  const { tools, allowlisted, bindings, mockTaskService } = await initializePluginAndHost(muxRepo, workdir);
  process.stdout.write('ready|' + workdir + '\n');
  for (;;) {
    const cmd = await readStdinJson();
    if (!cmd) break;
    patchBoundConfig(bindings, workdir);
    await processCommand(cmd, bindings, workdir, mockTaskService, symlinkPath);
  }
}

main().catch((err) => {
  process.stderr.write(`driver fatal: ${err instanceof Error ? err.message : String(err)}\n`);
  process.exit(1);
});