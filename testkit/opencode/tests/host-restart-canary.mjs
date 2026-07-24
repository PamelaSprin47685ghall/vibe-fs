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

function childrenOf(response) {
  return Array.isArray(response.data) ? response.data : response.data?.data || [];
}

function childWriteExpectations(scenario, marker) {
  scenario.provider.expectToolCall({
    id: `${marker}-write`,
    tool: 'write',
    args: { filePath: `restart-${marker}.txt`, content: `${marker}.` },
    match: { requiredTools: ['write'] },
  });
  scenario.provider.expectText({ id: marker, text: `${marker} terminal.`, match: { requiredTools: ['write'] } });
}

async function nudge(parentId, childId, agentId, marker, managerMarker, scenario) {
  scenario.provider.expectToolCall({
    id: `${marker}-nudge`,
    tool: 'fork',
    args: { agent: agentId, prompt: `Write restart-${marker}.txt with exactly ${marker}.` },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  childWriteExpectations(scenario, marker);
  scenario.provider.expectText({
    id: managerMarker,
    text: `${managerMarker} complete.`,
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  const childTurn = scenario.turn.start(childId);
  const parentTurn = scenario.turn.start(parentId);
  const response = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: `Nudge ${agentId} for ${marker}.` }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(response.ok, `manager nudge failed: ${JSON.stringify(response.data)}`);
  await Promise.all([
    childTurn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true }),
    parentTurn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true }),
  ]);
}

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({ project: { files: { 'AGENTS.md': 'restart reconcile canary\n' } }, strict: true, watchdogMs: 30000 });
  scenario.provider.allowSyntheticContinuations();
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowBloggerRequests();
  scenario.provider.allowOutOfOrder();

  scenario.provider.expectToolCall({
    id: 'manager-fork-coder',
    tool: 'fork',
    args: { agent: 'coder', prompt: 'Write restart-child-a1.txt with exactly child-a1.' },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });
  childWriteExpectations(scenario, 'child-a1');
  scenario.provider.expectText({
    id: 'manager-created',
    text: 'manager-created complete.',
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  const parent = await scenario.client.createSession();
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);
  const firstTurn = scenario.turn.start(parentId);
  const firstPrompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{ type: 'text', text: 'Create the child and report manager-created.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(firstPrompt.ok, `manager create failed: ${JSON.stringify(firstPrompt.data)}`);
  await firstTurn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });

  const messages = await scenario.client.messages(parentId);
  const messageJson = JSON.stringify(messages.data);
  const agentId = findValue(messages.data, 'agentId') || messageJson.match(/agentId[^a-z0-9]+([a-z0-9]{6})/i)?.[1];
  assert.ok(agentId, `fork result did not expose agentId: ${messageJson}`);
  const children = await scenario.client.request('GET', `/session/${parentId}/children`);
  const childId = getSessionId(childrenOf(children)[0]) || journalValue(scenario.host.workDir, 'ChildId');
  assert.ok(childId, `child session was not recoverable: ${JSON.stringify(children.data)}`);
  scenario.sessionIds.push(childId);

  // The restart must not race the child's first turn: await its terminal
  // and the written file, or the interrupted continuation silently
  // consumes later rounds' expectations after the restart.
  await scenario.turn.start(childId).awaitTerminal({
    timeoutMs: 30000,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });
  scenario.fs.expectFileContent('restart-child-a1.txt', 'child-a1.');

  await scenario.restart();
  await nudge(parentId, childId, agentId, 'child-a2', 'manager-a2', scenario);
  await nudge(parentId, childId, agentId, 'child-a3', 'manager-a3', scenario);

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Host restart reconcile canary passed: restored child retained coder tools across two nudges.');
} catch (error) {
  console.error(`Host restart reconcile canary failed: ${error.stack || error}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
