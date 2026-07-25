import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { getSessionId, runStaticGate, setupScenario, teardownScenario } from '../index.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);
const primaryRole = 'orchestrator';
const primaryTools = ['fork', 'join'];
const forbiddenPrimaryTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'list', 'verdict'];
const contextLimit = 1000;
// Activation: estimateTokens >= 0.8 * 1000 = 800 tokens = 3200 chars/4.
const longText = 'dense work record sentence. '.repeat(70); // ~1960 chars per round
const rounds = 4;

function journalContains(workDir, needle) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(path.isAbsolute(common) ? common : path.resolve(workDir, common), 'wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(runtimeDir)) return false;
  for (const file of fs.readdirSync(runtimeDir)) {
    if (!file.endsWith('.ndjson')) continue;
    if (fs.readFileSync(path.join(runtimeDir, file), 'utf8').includes(needle)) return true;
  }
  return false;
}

function primaryRequests(scenario) {
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
    watchdogMs: 1000,
  });
  scenario.provider.expectTitle({
    id: 'primary-title',
    lane: expectationLane('companion-replacement', 'primary-title', 'title', 1, 'title'),
  });

  const parent = await scenario.client.request('POST', '/api/session', {
    body: { agent: primaryRole, model: { providerID: 'test', id: 'test-model' } },
  });
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);
  bindLaneSession(scenario.provider, parentId, 'primary-title', 'primary');

  for (let round = 1; round <= rounds; round++) {
    scenario.provider.expectText({
      id: `round-${round}`,
      lane: expectationLane('companion-replacement', 'primary', primaryRole, round),
      text: `round ${round}: ${longText}`,
      match: { requiredTools: primaryTools, forbiddenTools: forbiddenPrimaryTools },
    });
    if (round <= 2) {
      scenario.provider.expectText({
        id: `manager-blogger-${round}`,
        lane: expectationLane('companion-replacement', 'primary-blogger', 'blogger', round, 'chat', 'primary'),
        text: `Blogger paragraph ${round}.`,
        match: {
          containsText: ['You are the blogger of a coding agent session.', '"agent":"orchestrator"'],
        },
      });
    }
    if (round === 3) {
      scenario.provider.expectText({
        id: 'manager-blogger-3',
        lane: expectationLane('companion-replacement', 'primary-blogger', 'blogger', 3, 'chat', 'primary'),
        neverEnd: true,
        blocking: false,
        text: 'Blogger replacement background remains busy.',
        match: {
          containsText: ['You are the blogger of a coding agent session.', '"agent":"orchestrator"'],
        },
      });
    }
    const turn = scenario.turn.start(parentId);
    const prompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
      body: {
        agent: primaryRole,
        parts: [{ type: 'text', text: `Record round ${round}.` }],
        model: { providerID: 'test', modelID: 'test-model' },
      },
    });
    assert.ok(prompt.ok, `round ${round} prompt failed: ${JSON.stringify(prompt.data)}`);
    await turn.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });
    if (round <= 2) {
      await scenario.provider.waitForExpectation(`manager-blogger-${round}`, 1000);
      await scenario.provider.waitForIdle(1000);
    }
    if (round === 3) {
      await scenario.provider.waitForExpectation('manager-blogger-3', 1000);
      scenario.watchdog?.advance({
        reason: 'replacement-blogger-busy',
        lane: 'manager-blogger:3',
        blocking: true,
      });
    }
  }

  assert.ok(
    journalContains(scenario.host.workDir, 'CompanionReplacementActiveSet'),
    'journal must record the durable PrefixReplacementEnabled fact',
  );
  assert.ok(
    journalContains(scenario.host.workDir, 'CompanionAdvanced'),
    'each successful Blogger checkpoint must atomically persist its B and baseline',
  );

  await scenario.restart();
  scenario.provider.expectText({
    id: 'round-restarted',
    lane: expectationLane('companion-replacement', 'primary', primaryRole, 5),
    text: `round restarted: ${longText}`,
    match: { requiredTools: primaryTools, forbiddenTools: forbiddenPrimaryTools },
  });
  scenario.provider.expectText({
    id: 'manager-blogger-restarted',
    lane: expectationLane('companion-replacement', 'primary-blogger-restarted', 'blogger', 1, 'chat', 'primary'),
    neverEnd: true,
    blocking: false,
    text: 'Blogger restart background remains busy.',
    match: {
      containsText: ['You are the blogger of a coding agent session.', '"agent":"orchestrator"'],
    },
  });

  const restartedTurn = scenario.turn.start(parentId);
  const restartedPrompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: primaryRole,
      parts: [{ type: 'text', text: 'Record round restarted.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(restartedPrompt.ok, `restarted round failed: ${JSON.stringify(restartedPrompt.data)}`);
  await restartedTurn.awaitTerminal({ timeoutMs: 1000, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });
  await scenario.provider.waitForExpectation('manager-blogger-restarted', 1000);
  scenario.watchdog?.advance({
    reason: 'replacement-blogger-restarted-busy',
    lane: 'manager-blogger-restarted:1',
    blocking: true,
  });

  const requests = primaryRequests(scenario);
  assert.ok(requests.length >= rounds + 1, `expected primary requests, got ${requests.length}`);
  const last = requests[requests.length - 1];
  const bIndex = last.messages.findIndex((m) => messageText(m).includes('Blogger paragraph'));
  assert.ok(bIndex >= 0, `replaced projection must carry the current B: ${JSON.stringify(last.messages.map(messageRole))}`);
  assert.equal(messageRole(last.messages[bIndex]), 'user', 'the B head travels as a user-role synthetic');
  assert.ok(
    messageText(last.messages[bIndex]).includes('Blogger paragraph 1.')
      && messageText(last.messages[bIndex]).includes('Blogger paragraph 2.'),
    'restarted projection must restore the complete accumulated B',
  );
  const lastUser = last.messages[last.messages.length - 1];
  assert.ok(
    messageText(lastUser).includes('Record round restarted.'),
    'uncovered raw tail must be preserved verbatim',
  );
  assert.ok(
   last.messages.length < rounds * 2 + 3,
    `covered prefix must be skipped, got ${last.messages.length} messages after restart`,
  );

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Companion replacement canary passed: real budget activated atomic B persistence and restart-safe prefix replacement.');
} catch (error) {
  console.error(`Companion replacement canary failed: ${error.stack || error}`);
  if (scenario?.host?.stderrLog) console.error(`── host stderr tail ──\n${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
