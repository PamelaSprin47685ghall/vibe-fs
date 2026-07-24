import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';

const __filename = fileURLToPath(import.meta.url);
const COMMAND = "node -e \"process.stdout.write('x'.repeat(450000))\"";

function names(request) {
  return request.tools?.map((tool) => tool.function?.name || tool.name).filter(Boolean) || [];
}

let scenario;
try {
  if (!runStaticGate([__filename]).passed) throw new Error('executor canary contains prohibited polling');
  scenario = await setupScenario({ project: { files: { 'AGENTS.md': 'executor canary\n' } }, strict: true });
  scenario.provider.allowTitleGeneration();
  scenario.provider.allowOutOfOrder();

  scenario.provider.expectToolCall({
    id: 'inspector-executor',
    tool: 'executor',
    args: {
      command: COMMAND,
      estimated_output_bytes: 100000,
      estimated_running_secs: 10,
      estimated_mem_usage: 'medium',
    },
    match: { requiredTools: ['executor'] },
  });
  for (let index = 0; index < 3; index += 1) {
    scenario.provider.expectText({
      id: `executor-map-${index}`,
      text: `chunk-${index}`,
      match: { containsText: ['Summarize command output chunk'] },
    });
  }
  scenario.provider.expectText({
    id: 'executor-reduce',
    text: 'reduced-output',
    match: { containsText: ['Reduce these command-output summaries'] },
  });
  scenario.provider.expectText({
    id: 'inspector-final',
    text: 'Executor completed with reduced output.',
    match: { requiredTools: ['executor'] },
  });

  const created = await scenario.client.createSession();
  const sessionId = getSessionId(created);
  assert.ok(sessionId, `inspector creation failed: ${JSON.stringify(created)}`);
  scenario.sessionIds.push(sessionId);
  const turn = scenario.turn.start(sessionId);
  const prompt = await scenario.client.request('POST', `/session/${sessionId}/prompt_async`, {
    body: {
      agent: 'inspector',
      parts: [{ type: 'text', text: 'Run the bounded output command and report the summary.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `inspector prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ timeoutMs: 60000, requireActivity: true, requireAssistantTerminal: true, requireIdleAfterActivity: true });

  const requests = scenario.provider.requests;
  const childRequests = requests.filter((request) =>
    names(request).length === 0
    && typeof request.messages?.at(-1)?.content === 'string'
    && request.messages.at(-1).content.startsWith('Summarize command output chunk'),
  );
  assert.equal(childRequests.length, 3, '450KB output must create three 200KB Executor map requests');
  assert.ok(childRequests.every((request) => names(request).length === 0), 'Executor summarizer must have no tools');
  const reduceRequests = requests.filter((request) => JSON.stringify(request).includes('Reduce these command-output summaries'));
  assert.equal(reduceRequests.length, 1, 'Executor must reduce map summaries exactly once');
  assert.ok(requests.some((request) => names(request).includes('executor') && request !== requests[0]), 'Reduced result did not return to Inspector');

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Executor canary passed: real command, 200KB map/reduce, and Inspector result return.');
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
