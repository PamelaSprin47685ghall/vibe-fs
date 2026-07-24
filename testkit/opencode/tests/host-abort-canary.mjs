import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { getSessionId, runStaticGate, setupScenario, teardownScenario } from '../index.js';

const __filename = fileURLToPath(import.meta.url);
const managerTools = ['fork', 'join', 'list'];
const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'];

function findValue(value, key) {
  if (!value || typeof value !== 'object') return null;
  if (Array.isArray(value) && value[0] === key && typeof value[1] === 'string') return value[1];
  if (typeof value[key] === 'string') return value[key];
  for (const child of Array.isArray(value) ? value : Object.values(value)) {
    const found = findValue(child, key);
    if (found) return found;
  }
  return null;
}

function childrenOf(response) {
  return Array.isArray(response.data) ? response.data : response.data?.data || [];
}

function journalValue(workDir, key) {
  const runtimeDir = path.join(workDir, '.wanxiangshu-next', 'runtimes');
  for (const file of fs.readdirSync(runtimeDir)) {
    if (!file.endsWith('.ndjson')) continue;
    const lines = fs.readFileSync(path.join(runtimeDir, file), 'utf8').split('\n');
    for (const line of lines) {
      if (!line.trim()) continue;
      const found = findValue(JSON.parse(line), key);
      if (found) return found;
    }
  }
  return null;
}

function isTerminalFor(sessionID) {
  return (e) => {
    const es = e.sessionID ?? e.properties?.sessionID;
    if (es !== sessionID) return false;
    if (e.type === 'session.idle' || e.type === 'session.aborted') return true;
    if (e.type === 'session.status') {
      const s = e.status ?? e.properties?.status;
      return s === 'idle' || s?.type === 'idle' || s?.status === 'idle';
    }
    return false;
  };
}

async function waitForProvider(predicate, label, timeoutMs) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (predicate()) return;
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
  throw new Error(`provider wait timed out: ${label}`);
}

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({ project: { files: { 'AGENTS.md': 'parent abort canary\n' } }, strict: true, watchdogMs: 30000 });
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowBloggerRequests();
  scenario.provider.allowOutOfOrder();

  scenario.provider.expectToolCall({
    id: 'manager-fork',
    tool: 'fork',
    args: { agent: 'coder', prompt: 'Work forever on abort-task.' },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  scenario.provider.expectText({
    id: 'child-long',
    text: 'child stream that never finishes',
    neverEnd: true,
    match: { requiredTools: ['write'] },
  });
  scenario.provider.expectText({
    id: 'manager-long',
    text: 'manager stream that never finishes',
    neverEnd: true,
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  const parent = await scenario.client.createSession();
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);

  const prompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Fork the coder and keep working.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `manager prompt failed: ${JSON.stringify(prompt.data)}`);

  // Both manager and child must be mid-stream before the abort.
  await waitForProvider(() => scenario.provider.remainingExpectations === 0, 'all turns in flight', 20000);
  assert.equal(scenario.provider.activeRequestCount, 2, 'manager and child streams must both hang');

  const children = await scenario.client.request('GET', `/session/${parentId}/children`);
  const childId = getSessionId(childrenOf(children)[0]) || journalValue(scenario.host.workDir, 'ChildId');
  assert.ok(childId, `child session was not recoverable: ${JSON.stringify(children.data)}`);
  scenario.sessionIds.push(childId);

  const watermark = scenario.events.lastSeq;
  const abort = await scenario.client.abort(parentId);
  assert.ok(abort.ok, `parent abort failed: ${JSON.stringify(abort.data)}`);

  await scenario.events.awaitEvent(
    (e) => e.seq > watermark && isTerminalFor(childId)(e),
    15000,
  );
  await scenario.events.awaitEvent(
    (e) => e.seq > watermark && isTerminalFor(parentId)(e),
    15000,
  );
  await waitForProvider(() => scenario.provider.activeRequestCount === 0, 'hanging streams aborted', 15000);

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Host abort canary passed: parent abort propagated to the busy child and closed both streams.');
} catch (error) {
  console.error(`Host abort canary failed: ${error.stack || error}`);
  if (scenario?.host?.stderrLog) console.error(`── host stderr tail ──\n${scenario.host.stderrLog.slice(-30000)}`);
  if (scenario?.events) scenario.events.dumpOnFailure();
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
