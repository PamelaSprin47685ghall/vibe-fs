import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js';
import { OPENCODE_BIN, initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js';
import { buildTextChunks, buildToolCallChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js';
import { readRequestBody, startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js';
import { resolvePluginPath } from '../../../verification-system/tests/e2e/support/scenario-paths.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, '../../../..');
const canaryPluginPath = path.join(here, 'manager-review-tools-canary-plugin.mjs');
const fixtureCandidate1 = path.join(here, '../../fixtures/manager-review-tools-canary-1.18.29.json');
const fixtureCandidate2 = path.join(here, '../fixtures/manager-review-tools-canary-1.18.29.json');
const fixturePath = fs.existsSync(fixtureCandidate1) ? fixtureCandidate1 : fixtureCandidate2;
const fixture = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));

let productionPluginPath = null;
try {
  productionPluginPath = resolvePluginPath('opencode', { productionRoot: repoRoot });
} catch {
  const fallback = path.join(repoRoot, 'dist', 'OpenCode', 'Plugin', 'Plugin.js');
  if (fs.existsSync(fallback)) productionPluginPath = fallback;
}

const pluginPackageJsonPath = path.join(repoRoot, 'node_modules/@opencode-ai/plugin/package.json');
const pluginVersion = JSON.parse(fs.readFileSync(pluginPackageJsonPath, 'utf8')).version;
const opencodeVersion = execFileSync(OPENCODE_BIN, ['--version'], { encoding: 'utf8' }).trim().replace(/^v/, '');
assert.equal(pluginVersion, fixture.targetVersion);
assert.equal(opencodeVersion, fixture.targetVersion);
assert.ok(productionPluginPath, 'production plugin must be available');

const REVIEW_TOOLS = ['js-manager'];
const CONTROL_TOOLS = ['read', 'grep', 'glob', 'js-engineer', 'js-devops'];
const CONTRACT_TOKEN = 'do-not-use-except-for-review';

// ── Collector setup ──────────────────────────────────────────────────────────

const observations = [];
const waiters = [];

const publish = (observation) => {
  const recorded = { sequence: observations.length + 1, ...observation };
  observations.push(recorded);
  for (let index = waiters.length - 1; index >= 0; index -= 1) {
    if (!waiters[index].predicate(recorded)) continue;
    waiters.splice(index, 1)[0].resolve(recorded);
  }
};

const waitFor = (predicate, timeoutMs = 25000) => {
  const existing = observations.find(predicate);
  if (existing) return Promise.resolve(existing);
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      const idx = waiters.findIndex((w) => w.resolve === resolve);
      if (idx >= 0) waiters.splice(idx, 1);
      reject(new Error(`Timed out waiting for observation after ${timeoutMs}ms`));
    }, timeoutMs);
    waiters.push({
      predicate,
      resolve: (value) => {
        clearTimeout(timer);
        resolve(value);
      },
    });
  });
};

const collector = http.createServer(async (request, response) => {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  try {
    const parsed = JSON.parse(Buffer.concat(chunks).toString('utf8'));
    publish(parsed);
  } catch {}
  response.writeHead(204).end();
});

await new Promise((resolve, reject) => {
  collector.once('error', reject);
  collector.listen(0, '127.0.0.1', resolve);
});
const collectorUrl = `http://127.0.0.1:${collector.address().port}`;

// ── Mock Provider setup with Request Routing Isolation ───────────────────────

const providerRequests = [];
const wireInspection = {
  contractRequired: false,
  contractType: null,
  contractEnum: null,
  historicalToolCallPreservesContract: false,
  round2ToolsStable: false,
};

let sessionID = null;
let managerStep = 0;

const isTitleRequest = (body) => {
  const messages = Array.isArray(body?.messages) ? body.messages : [];
  return (
    messages.slice(0, 4).some(
      (m) => typeof m?.content === 'string' && m.content.startsWith('Generate a title for this conversation:'),
    ) ||
    messages.some(
      (m) => m?.role === 'system' && typeof m?.content === 'string' && m.content.includes('title generator'),
    )
  );
};

const isManagerRequest = (request, body) => {
  if (isTitleRequest(body)) return false;

  const reqSessionId =
    request.headers['x-session-affinity'] ||
    request.headers['x-session-id'] ||
    request.headers['x-opencode-session'] ||
    null;

  // If header specifies a session ID and we already have our target sessionID, must match
  if (sessionID && reqSessionId && reqSessionId !== sessionID) {
    return false;
  }

  const toolNames = (body?.tools ?? [])
    .map((t) => t?.function?.name ?? t?.name)
    .filter((n) => typeof n === 'string');

  // Review tools are strictly exclusive to Role.Manager
  const hasManagerReviewTools = toolNames.includes('js-manager');

  if (!hasManagerReviewTools) {
    return false;
  }

  // Reject blogger requests that might slip through
  const messages = Array.isArray(body?.messages) ? body.messages : [];
  const hasBloggerPrompt = messages.some(
    (m) =>
      m?.role === 'system' &&
      typeof m?.content === 'string' &&
      (m.content.includes('role/blogger') || m.content.includes('BloggerSystemPrompt')),
  );
  if (hasBloggerPrompt) {
    return false;
  }

  return true;
};

