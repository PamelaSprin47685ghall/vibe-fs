import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { createMockLLM } from '../e2e/mock-llm.js';
import { OpencodePluginHarness } from './opencode-plugin-harness-class.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PROJECT_ROOT = path.resolve(__dirname, '..');

function getPluginPath(variant) {
  let file = 'Plugin.js';
  if (variant === 'mimocode') file = 'PluginMimo.js';
  if (variant === 'mimotui') file = 'PluginMimoTui.js';
  let p = path.resolve(PROJECT_ROOT, `build/src/Hosts/OpenCode/${file}`);
  if (!fs.existsSync(p)) {
    let altRoot = path.resolve(PROJECT_ROOT, '..');
    p = path.resolve(altRoot, `build/src/Hosts/OpenCode/${file}`);
  }
  return p;
}

async function loadPlugin(variant) {
  const pluginUrl = pathToFileURL(getPluginPath(variant)).href;
  return import(pluginUrl);
}

function buildMockClient(messages = [], opts = {}, hookRef) {
  return {
    session: {
      messages: async () => ({ data: messages }),
      create: async () => ({ data: { id: 'mock-child-session' } }),
      ...(opts.mockSessionClient || {}),
      prompt: async (body) => {
        const promptBody = (body && body.body) || {};
        const hook = (hookRef || {}).hook;
        if (hook) {
          const parts = (promptBody && promptBody.parts) || [];
          const nonce = (parts[0] && parts[0].metadata && parts[0].metadata.nonce) || ('mock-' + Date.now());
          const input = {
            sessionID: (body && body.path && body.path.id) || '',
            agent: (promptBody && promptBody.agent) || 'build',
          };
          const output = {
            parts,
            message: { id: nonce, role: 'assistant', agent: (promptBody && promptBody.agent) || 'build' },
          };
          try { await hook(input, output); } catch (_) {}
        }
        const mockPrompt = (opts.mockSessionClient || {}).prompt;
        return mockPrompt ? await mockPrompt(body) : undefined;
      },
      abort: async () => ({}),
    },
    ...(opts.mockClientExtra || {}),
  };
}

export async function start(opts = {}) {
  const llm = createMockLLM();
  const llmHandle = await llm.start();
  delete process.env.OLLAMA_API_KEY;
  delete process.env.OLLAMA_API_BASE;

  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'opencode-e2e-'));
  const workDir = path.join(home, 'workspace');
  fs.mkdirSync(workDir, { recursive: true });

  const { execSync } = await import('node:child_process');
  execSync('git init', { cwd: workDir, stdio: 'ignore' });
  execSync('git config user.email test@example.com', { cwd: workDir, stdio: 'ignore' });
  execSync('git config user.name test', { cwd: workDir, stdio: 'ignore' });
  fs.writeFileSync(path.join(workDir, 'README.md'), '# test\n');
  fs.writeFileSync(path.join(workDir, 'AGENTS.md'), opts.agentsContent || '- e2e test workspace\n');
  execSync('git add README.md AGENTS.md', { cwd: workDir, stdio: 'ignore' });
  execSync('git commit -m init', { cwd: workDir, stdio: 'ignore' });

  const hookRef = { hook: null };
  const client = buildMockClient(opts.messages || [], opts, hookRef);

  const plugin = await loadPlugin(opts.variant);
  const pluginDefault = plugin.default || plugin;
  const ctx = { directory: workDir, client, workdir: workDir, ...(opts.pluginCtxExtra || {}) };

  let resultPlugin;
  let seams = null;

  if (typeof pluginDefault.pluginForWithSeams === 'function') {
    const seamsResult = await pluginDefault.pluginForWithSeams(ctx);
    resultPlugin = seamsResult.Plugin;
    seams = { ReviewStore: seamsResult.ReviewStore, FallbackRuntime: seamsResult.FallbackRuntime };
  } else {
    resultPlugin = await (pluginDefault.server || pluginDefault.setup || pluginDefault)(ctx);
  }

  hookRef.hook = resultPlugin['chat.message'] || resultPlugin['experimental.chat.messages.transform'] || null;

  const sessionId = opts.sessionId || 'opencode-e2e-session';
  return new OpencodePluginHarness(workDir, home, sessionId, resultPlugin, llmHandle, seams);
}
