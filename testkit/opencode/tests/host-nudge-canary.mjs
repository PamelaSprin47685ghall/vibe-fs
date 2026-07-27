import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);
const managerTools = ['fork', 'join', 'list'];
const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict'];

/**
 * Busy-overlap E2E — fully event-driven (no sleep, delayDone, or casual waiting):
 *
 * 1. manager forks coder
 * 2. coder stream matches and neverEnds → activeRequestCount >= 1
 * 3. manager forks same agent id (nudge) while hang still open
 * 4. manager finishes
 * 5. parent abort closes neverEnd streams (same teardown as host-abort)
 *
 * OpenCode may reject a second provider request while the child is busy; that is
 * host behavior. Production busy-nudge is fire-and-forget without a second Run
 * (unit-tested). This canary proves the tool surface issues the overlap nudge
 * while the first stream is still open.
 */
function extractAgentIdFromMessages(body) {
  const messages = body?.messages || [];
  for (let i = messages.length - 1; i >= 0; i -= 1) {
    const msg = messages[i];
    const content = typeof msg?.content === 'string' ? msg.content : JSON.stringify(msg?.content || '');
    const match = content.match(/"agentId"\s*:\s*"([^"]+)"/) || content.match(/agentId[=:]\s*([A-Za-z0-9_-]+)/);
    if (match) return match[1];
  }
  return null;
}



let scenario;
try {
  if (!runStaticGate([__filename]).passed) throw new Error('host nudge canary contains prohibited polling');
  scenario = await setupScenario({ project: { files: {} }, strict: true });

  scenario.provider.expectTitle({
    id: 'parent-title',
    lane: expectationLane('host-nudge', 'parent-title', 'title', 1, 'title'),
  });

  scenario.provider.expectToolCall({
    id: 'manager-fork-busy',
    lane: expectationLane('host-nudge', 'manager', 'manager', 1),
    tool: 'fork',
    args: { agent: 'coder', prompt: 'first busy run: stay streaming until nudged' },
    match: { requiredTools: managerTools, forbiddenTools: forbiddenManagerTools },
  });

  scenario.provider.expectText({
    id: 'manager-blogger',
    lane: expectationLane('host-nudge', 'manager-blogger', 'blogger', 1, 'chat', 'manager'),
    blocking: false,
    neverEnd: true,
    text: 'Manager background remains busy.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"manager"'] },
  });

  scenario.provider.expectText({
    id: 'coder-blogger',
    lane: expectationLane('host-nudge', 'coder-blogger', 'blogger', 1, 'chat', 'coder'),
    blocking: false,
    neverEnd: true,
    text: 'Coder background remains busy.',
    match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"coder"'] },
  });

  // Causal busy barrier: match + keep stream open. No timers.
  scenario.provider.expectText({
    id: 'coder-busy-hang',
    lane: expectationLane('host-nudge', 'coder', 'coder', 1, 'chat', 'manager'),
    text: 'Coder first run still working.',
    blocking: false,
    neverEnd: true,
    match: { requiredTools: ['write'] },
  });

  scenario.provider.expectToolCall({
    id: 'manager-fork-nudge',
    lane: expectationLane('host-nudge', 'manager', 'manager', 2),
    tool: 'fork',
    args: (parsed) => {
      const agentId = extractAgentIdFromMessages(parsed) || 'coder';
      return { agent: agentId, prompt: 'nudge continue second run while first is busy' };
    },
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
      parts: [{
        type: 'text',
        text: [
          'Fork a coder with prompt exactly: first busy run: stay streaming until nudged.',
          'After the fork tool returns, while the coder is still busy, immediately fork the same agent id',
          'with prompt exactly: nudge continue second run while first is busy.',
          'Do not join a hanging child.',
        ].join(' '),
      }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `manager prompt failed: ${JSON.stringify(prompt.data)}`);

  await scenario.provider.waitForExpectation('manager-fork-busy', WATCHDOG_TIMEOUT_MS);
  const childCreated = await scenario.events.awaitEvent(
    (event) => event.type === 'session.created'
      && event.parentSessionID === parentId
      && event.sessionAgent === 'coder',
    WATCHDOG_TIMEOUT_MS,
  );
  const childId = childCreated.sessionID;
  assert.ok(childId, 'manager fork must create a coder child');
  scenario.sessionIds.push(childId);
  bindLaneSession(scenario.provider, childId, 'coder');
  scenario.watchdog?.advance({ reason: 'coder-child-created', lane: `session:${childId}`, blocking: true });

  await scenario.provider.waitForExpectation('coder-busy-hang', WATCHDOG_TIMEOUT_MS);
  assert.ok(
    scenario.provider.activeRequestCount >= 1,
    'coder first run must still be streaming when overlap begins',
  );
  scenario.watchdog?.advance({ reason: 'coder-busy-hang-observed', lane: 'coder', blocking: true });

  // Capture sequence before nudge expectation
  const nudgeSeq = scenario.events.lastSeq;
  await scenario.provider.waitForExpectation('manager-fork-nudge', WATCHDOG_TIMEOUT_MS);
  scenario.watchdog?.advance({ reason: 'manager-busy-nudge-issued', lane: 'manager', blocking: true });

  assert.ok(
    scenario.provider.activeRequestCount >= 1,
    'first coder stream must still be active when the busy nudge fork is issued',
  );

  // Wait for the fork tool result message.updated event in the parent session
  await scenario.events.awaitEvent(
    (e) => e.seq > nudgeSeq && e.sessionID === parentId && e.type === 'message.updated',
    WATCHDOG_TIMEOUT_MS,
  );
  scenario.watchdog?.advance({ reason: 'manager-nudge-completed', lane: 'manager', blocking: true });

  // The canary does not prove Manager normal completion; it proves the
  // overlap nudge while the first stream is active. Abort the Manager now to
  // terminate its ReviewGuard loop, then verify both sessions clean up.
  const abort = await scenario.client.abort(parentId);
  assert.ok(abort.ok, `parent abort failed: ${JSON.stringify(abort.data)}`);

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Host nudge canary passed: busy-overlap nudge while first run still streaming.');
} catch (error) {
  console.error(`Host nudge canary failed: ${error.stack || error}`);
  if (scenario?.events) {
    console.error('Terminal debug:', scenario.events._events.slice(-15).map((e) => `#${e.seq}:${e.type}:${e.sessionID}:${e.status}`).join(' | '));
  }
  if (scenario?.host?.workDir) console.error(`workDir: ${scenario.host.workDir}`);
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