const provider = await startHttpServer(async (request, response) => {
  const url = new URL(request.url, `http://${request.headers.host}`);
  if ((url.pathname === '/v1/models' || url.pathname === '/models') && request.method === 'GET') {
    sendJSON(response, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] });
    return;
  }

  if (url.pathname === '/v1/chat/completions' && request.method === 'POST') {
    const body = await readRequestBody(request);
    providerRequests.push(body);

    // 1. Isolate Title requests -> return harmless plain text, do not advance manager steps
    if (isTitleRequest(body)) {
      sendSSE(response, buildTextChunks(`title_${Date.now()}`, 'Manager Review Canary Title', 1));
      return;
    }

    // 2. Isolate Blogger or other companion sidecars -> return harmless text, do not advance manager steps
    if (!isManagerRequest(request, body)) {
      sendSSE(response, buildTextChunks(`companion_${Date.now()}`, 'COMPANION_OK', 1));
      return;
    }

    // 3. Target Manager session requests: step exclusively for manager lane
    managerStep += 1;

    // Manager Step 1: Initial prompt -> issue js-manager tool call (with contract)
    if (managerStep === 1) {
      if (Array.isArray(body.tools)) {
        const readManagerTool = body.tools.find(
          (t) => (t?.function?.name ?? t?.name) === 'js-manager',
        );
        const contractProp = readManagerTool?.function?.parameters?.properties?.contract;
        const required = readManagerTool?.function?.parameters?.required ?? [];
        wireInspection.contractType = contractProp?.type ?? null;
        wireInspection.contractEnum = contractProp?.enum ?? null;
        wireInspection.contractRequired = Array.isArray(required) && required.includes('contract');
      }

      const call = {
        name: 'js-manager',
        argsStr: JSON.stringify({
          program: "class Js extends JsProgram { async run() { const f = await this.file('fixture-sample.txt'); return f.text('^', '$'); } }",
          contract: CONTRACT_TOKEN,
        }),
      };
      sendSSE(response, buildToolCallChunks('call_read_norm_1', call.name, call.argsStr, 10));
      return;
    }

    // Manager Step 2: Follow-up after js-manager normal execution
    if (managerStep === 2) {
      sendSSE(response, buildTextChunks('resp_read_norm_done', 'CANARY_READ_DONE', 15));
      return;
    }

    // Manager Step 3: Stability verification prompt (inspect history and tools)
    if (managerStep === 3) {
      let foundContractInHistory = false;
      for (const msg of body.messages ?? []) {
        if (msg.role === 'assistant' && Array.isArray(msg.tool_calls)) {
          for (const tc of msg.tool_calls) {
            try {
              const parsedArgs = JSON.parse(tc.function?.arguments ?? '{}');
              if (parsedArgs.contract === CONTRACT_TOKEN) {
                foundContractInHistory = true;
              }
            } catch {}
          }
        }
      }
      wireInspection.historicalToolCallPreservesContract = foundContractInHistory;

      const toolNames = (body.tools ?? []).map((t) => t?.function?.name ?? t?.name);
      wireInspection.round2ToolsStable = REVIEW_TOOLS.every((name) => toolNames.includes(name));

      publish({ kind: 'manager.stability.done' });
      sendSSE(response, buildTextChunks('resp_stability_done', 'CANARY_STABILITY_DONE', 20));
      return;
    }

    // Manager Step 4: Error path prompt -> issue js-manager with nonexistent file
    if (managerStep === 4) {
      const call = {
        name: 'js-manager',
        argsStr: JSON.stringify({
          program: "class Js extends JsProgram { async run() { const f = await this.file('nonexistent-missing-file.txt'); return f.text('^', '$'); } }",
          contract: CONTRACT_TOKEN,
        }),
      };
      sendSSE(response, buildToolCallChunks('call_read_err_1', call.name, call.argsStr, 25));
      return;
    }

    // Manager Step 5: Follow-up after tool error
    if (managerStep === 5) {
      sendSSE(response, buildTextChunks('resp_err_done', 'CANARY_ERROR_DONE', 30));
      return;
    }

    // Manager Step 6: Cancellation path prompt -> issue long-running js-manager call
    if (managerStep === 6) {
      const call = {
        name: 'js-manager',
        argsStr: JSON.stringify({
          program:
            'class Js extends JsProgram { async run() { await new Promise(() => {}); return null; } }',
          contract: CONTRACT_TOKEN,
        }),
      };
      sendSSE(response, buildToolCallChunks('call_js_cancel_1', call.name, call.argsStr, 35));
      return;
    }

    // Fallback if additional manager steps arrive
    sendSSE(response, buildTextChunks(`resp_step_${managerStep}`, 'CANARY_OK', 40));
    return;
  }

  sendJSON(response, 404, { error: `unexpected ${request.method} ${url.pathname}` });
});

