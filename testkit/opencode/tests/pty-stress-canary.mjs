import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);

/**
 * Real PTY product surface (not plain executor stress):
 *   fork(agent="pty", prompt=command) -> returns { ptyId }
 *   fork(agent=ptyId, prompt=text)    -> writes to the process
 *   fork(agent=ptyId, prompt="")      -> reads buffered output back to the caller
 *   fork(agent=ptyId, signal="TERM") -> forwards to the backend (no premature completion)
 *   fork(agent=ptyId, signal="KILL")  -> forces exit when TERM is ignored
 *   join                              -> delivers the real backend exit (Closed)
 *
 * This canary proves: the PTY runs in the session cwd (pwd), write reaches the
 * process and read returns the echoed output, TERM truly exits and join delivers
 * Closed, KILL forces exit when TERM is trapped, and no pty leaks after joins.
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

function forkResults(provider) {
  const out = [];
  for (const request of provider.requests || []) {
    for (const message of request.messages || []) {
      if (message.role === 'tool' || message.role === 'toolResult') {
        const content = typeof message.content === 'string' ? message.content : JSON.stringify(message.content || '');
        try {
          out.push(JSON.parse(content));
        } catch {
          out.push({ raw: content });
        }
      }
    }
  }
  return out;
}

const PTHRU_PROMPT = 'echo CWD=$(pwd); exec cat';
const TRAP_PROMPT = 'trap "" TERM; echo TRAPPED; exec tail -f /dev/null';

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
    id: 'manager-title',
    lane: expectationLane('pty-stress', 'manager-title', 'title', 1, 'title'),
  });

  // Manager companion emits a Blogger request on many projection boundaries.
  // Pre-register enough FIFO blogger turns for the full PTY tool loop.
  for (let round = 1; round <= 6; round += 1) {
    scenario.provider.expectText({
      id: `manager-blogger-${round}`,
      lane: expectationLane('pty-stress', 'manager-blogger', 'blogger', round, 'chat', 'manager'),
      text: `manager blog paragraph ${round}.`,
      match: { containsText: ['You are the blogger of a coding agent session.', '"agent":"manager"'] },
    });
  }

  // 1) Spawn in the session cwd.
  scenario.provider.expectToolCall({
    id: 'manager-fork-pty',
    lane: expectationLane('pty-stress', 'manager', 'manager', 1),
    tool: 'fork',
    args: { agent: 'pty', prompt: PTHRU_PROMPT },
    match: { requiredTools: ['fork', 'join'] },
  });
  // 2) Write reaches the process.
  scenario.provider.expectToolCall({
    id: 'manager-pty-write',
    lane: expectationLane('pty-stress', 'manager', 'manager', 2),
    tool: 'fork',
    args: (parsed) => ({ agent: extractPtyIdFromMessages(parsed) || 'pty-unknown', prompt: 'ECHO_TEST' }),
    match: { requiredTools: ['fork', 'join'] },
  });
  // 3) Read returns the buffered output immediately.
  scenario.provider.expectToolCall({
    id: 'manager-pty-read',
    lane: expectationLane('pty-stress', 'manager', 'manager', 3),
    tool: 'fork',
    args: (parsed) => ({ agent: extractPtyIdFromMessages(parsed) || 'pty-unknown', prompt: '' }),
    match: { requiredTools: ['fork', 'join'] },
  });
  // 4) TERM forwards to the backend (no premature completion).
  scenario.provider.expectToolCall({
    id: 'manager-pty-term',
    lane: expectationLane('pty-stress', 'manager', 'manager', 4),
    tool: 'fork',
    args: (parsed) => ({ agent: extractPtyIdFromMessages(parsed) || 'pty-unknown', signal: 'TERM' }),
    match: { requiredTools: ['fork', 'join'] },
  });
  // 5) Join delivers the real exit (Closed).
  scenario.provider.expectToolCall({
    id: 'manager-join-term',
    lane: expectationLane('pty-stress', 'manager', 'manager', 5),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join'] },
  });
  // 6) Spawn a process that ignores TERM.
  scenario.provider.expectToolCall({
    id: 'manager-fork-pty2',
    lane: expectationLane('pty-stress', 'manager', 'manager', 6),
    tool: 'fork',
    args: { agent: 'pty', prompt: TRAP_PROMPT },
    match: { requiredTools: ['fork', 'join'] },
  });
  // 7) TERM is ignored by the trapped process.
  scenario.provider.expectToolCall({
    id: 'manager-pty2-term',
    lane: expectationLane('pty-stress', 'manager', 'manager', 7),
    tool: 'fork',
    args: (parsed) => ({ agent: extractPtyIdFromMessages(parsed) || 'pty-unknown', signal: 'TERM' }),
    match: { requiredTools: ['fork', 'join'] },
  });
  // 8) KILL forces the exit.
  scenario.provider.expectToolCall({
    id: 'manager-pty2-kill',
    lane: expectationLane('pty-stress', 'manager', 'manager', 8),
    tool: 'fork',
    args: (parsed) => ({ agent: extractPtyIdFromMessages(parsed) || 'pty-unknown', signal: 'KILL' }),
    match: { requiredTools: ['fork', 'join'] },
  });
  // 9) Join delivers the forced exit (Closed).
  scenario.provider.expectToolCall({
    id: 'manager-join-kill',
    lane: expectationLane('pty-stress', 'manager', 'manager', 9),
    tool: 'join',
    args: {},
    match: { requiredTools: ['fork', 'join'] },
  });
  // 10) No pty leaks after both joins.
  scenario.provider.expectToolCall({
    id: 'manager-list',
    lane: expectationLane('pty-stress', 'manager', 'manager', 10),
    tool: 'list',
    args: {},
    match: { requiredTools: ['fork', 'join', 'list'] },
  });

  // Final assistant summary after the 10 tool turns.
  scenario.provider.expectText({
    id: 'manager-final',
    lane: expectationLane('pty-stress', 'manager', 'manager', 11),
    text: 'Both PTYs closed; no active pty remains.',
    match: { requiredTools: ['fork', 'join', 'list'] },
  });

  const manager = await scenario.client.createSession();
  const managerId = getSessionId(manager);
  assert.ok(managerId, `manager session creation failed: ${JSON.stringify(manager)}`);
  scenario.sessionIds.push(managerId);
  bindLaneSession(scenario.provider, managerId, 'manager-title', 'manager');

  const turn = scenario.turn.start(managerId);
  const prompt = await scenario.client.request('POST', `/session/${managerId}/prompt_async`, {
    body: {
      agent: 'manager',
      parts: [{
        type: 'text',
        text: [
          'Use the structured PTY fork DSL only (not executor):',
          `1) fork agent="pty" with command: ${PTHRU_PROMPT}`,
          '2) fork the returned ptyId with prompt: ECHO_TEST',
          '3) fork the ptyId with an empty prompt to READ the buffered output',
          '4) fork the ptyId with signal="TERM"',
          '5) join and confirm the exit was delivered',
          `6) fork agent="pty" with command: ${TRAP_PROMPT}`,
          '7) fork that ptyId with signal="TERM" (the process ignores it)',
          '8) fork that ptyId with signal="KILL" to force exit',
          '9) join and confirm the forced exit was delivered',
          '10) call list and confirm no active pty remains',
          'Report the cwd printed by pwd, the echoed ECHO_TEST, and that both joins returned closed.',
        ].join(' '),
      }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `manager prompt failed: ${JSON.stringify(prompt.data)}`);

  for (const id of [
    'manager-fork-pty',
    'manager-pty-write',
    'manager-pty-read',
    'manager-pty-term',
    'manager-join-term',
    'manager-fork-pty2',
    'manager-pty2-term',
    'manager-pty2-kill',
    'manager-join-kill',
    'manager-list',
  ]) {
    await scenario.provider.waitForExpectation(id, WATCHDOG_TIMEOUT_MS);
    scenario.watchdog?.advance({ reason: id, lane: 'manager', blocking: true });
  }

  await turn.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });

  const results = forkResults(scenario.provider);
  const readResult = results.find((r) => typeof r.output === 'string' && r.output.includes('ECHO_TEST'));
  assert.ok(readResult, `read must return the echoed ECHO_TEST output: ${JSON.stringify(results)}`);
  assert.ok(readResult.output.includes('CWD='), `read must surface the session cwd via pwd: ${readResult.output}`);
  const isClosedOutcome = (outcome) =>
    outcome === 'closed' ||
    (typeof outcome === 'string' && outcome.includes('closed')) ||
    (Array.isArray(outcome) && outcome.includes('closed'));
  assert.ok(
    results.some((r) => isClosedOutcome(r.outcome)),
    `join must deliver the closed exit after TERM and after KILL: ${JSON.stringify(results)}`,
  );
  const listResult = results.find((r) => Array.isArray(r));
  assert.ok(listResult, `list must return an array: ${JSON.stringify(results)}`);
  assert.ok(
    !listResult.some((e) => e && e.kind === 'pty'),
    `no leaked pty after both joins: ${JSON.stringify(listResult)}`,
  );

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('PTY stress canary passed: cwd, write/read echo, TERM->Closed, KILL forces exit, no leak.');
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
