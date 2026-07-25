import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { getSessionId, runStaticGate, setupScenario, teardownScenario } from '../index.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

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
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(path.isAbsolute(common) ? common : path.resolve(workDir, common), 'wanxiangshu-next', 'runtimes');
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

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({ project: { files: { 'AGENTS.md': 'parent abort canary\n' } }, strict: true, watchdogMs: 1000 });
  scenario.provider.expectTitle({
    id: 'parent-title',
    lane: expectationLane('host-abort', 'parent-title', 'title', 1, 'title'),
  });

  scenario.provider.expectToolCall({
    id: 'manager-fork',
    lane: expectationLane('host-abort', 'manager', 'manager', 1),
    tool: 'fork',
    args: { agent: 'coder', prompt: 'Work forever on abort-task.' },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  scenario.provider.expectText({
    id: 'manager-blogger',
    lane: expectationLane('host-abort', 'manager-blogger', 'blogger', 1, 'chat', 'manager'),
    blocking: false,
    text: 'Manager abort background.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"manager"'] },
  });
  scenario.provider.expectText({
    id: 'child-long',
    lane: expectationLane('host-abort', 'coder', 'coder', 1, 'chat', 'manager'),
    text: 'child stream that never finishes',
    neverEnd: true,
    match: { requiredTools: ['write'] },
  });

  scenario.provider.expectText({
    id: 'manager-long',
    lane: expectationLane('host-abort', 'manager', 'manager', 2),
    text: 'manager stream that never finishes',
    neverEnd: true,
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  const parent = await scenario.client.createSession();
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);
  bindLaneSession(scenario.provider, parentId, 'parent-title', 'manager');

  const prompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Fork the coder and keep working.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `manager prompt failed: ${JSON.stringify(prompt.data)}`);

  await Promise.all([
    scenario.provider.waitForExpectation('child-long', 1000),
    scenario.provider.waitForExpectation('manager-long', 1000),
  ]);
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
    1000,
  );
  await scenario.events.awaitEvent(
    (e) => e.seq > watermark && isTerminalFor(parentId)(e),
    1000,
  );
  await scenario.provider.waitForIdle(1000);

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
