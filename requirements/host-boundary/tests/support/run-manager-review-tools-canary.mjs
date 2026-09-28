import assert from 'node:assert/strict';
import { execFile } from 'node:child_process';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

import { ProcessHost } from '../../../verification-system/tests/e2e/support/process-host.js';
import { OPENCODE_BIN, initGitWorkspace } from '../../../verification-system/tests/e2e/support/process-host-utils.js';
import { buildTextChunks, buildToolCallChunks, sendJSON, sendSSE } from '../../../verification-system/tests/e2e/support/strict-mock-sse.js';
import { readRequestBody, startHttpServer, stopHttpServer } from '../../../verification-system/tests/e2e/support/strict-mock-server.js';
import { resolvePluginPath } from '../../../verification-system/tests/e2e/support/scenario-paths.js';

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, '../../../..');
const canaryPluginPath = path.join(here, 'manager-review-tools-canary-plugin.mjs');
const fixture = JSON.parse(fs.readFileSync(path.join(here, '../../fixtures/manager-review-tools-canary-1.18.29.json'), 'utf8'));
const execute = promisify(execFile);
const CONTRACT_TOKEN = 'do-not-use-except-for-review';

const isTitleRequest = (body) => {
  const messages = Array.isArray(body?.messages) ? body.messages : [];
  return messages.slice(0, 4).some(
    (message) => typeof message?.content === 'string' && message.content.startsWith('Generate a title for this conversation:'),
  ) || messages.some(
    (message) => message?.role === 'system' && typeof message?.content === 'string' && message.content.includes('title generator'),
  );
};

const isManagerRequest = (request, body, sessionID) => {
  if (isTitleRequest(body)) return false;
  const requestSession = request.headers['x-session-affinity'] || request.headers['x-session-id'] || request.headers['x-opencode-session'];
  if (sessionID && requestSession && requestSession !== sessionID) return false;
  const tools = (body?.tools ?? []).map((tool) => tool?.function?.name ?? tool?.name);
  if (!tools.includes('js-manager')) return false;
  return !(body.messages ?? []).some((message) => message?.role === 'system'
    && typeof message?.content === 'string'
    && (message.content.includes('role/blogger') || message.content.includes('BloggerSystemPrompt')));
};

