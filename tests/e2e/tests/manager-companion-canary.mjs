import assert from 'node:assert/strict';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import {
  runStaticGate,
  setupScenario,
  teardownScenario,
  getSessionId,
} from '../index.js';
import { bindLaneSession } from './lane.mjs';
import { compileScenario } from '../scenario-schema.js';
import { ScenarioRuntime } from '../scenario-runtime.js';
import { WATCHDOG_TIMEOUT_MS } from '../time-budget.js';

const __filename = fileURLToPath(import.meta.url);
const managerTools = ['fork', 'join', 'list'];
const forbiddenManagerTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'verdict', 'executor', 'inspector', 'fork-pty'];

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'manager companion canary\n' } },
    strict: true,
  });

  const source = fs.readFileSync(new URL('../scripts/manager-companion.toml', import.meta.url), 'utf8');
  const compiled = compileScenario(source, { name: 'manager-companion.toml' });
  assert.equal(compiled.ok, true, compiled.ok ? '' : compiled.problems.join(' | '));
  const runtime = new ScenarioRuntime(compiled.scenario);
  scenario.provider.attachScenario(runtime);
  const managerAgent = compiled.scenario.session.agent;
  const managerPrompt = compiled.scenario.prompt.text;

  const parent = await scenario.client.request('POST', '/api/session', {
    body: { agent: managerAgent, model: { providerID: 'test', id: 'test-model' } },
  });
  const managerId = getSessionId(parent);
  assert.ok(managerId, `manager creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(managerId);
  bindLaneSession(scenario.provider, managerId, 'manager-title', 'manager');

  const turn = scenario.turn.start(managerId);
  const prompt = await scenario.client.request('POST', `/session/${managerId}/prompt_async`, {
    body: {
      agent: managerAgent,
      parts: [{ type: 'text', text: managerPrompt }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(prompt.ok, `prompt failed: ${JSON.stringify(prompt.data)}`);
  await turn.awaitTerminal({ requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });

  const managerRequest = scenario.provider.requests.find((request) => Array.isArray(request.tools) && request.tools.length > 0);
  assert.ok(managerRequest, 'manager provider request must be recorded');
  const managerRequestTools = managerRequest.tools.map((tool) => tool?.function?.name ?? tool?.name);
  assert.ok(managerTools.every((tool) => managerRequestTools.includes(tool)), 'manager tool schema must include manager tools');
  assert.ok(forbiddenManagerTools.every((tool) => !managerRequestTools.includes(tool)), 'manager tool schema must exclude forbidden tools');

  const sessionsResponse = await scenario.client.request('GET', '/session', { query: { scope: 'project' } });
  assert.equal(sessionsResponse.ok, true, JSON.stringify(sessionsResponse.data));
  const sessions = sessionsResponse.data?.data?.data ?? sessionsResponse.data?.data ?? sessionsResponse.data;
  assert.ok(Array.isArray(sessions), `session snapshot must be an array: ${JSON.stringify(sessions)}`);
  const bloggers = sessions.filter((session) => session?.agent === 'fast-blogger');
  assert.equal(bloggers.length, 1, 'the Host snapshot must contain exactly one fast-blogger');

  // Blogger parks after blog tool cycle — may never go session.idle while parked.
  // Journal BlogEntryCommitted is the completion signal for the first cycle.
  const workDir = scenario.host.workDir;
  const { execFileSync } = await import('node:child_process');
  const { join } = await import('node:path');
  const { existsSync, readdirSync, readFileSync } = await import('node:fs');
  const runtimeFacts = (factName) => {
    const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
    const runtimeDir = join(common.startsWith('/') ? common : join(workDir, common), 'wanxiangshu-next', 'runtimes');
    if (!existsSync(runtimeDir)) return [];
    return readdirSync(runtimeDir)
      .filter((name) => name.endsWith('.ndjson'))
      .flatMap((name) => readFileSync(join(runtimeDir, name), 'utf8').split('\n'))
      .filter((line) => line.trim() !== '')
      .map((line) => JSON.parse(line))
      .filter((fact) => JSON.stringify(fact).includes(factName));
  };
  const deadline = Date.now() + WATCHDOG_TIMEOUT_MS;
  while (Date.now() < deadline && runtimeFacts('BlogEntryCommitted').length < 1) {
    await new Promise((r) => setTimeout(r, 50));
    scenario.watchdog?.advance({ reason: 'blog-entry-wait', lane: 'manager-blogger', blocking: true });
  }
  assert.ok(runtimeFacts('BlogEntryCommitted').length >= 1, 'manager blogger must commit at least one BlogEntry');
  assert.deepEqual(runtime.unanswered().map((entry) => entry.id), [], 'all non-internal scenario steps must complete');
  assert.deepEqual(runtime.unmetMust(), [], 'all required scenario steps must complete');

  console.log('Manager companion canary passed.');
} catch (error) {
  console.error('Manager companion canary failed:', error);
  if (scenario) {
    console.error(`Host stdout:\n${scenario.host.stdoutLog}`);
    console.error(`Host stderr:\n${scenario.host.stderrLog}`);
    console.error(`Event tail:\n${scenario.events.dump(20)}`);
  }
  process.exitCode = 1;
} finally {
  if (scenario) await teardownScenario(scenario);
}
