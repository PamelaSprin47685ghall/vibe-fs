import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { getSessionId, runStaticGate, setupScenario, teardownScenario } from '../index.js';

const __filename = fileURLToPath(import.meta.url);
const managerTools = ['fork', 'join', 'list'];
const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'];
const contextLimit = 4000;
// Activation: estimateTokens >= 0.8 * 4000 = 3200 tokens = 12800 chars/4.
const longText = 'dense work record sentence. '.repeat(80); // ~2240 chars per round
const rounds = 8;

function journalContains(workDir, needle) {
  const runtimeDir = path.join(workDir, '.wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(runtimeDir)) return false;
  for (const file of fs.readdirSync(runtimeDir)) {
    if (!file.endsWith('.ndjson')) continue;
    if (fs.readFileSync(path.join(runtimeDir, file), 'utf8').includes(needle)) return true;
  }
  return false;
}

function managerRequests(scenario) {
  return scenario.provider.requests.filter((body) =>
    (body.tools || []).some((t) => (t?.function?.name || t?.name) === 'fork'));
}

function messageRole(message) {
  return message?.role || message?.info?.role;
}

function messageText(message) {
  const content = message?.content ?? message?.text ?? '';
  if (typeof content === 'string') return content;
  if (Array.isArray(content)) return content.map((p) => p?.text || '').join('\n');
  return JSON.stringify(content);
}

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'companion replacement canary\n' } },
    strict: true,
    contextLimit,
    watchdogMs: 30000,
  });
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowBloggerRequests();
  scenario.provider.allowOutOfOrder();

  const parent = await scenario.client.createSession();
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);

  for (let round = 1; round <= rounds; round++) {
    scenario.provider.expectText({
      id: `round-${round}`,
      text: `round ${round}: ${longText}`,
      match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
    });
    const turn = scenario.turn.start(parentId);
    const prompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
      body: {
        agent: 'manager',
        parts: [{ type: 'text', text: `Record round ${round}.` }],
        model: { providerID: 'test', modelID: 'test-model' },
      },
    });
    assert.ok(prompt.ok, `round ${round} prompt failed: ${JSON.stringify(prompt.data)}`);
    await turn.awaitTerminal({ timeoutMs: 30000, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });
  }

  assert.ok(
    journalContains(scenario.host.workDir, 'CompanionReplacementActiveSet'),
    'journal must record the durable PrefixReplacementEnabled fact',
  );

  const requests = managerRequests(scenario);
  assert.ok(requests.length >= rounds - 1, `expected manager requests, got ${requests.length}`);
  const last = requests[requests.length - 1];
  const bIndex = last.messages.findIndex((m) => messageText(m).includes('Blogger paragraph'));
  assert.ok(bIndex >= 0, `replaced projection must carry the current B: ${JSON.stringify(last.messages.map(messageRole))}`);
  assert.equal(messageRole(last.messages[bIndex]), 'user', 'the B head travels as a user-role synthetic');
  const lastUser = last.messages[last.messages.length - 1];
  assert.ok(
    messageText(lastUser).includes(`Record round ${rounds}.`),
    'uncovered raw tail must be preserved verbatim',
  );
  assert.ok(
    last.messages.length < rounds + 2,
    `covered prefix must be skipped, got ${last.messages.length} messages after ${rounds} rounds`,
  );

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Companion replacement canary passed: real budget activated durable prefix replacement.');
} catch (error) {
  console.error(`Companion replacement canary failed: ${error.stack || error}`);
  if (scenario?.host?.stderrLog) console.error(`── host stderr tail ──\n${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