const request = async (baseUrl, method, pathname, body, expectedStatus) => {
  const response = await fetch(baseUrl + pathname, {
    method,
    headers: { 'content-type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await response.text();
  const allowed = Array.isArray(expectedStatus) ? expectedStatus : [expectedStatus];
  assert.ok(allowed.includes(response.status), `${method} ${pathname}: ${text}`);
  return { status: response.status, data: text ? JSON.parse(text) : null };
};

const prompt = (messageID, text) => ({
  messageID,
  agent: 'manager',
  model: { providerID: 'test', modelID: 'test-model' },
  parts: [{ type: 'text', text }],
});

const callEvidence = (restored, terminal) => ({
  sameArguments: restored.identityWithBefore,
  originalOrder: restored.originalOrder,
  originalValues: restored.originalValues,
  durableInputRetainsContract: terminal.value.contractRetained,
  originalDurableInput: terminal.value.originalInput,
  status: terminal.value.status,
});

export async function runManagerReviewToolsCanary(verify) {
  const productionPluginPath = resolvePluginPath('opencode', { productionRoot: repoRoot });
  const pluginVersion = JSON.parse(fs.readFileSync(path.join(repoRoot, 'node_modules/@opencode-ai/plugin/package.json'), 'utf8')).version;
  const { stdout } = await execute(OPENCODE_BIN, ['--version'], { encoding: 'utf8' });
  const versions = { opencode: stdout.trim().replace(/^v/, ''), plugin: pluginVersion };
  assert.equal(versions.plugin, fixture.targetVersion);
  assert.equal(versions.opencode, fixture.targetVersion);
  await verify.versions(versions);

  const observations = [];
  const waiters = [];
  const providerRequests = [];
  const wireInspection = { historicalToolCallPreservesContract: false };
  const failures = [];
  const host = new ProcessHost();
  let provider;
  let scenarioDir;
  let sessionID;
  let managerStep = 0;
  let evidence;
  const publish = (observation) => {
    const recorded = { sequence: observations.length + 1, ...observation };
    observations.push(recorded);
    for (let index = waiters.length - 1; index >= 0; index -= 1) {
      if (!waiters[index].predicate(recorded)) continue;
      waiters.splice(index, 1)[0].resolve(recorded);
    }
  };
  const waitFor = (predicate) => {
    const existing = observations.find(predicate);
    return existing ? Promise.resolve(existing) : new Promise((resolve) => waiters.push({ predicate, resolve }));
  };
  const observeCall = (kind, callID) => waitFor((observation) => observation.kind === kind && observation.value?.callID === callID);
  const collector = http.createServer(async (incoming, response) => {
    const chunks = [];
    for await (const chunk of incoming) chunks.push(chunk);
    publish(JSON.parse(Buffer.concat(chunks).toString('utf8')));
    response.writeHead(204).end();
  });

  try {
    await new Promise((resolve, reject) => {
      collector.once('error', reject);
      collector.listen(0, '127.0.0.1', resolve);
    });
    const collectorUrl = `http://127.0.0.1:${collector.address().port}`;
    provider = await startHttpServer(async (incoming, response) => {
      const url = new URL(incoming.url, `http://${incoming.headers.host}`);
      if ((url.pathname === '/v1/models' || url.pathname === '/models') && incoming.method === 'GET') {
        sendJSON(response, 200, { object: 'list', data: [{ id: 'test-model', object: 'model' }] });
        return;
      }
      if (url.pathname !== '/v1/chat/completions' || incoming.method !== 'POST') {
        sendJSON(response, 404, { error: `unexpected ${incoming.method} ${url.pathname}` });
        return;
      }
      const body = await readRequestBody(incoming);
      providerRequests.push(body);
      if (isTitleRequest(body)) {
        sendSSE(response, buildTextChunks(`title_${Date.now()}`, 'Manager Review Canary Title', 1));
        return;
      }
      if (!isManagerRequest(incoming, body, sessionID)) {
        sendSSE(response, buildTextChunks(`companion_${Date.now()}`, 'COMPANION_OK', 1));
        return;
      }
      managerStep += 1;
      if (managerStep === 1) {
        sendSSE(response, buildToolCallChunks('call_read_norm_1', 'js-manager', JSON.stringify({
          program: "class Js extends JsProgram { async run() { const f = await this.file('fixture-sample.txt'); return f.text('^', '$'); } }",
          contract: CONTRACT_TOKEN,
        }), 10));
        return;
      }
      if (managerStep === 2) {
        sendSSE(response, buildTextChunks('resp_read_norm_done', 'CANARY_READ_DONE', 15));
        return;
      }
      if (managerStep === 3) {
        wireInspection.historicalToolCallPreservesContract = (body.messages ?? []).some((message) =>
          message.role === 'assistant' && (message.tool_calls ?? []).some((call) => {
            try { return JSON.parse(call.function?.arguments ?? '{}').contract === CONTRACT_TOKEN; }
            catch { return false; }
          }));
        publish({ kind: 'manager.stability.done' });
        sendSSE(response, buildTextChunks('resp_stability_done', 'CANARY_STABILITY_DONE', 20));
        return;
      }
      if (managerStep === 4) {
        sendSSE(response, buildToolCallChunks('call_read_err_1', 'js-manager', JSON.stringify({
          program: 'CANARY_CONTROLLED_THROW',
          contract: CONTRACT_TOKEN,
        }), 25));
        return;
      }
      if (managerStep === 5) {
        sendSSE(response, buildTextChunks('resp_err_done', 'CANARY_ERROR_DONE', 30));
        return;
      }
      if (managerStep === 6) {
        sendSSE(response, buildToolCallChunks('call_js_cancel_1', 'js-manager', JSON.stringify({
          program: 'CANARY_CONTROLLED_ABORT',
          contract: CONTRACT_TOKEN,
        }), 35));
        return;
      }
      sendSSE(response, buildTextChunks(`resp_step_${managerStep}`, 'CANARY_OK', 40));
    });

    scenarioDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-manager-review-canary-'));
    const workspace = path.join(scenarioDir, 'workspace');
    fs.mkdirSync(workspace, { recursive: true });
    fs.writeFileSync(path.join(workspace, 'fixture-sample.txt'), 'Hello Wanxiangshu Manager Review Tools Canary\nLine 2: sample text\n', 'utf8');
    await initGitWorkspace(workspace);
    await host.start({
      scenarioDir,
      providerUrl: `${provider.url}/v1`,
      pluginPaths: [canaryPluginPath],
      detached: false,
      extraEnv: {
        WANXIANGSHU_REVIEW_TOOLS_CANARY_COLLECTOR: collectorUrl,
        WANXIANGSHU_REVIEW_TOOLS_PRODUCTION_PLUGIN: productionPluginPath,
      },
    });
    const created = await request(host.baseUrl, 'POST', '/api/session', {
      agent: 'manager', model: { providerID: 'test', id: 'test-model' },
    }, 200);
    sessionID = created.data?.data?.data?.id ?? created.data?.data?.id ?? created.data?.id;
    assert.ok(sessionID, 'session creation failed to return sessionID');
    await verify.ready({ sessionID, pid: host.pid, health: await request(host.baseUrl, 'GET', '/global/health', undefined, 200) });

    await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt('msg_canary_prompt_1', 'READ_SAMPLE'), 204);
    const normalBefore = await observeCall('tool.execute.before.observed', 'call_read_norm_1');
    const normalAfter = await observeCall('tool.execute.after.observed', 'call_read_norm_1');
    const normalTerminal = await observeCall('tool.terminal.observed', 'call_read_norm_1');
    const normal = callEvidence(normalAfter.value, normalTerminal);
    await verify.normal({ before: normalBefore.value, after: normalAfter.value, call: normal });
    await waitFor(({ kind, value, sequence }) => kind === 'session.idle.observed'
      && value.sessionID === sessionID && sequence > normalBefore.sequence);

    const historyStart = observations.length;
    await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt('msg_canary_prompt_2', 'VERIFY_STABILITY'), 204);
    await waitFor(({ kind }) => kind === 'manager.stability.done');
    await verify.history({ historicalToolCallPreservesContract: wireInspection.historicalToolCallPreservesContract });
    await waitFor(({ kind, value, sequence }) => kind === 'session.idle.observed'
      && value.sessionID === sessionID && sequence > historyStart);

    await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt('msg_canary_prompt_3', 'TRIGGER_ERROR'), 204);
    const errorBefore = await observeCall('tool.execute.before.observed', 'call_read_err_1');
    const errorSettled = await observeCall('tool.executor.settled', 'call_read_err_1');
    const errorTerminal = await observeCall('tool.terminal.observed', 'call_read_err_1');
    const executorError = callEvidence(errorSettled.value, errorTerminal);
    await verify.executorError({ before: errorBefore.value, settled: errorSettled.value, call: executorError });
    await waitFor(({ kind, value, sequence }) => kind === 'session.idle.observed'
      && value.sessionID === sessionID && sequence > errorBefore.sequence);

    await request(host.baseUrl, 'POST', `/session/${sessionID}/prompt_async`, prompt('msg_canary_prompt_4', 'TRIGGER_CANCEL'), 204);
    const cancelBefore = await observeCall('tool.execute.before.observed', 'call_js_cancel_1');
    const cancelExecuting = await observeCall('tool.executor.entered', 'call_js_cancel_1');
    const cancelRunning = await observeCall('tool.running.observed', 'call_js_cancel_1');
    assert.equal(observations.some(({ kind, value }) => kind === 'tool.terminal.observed' && value?.callID === 'call_js_cancel_1'), false,
      'cancellation requires a live call, not one already failed');
    await verify.cancelRunning({ before: cancelBefore.value, executing: cancelExecuting.value, running: cancelRunning, observations: structuredClone(observations) });
    const observationsBeforeAbort = observations.length;
    await request(host.baseUrl, 'POST', `/session/${sessionID}/abort`, {}, [200, 204]);
    const cancelSettled = await observeCall('tool.executor.settled', 'call_js_cancel_1');
    const cancelTerminal = await observeCall('tool.terminal.observed', 'call_js_cancel_1');
    assert.ok(cancelTerminal.sequence > observationsBeforeAbort);
    assert.ok(cancelTerminal.sequence > cancelRunning.sequence);
    const cancellation = callEvidence(cancelSettled.value, cancelTerminal);
    await verify.cancellation({ before: cancelBefore.value, settled: cancelSettled.value, call: cancellation });

    const definition = observations.find(({ kind, value }) => kind === 'tool.definition.observed' && value?.toolName === 'js-manager');
    assert.ok(definition, 'missing definition observation for review tool js-manager');
    assert.equal(definition.value.hasContractProperty, true, 'js-manager definition missing contract property');
    assert.equal(normalBefore.value.argsIdentityPreserved, true, 'before must preserve args reference identity');
    assert.equal(normalBefore.value.preContractInArgs, true, 'pre-before args must carry contract');
    assert.equal(normalBefore.value.postContractInArgs, true, 'before must preserve the public input before Host persistence');
    assert.equal(normalBefore.value.businessKeysPreserved, true, 'post-before must preserve business args');
    assert.equal(normalAfter.value.identityWithBefore, true, 'after must receive same args reference as before');
    assert.equal(normalAfter.value.postAfterContractInArgs, true, 'post-after must restore contract on args');
    assert.equal(normalAfter.value.postAfterContractValue, CONTRACT_TOKEN, 'restored contract value mismatch');
    assert.equal(errorBefore.value.postContractInArgs, true, 'error before must preserve public input');
    assert.equal(errorSettled.value.contractRestored, true, 'executor rejection must restore contract');
    assert.equal(errorSettled.value.entered.contractHidden, true, 'the actual controlled executor must not receive contract');
    assert.equal(errorSettled.value.originalError, true, 'the controlled execution error must propagate unchanged');
    assert.equal(cancelBefore.value.postContractInArgs, true, 'cancel before must preserve public input');
    assert.equal(cancelSettled.value.contractRestored, true, 'actual executor cancellation must restore contract');
    assert.equal(cancelSettled.value.entered.abortObserved, true, 'the real public AbortSignal must reject the controlled executor');
    assert.equal(wireInspection.historicalToolCallPreservesContract, true, 'round 2 provider request must preserve contract in historical tool_calls');
    const calls = { normal, executorError, cancellation };
    for (const [name, call] of Object.entries(calls)) {
      assert.equal(call.sameArguments, true, name);
      assert.equal(call.originalOrder, true, name);
      assert.equal(call.originalValues, true, name);
      assert.equal(call.durableInputRetainsContract, true, name);
      assert.equal(call.originalDurableInput, true, name);
    }
    assert.equal(calls.normal.status, 'completed');
    assert.equal(calls.executorError.status, 'error');
    assert.equal(calls.cancellation.status, 'error');
    evidence = {
      schemaVersion: 1,
      versions,
      executionKinds: { normal: 'production-js', executorError: 'controlled-throw', cancellation: 'controlled-abort-rejection' },
      calls,
      historicalToolCallPreservesContract: wireInspection.historicalToolCallPreservesContract,
      observationsCount: observations.length,
      providerRequestsCount: providerRequests.length,
    };
  } catch (error) {
    failures.push(error);
    console.error('[run-manager-review-tools-canary] observed failure:', JSON.stringify({
      message: error.message, managerStep, observations, providerRequestsCount: providerRequests.length,
    }));
  } finally {
    if (sessionID && host.baseUrl) {
      try { await request(host.baseUrl, 'POST', `/session/${sessionID}/abort`, {}, [200, 204]); }
      catch (error) { failures.push(error); }
    }
    try { await host.stop(); } catch (error) { failures.push(error); }
    for (const result of await Promise.allSettled([stopHttpServer(provider?.server), stopHttpServer(collector)])) {
      if (result.status === 'rejected') failures.push(result.reason);
    }
    try { if (scenarioDir) fs.rmSync(scenarioDir, { recursive: true, force: true }); }
    catch (error) { failures.push(error); }
  }
  if (failures.length > 1) throw new AggregateError(failures, 'Manager review canary and cleanup failed', { cause: failures[0] });
  if (failures.length === 1) throw failures[0];
  return evidence;
}
