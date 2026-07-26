import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);
const COMMAND = "node -e \"process.stdout.write('x'.repeat(10000))\"";

function names(request) {
  return request.tools?.map((tool) => tool.function?.name || tool.name).filter(Boolean) || [];
}

let scenario;
try {
  if (!runStaticGate([__filename]).passed) throw new Error('executor canary contains prohibited polling');
  scenario = await setupScenario({ project: { files: { 'AGENTS.md': 'executor canary\n' } }, strict: true });
  scenario.provider.expectTitle({
    id: 'inspector-title',
    lane: expectationLane('executor', 'inspector-title', 'title', 1, 'title'),
  });
  scenario.provider.expectToolCall({
    id: 'inspector-executor',
    lane: expectationLane('executor', 'inspector', 'inspector', 1),
    tool: 'executor',
    args: {
      command: COMMAND,
      estimated_output_bytes: 2000,
      estimated_running_secs: 10,
      estimated_mem_usage: 'medium',
    },
    match: { requiredTools: ['executor'] },
  });
  scenario.provider.expectText({
    id: 'executor-map-0',
    lane: expectationLane('executor', 'map-0', 'executor', 1, 'chat', 'inspector'),
    text: 'chunk-0',
    match: { containsText: ['Summarize command output chunk'] },
  });
  // Hierarchical reduce only issues a reduce Executor when fan-in > 1 summary.
  // One 10KB spool chunk maps once and returns that summary directly.
  scenario.provider.expectText({
    id: 'inspector-final',
    lane: expectationLane('executor', 'inspector', 'inspector', 2),
    text: 'Executor completed with reduced output.',
    match: { requiredTools: ['executor'] },
  });

  const created = await scenario.client.createSession();
  const sessionId = getSessionId(created);
  assert.ok(sessionId, `inspector creation failed: ${JSON.stringify(created)}`);
  scenario.sessionIds.push(sessionId);
  bindLaneSession(scenario.provider, sessionId, 'inspector-title', 'inspector');
  const turn = scenario.turn.start(sessionId);
  const prompt = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      agent: 'inspector',
      parts: [{ type: 'text', text: 'Run the bounded output command and report the summary.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `inspector prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const requests = scenario.provider.requests;
  const childRequests = requests.filter((request) =>
    names(request).length === 0
    && typeof request.messages?.at(-1)?.content === 'string'
    && request.messages.at(-1).content.startsWith('Summarize command output chunk'),
  );
  assert.equal(childRequests.length, 1, '10KB output must create one Executor map request');
  assert.ok(childRequests.every((request) => names(request).length === 0), 'Executor summarizer must have no tools');
  const reduceRequests = requests.filter((request) => names(request).length === 0 && JSON.stringify(request).includes('Reduce level-'));
  assert.equal(reduceRequests.length, 0, 'single map summary must not allocate a reduce Executor');
  const summaryReturned = requests.some((request) => {
    const dump = JSON.stringify(request);
    return dump.includes('summary') || dump.includes('chunk-0') || names(request).includes('executor');
  });
  assert.ok(summaryReturned, 'Mapped summary must return to Inspector');

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Executor canary passed: real command, spool map/reduce, and Inspector result return.');
} catch (error) {
  console.error(`Executor canary failed: ${error.stack || error}`);
  if (scenario?.provider?.requests) {
    console.error(JSON.stringify(scenario.provider.requests.map((request) => ({
      sessionId: request.sessionId,
      tools: names(request),
      lastUser: typeof request.messages?.at(-1)?.content === 'string' ? request.messages.at(-1).content.slice(0, 120) : request.messages?.at(-1)?.content,
      calls: request.messages?.filter((message) => message.role === 'assistant').flatMap((message) => message.tool_calls || []).map((call) => call.function?.arguments),
    }))));
  }
  if (scenario?.provider?.unexpectedRequests) console.error(JSON.stringify(scenario.provider.unexpectedRequests));
  if (scenario?.sessionIds?.[0]) {
    try {
      const transcript = await scenario.client.messages(scenario.sessionIds[0]);
      const compact = transcript.data?.map((message) => ({
        role: message.info?.role,
        parts: message.parts?.map((part) => ({ type: part.type, text: part.text?.slice?.(0, 120), tool: part.tool, state: part.state?.status, error: part.state?.error?.slice?.(0, 120), output: part.state?.output?.slice?.(0, 120) })),
      }));
      console.error(`transcript: ${JSON.stringify(compact)}`);
    } catch {}
  }
  if (scenario?.host?.stdoutLog) console.error(`host stdout: ${scenario.host.stdoutLog.slice(-4000)}`);
  if (scenario?.host?.stderrLog) console.error(`host stderr: ${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
