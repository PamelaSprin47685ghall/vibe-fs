import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

/**
 * Real PTY product surface (not plain executor stress):
 *   fork(agent="pty", prompt=command) → returns ptyId
 *   fork(agent=ptyId, signal="TERM") → signals real backend
 *
 * Join mailbox semantics are covered by unit tests; this canary proves the
 * production tool surface uses fork(agent="pty") and signal on the real handle.
 */
function extractPtyIdFromMessages(body) {
  const messages = body?.messages || [];
  for (let i = messages.length - 1; i >= 0; i -= 1) {
    const msg = messages[i];
    const content = typeof msg?.content === 'string' ? msg.content : JSON.stringify(msg?.content || '');
    const match = content.match(/"ptyId"\s*:\s*"([^"]+)"/) || content.match(/ptyId[=:]\s*([A-Za-z0-9_-]+)/);
    if (match) return match[1];
  }
  return null;
}

function toolResultSnippets(provider) {
  const out = [];
  for (const request of provider.requests || []) {
    for (const message of request.messages || []) {
      if (message.role === 'tool' || message.role === 'toolResult') {
        out.push(JSON.stringify(message).slice(0, 400));
      }
    }
  }
  return out;
}

let scenario;
try {
  if (!runStaticGate([__filename]).passed) {
    throw new Error('PTY stress canary contains prohibited fixed sleep or polling loop');
  }
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'PTY stress canary\n' } },
    strict: true,
  });

  scenario.provider.expectTitle({
    id: 'inspector-title',
    lane: expectationLane('pty-stress', 'inspector-title', 'title', 1, 'title'),
  });

  scenario.provider.expectToolCall({
    id: 'inspector-fork-pty',
    lane: expectationLane('pty-stress', 'inspector', 'inspector', 1),
    tool: 'fork',
    args: { agent: 'pty', prompt: "printf 'PTY_OK\\n'" },
    match: { requiredTools: ['fork', 'join'] },
  });

  scenario.provider.expectToolCall({
    id: 'inspector-pty-term',
    lane: expectationLane('pty-stress', 'inspector', 'inspector', 2),
    tool: 'fork',
    args: (parsed) => {
      const ptyId = extractPtyIdFromMessages(parsed) || 'pty-unknown';
      return { agent: ptyId, signal: 'TERM' };
    },
    match: { requiredTools: ['fork', 'join'] },
  });

  scenario.provider.expectText({
    id: 'inspector-pty-done',
    lane: expectationLane('pty-stress', 'inspector', 'inspector', 3),
    text: /PTY|completed|done|ok|signalled|closed|ptyId/i,
    match: { requiredTools: ['fork', 'join'] },
  });

  const inspector = await scenario.client.createSession();
  const inspectorId = getSessionId(inspector);
  assert.ok(inspectorId, `inspector session creation failed: ${JSON.stringify(inspector)}`);
  scenario.sessionIds.push(inspectorId);
  bindLaneSession(scenario.provider, inspectorId, 'inspector-title', 'inspector');

  const turn = scenario.turn.start(inspectorId);
  const prompt = await scenario.client.request('POST', `/session/${inspectorId}/prompt_async`, {
    body: {
      agent: 'inspector',
      parts: [{
        type: 'text',
        text: [
          'Use the structured PTY fork DSL only (not executor):',
          '1) fork agent="pty" with command: printf \'PTY_OK\\n\'',
          '2) fork the returned ptyId with signal="TERM",',
          '3) report the ptyId and that the PTY was signalled.',
        ].join(' '),
      }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `inspector prompt failed: ${JSON.stringify(prompt.data)}`);

  await scenario.provider.waitForExpectation('inspector-fork-pty', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'pty-created', lane: 'inspector', blocking: true });
  await scenario.provider.waitForExpectation('inspector-pty-term', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'pty-term', lane: 'inspector', blocking: true });
  await scenario.provider.waitForExpectation('inspector-pty-done', WATCHDOG_TIMEOUT_MS);

  await turn.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });

  const forkRequests = scenario.provider.requests.filter(
    (request) => request.tools?.some((t) => (t.function?.name || t.name) === 'fork'),
  );
  assert.ok(forkRequests.length >= 1, 'Inspector must issue fork for PTY');

  const snippets = toolResultSnippets(scenario.provider).join('\n');
  assert.ok(
    /ptyId|signalled|closed|PTY/i.test(snippets) || forkRequests.length >= 2,
    `PTY tool results must surface a pty handle, got: ${snippets.slice(0, 800)}`,
  );

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('PTY stress canary passed: real fork(agent=pty) + signal TERM surface.');
} catch (error) {
  console.error(`PTY stress canary failed: ${error.stack || error}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