// ── Host client helper ──────────────────────────────────────────────────────

const request = async (baseUrl, method, pathname, body, expectedStatus) => {
  const response = await fetch(baseUrl + pathname, {
    method,
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  if (expectedStatus !== undefined) {
    const allowed = Array.isArray(expectedStatus) ? expectedStatus : [expectedStatus];
    assert.ok(allowed.includes(response.status), `${method} ${pathname}: ${text}`);
  }
  return { status: response.status, data: text ? JSON.parse(text) : null };
};

const sessionIdOf = ({ data }) => data?.data?.data?.id ?? data?.data?.id ?? data?.id;

const prompt = (messageID, text) => ({
  messageID,
  agent: 'manager',
  model: { providerID: 'test', modelID: 'test-model' },
  parts: [{ type: 'text', text }],
});

// ── Run Canary Scenario ─────────────────────────────────────────────────────

const scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-manager-review-canary-'));
const workspace = path.join(scenarioDir, 'workspace');
fs.mkdirSync(workspace, { recursive: true });

// Create test fixture file for js-manager
fs.writeFileSync(
  path.join(workspace, 'fixture-sample.txt'),
  'Hello Wanxiangshu Manager Review Tools Canary\nLine 2: sample text\n',
  'utf8',
);
await initGitWorkspace(workspace);

const host = new ProcessHost();

try {
  await host.start({
    scenarioDir,
    providerUrl: `${provider.url}/v1`,
    pluginPaths: [canaryPluginPath],
    extraEnv: {
      WANXIANGSHU_REVIEW_TOOLS_CANARY_COLLECTOR: collectorUrl,
      ...(productionPluginPath ? { WANXIANGSHU_REVIEW_TOOLS_PRODUCTION_PLUGIN: productionPluginPath } : {}),
    },
  });

  const sessionRes = await request(
    host.baseUrl,
    'POST',
    '/api/session',
    {
      agent: 'manager',
      model: { providerID: 'test', id: 'test-model' },
    },
    200,
  );
  sessionID = sessionIdOf(sessionRes);
  assert.ok(sessionID, 'session creation failed to return sessionID');

  // 1. Normal js-manager prompt
  const msg1 = 'msg_canary_prompt_1';
  await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt(msg1, 'READ_SAMPLE'), 204);

  // Wait for normal before & after observations
  const normBeforeObs = await waitFor(
    ({ kind, value }) => kind === 'tool.execute.before.observed' && value?.callID === 'call_read_norm_1',
  );
  const normAfterObs = await waitFor(
    ({ kind, value }) => kind === 'tool.execute.after.observed' && value?.callID === 'call_read_norm_1',
  );
  const normalTerminal = await waitFor(
    ({ kind, value }) => kind === 'tool.terminal.observed' && value?.callID === 'call_read_norm_1',
  );

  // 2. Round 2: stability prompt
  const msg2 = 'msg_canary_prompt_2';
  await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt(msg2, 'VERIFY_STABILITY'), 204);
  await waitFor(({ kind }) => kind === 'manager.stability.done');

  // 3. Round 3: error path (read nonexistent file)
  const msg3 = 'msg_canary_prompt_3';
  await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt(msg3, 'TRIGGER_ERROR'), 204);
  const errBeforeObs = await waitFor(
    ({ kind, value }) => kind === 'tool.execute.before.observed' && value?.callID === 'call_read_err_1',
  );
  const errAfterObs = await waitFor(
    ({ kind, value }) => kind === 'tool.execute.after.observed' && value?.callID === 'call_read_err_1',
  );
  const errorTerminal = await waitFor(
    ({ kind, value }) => kind === 'tool.terminal.observed' && value?.callID === 'call_read_err_1',
  );

  // 4. Round 4: cancellation path (long task + abort)
  const msg4 = 'msg_canary_prompt_4';
  await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt(msg4, 'TRIGGER_CANCEL'), 204);
  const cancelBeforeObs = await waitFor(
    ({ kind, value }) => kind === 'tool.execute.before.observed' && value?.callID === 'call_js_cancel_1',
  );
  const cancelRunningObs = await waitFor(
    ({ kind, value }) => kind === 'tool.running.observed' && value?.callID === 'call_js_cancel_1',
  );
  assert.equal(observations.some(({ kind, value }) =>
    kind === 'tool.terminal.observed' && value?.callID === 'call_js_cancel_1'), false,
  'cancellation requires a live call, not one already failed');
  const observationsBeforeAbort = observations.length;

  // Trigger external abort
  await request(host.baseUrl, 'POST', `/session/${sessionID}/abort`, {}, [200, 204]);

  const cancelAfterObs = await waitFor(
    ({ kind, value }) => kind === 'tool.execute.after.observed' && value?.callID === 'call_js_cancel_1',
  );
  const cancelTerminal = await waitFor(
    ({ kind, value }) => kind === 'tool.terminal.observed' && value?.callID === 'call_js_cancel_1',
  );
  assert.ok(cancelTerminal.sequence > observationsBeforeAbort);
  assert.ok(cancelTerminal.sequence > cancelRunningObs.sequence);

  // ── Verification & Assertions against Fixture ───────────────────────────────

  // Check definition observations
  const defObs = observations.filter(({ kind }) => kind === 'tool.definition.observed');
  for (const tool of REVIEW_TOOLS) {
    const o = defObs.find(({ value }) => value?.toolName === tool);
    assert.ok(o, `missing definition observation for review tool ${tool}`);
    assert.equal(o.value.hasContractProperty, true, `${tool} definition missing contract property`);
  }

  // Normal terminal assertions
  assert.equal(normBeforeObs.value.argsIdentityPreserved, true, 'before must preserve args reference identity');
  assert.equal(normBeforeObs.value.preContractInArgs, true, 'pre-before args must carry contract');
  assert.equal(normBeforeObs.value.postContractInArgs, false, 'post-before must hide contract from args');
  assert.equal(normBeforeObs.value.businessKeysPreserved, true, 'post-before must preserve business args');

  assert.equal(normAfterObs.value.identityWithBefore, true, 'after must receive same args reference as before');
  assert.equal(normAfterObs.value.postAfterContractInArgs, true, 'post-after must restore contract on args');
  assert.equal(normAfterObs.value.postAfterContractValue, CONTRACT_TOKEN, 'restored contract value mismatch');

  // Error terminal assertions
  assert.equal(errBeforeObs.value.postContractInArgs, false, 'error before must hide contract');
  assert.equal(errAfterObs.value.postAfterContractInArgs, true, 'error after must restore contract');

  // Cancellation assertions
  assert.equal(cancelBeforeObs.value.postContractInArgs, false, 'cancel before must hide contract');

  // Wire inspection assertions
  assert.equal(
    wireInspection.historicalToolCallPreservesContract,
    true,
    'round 2 provider request must preserve contract in historical tool_calls',
  );
  const calls = {};
  for (const [name, observation, terminal] of [
    ['normal', normAfterObs, normalTerminal],
    ['executorError', errAfterObs, errorTerminal],
    ['cancellation', cancelAfterObs, cancelTerminal],
  ]) {
    const value = observation.value;
    calls[name] = {
      sameArguments: value.identityWithBefore,
      originalOrder: value.originalOrder,
      originalValues: value.originalValues,
      durableInputRetainsContract: terminal.value.contractRetained,
      originalDurableInput: terminal.value.originalInput,
      status: terminal.value.status,
    };
    assert.equal(value.identityWithBefore, true, name);
    assert.equal(value.originalOrder, true, name);
    assert.equal(value.originalValues, true, name);
    assert.equal(terminal.value.contractRetained, true, name);
    assert.equal(terminal.value.originalInput, true, name);
  }
  assert.equal(calls.normal.status, 'completed');
  assert.equal(calls.executorError.status, 'error');
  assert.equal(calls.cancellation.status, 'error');

  // ── Output Final Summary Artifact ──────────────────────────────────────────

  const summary = {
    schemaVersion: 1,
    versions: { opencode: opencodeVersion, plugin: pluginVersion },
    calls,
    historicalToolCallPreservesContract: wireInspection.historicalToolCallPreservesContract,
    observationsCount: observations.length,
    providerRequestsCount: providerRequests.length,
  };

  process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
} catch (err) {
  console.error('[run-manager-review-tools-canary] Canary failed:', err);
  process.exitCode = 1;
} finally {
  if (sessionID && host.baseUrl) {
    try {
      await request(host.baseUrl, 'POST', `/session/${sessionID}/abort`, {}, 204);
    } catch {}
  }
  try {
    await host.stop();
  } catch {}
  await stopHttpServer(provider.server);
  await new Promise((resolve) => collector.close(resolve));
  fs.rmSync(scenarioDir, { recursive: true, force: true });
}
