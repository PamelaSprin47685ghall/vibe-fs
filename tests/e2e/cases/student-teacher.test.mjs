import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import { compileScenario } from '../support/scenario-schema.js';
import { ScenarioRuntime } from '../support/scenario-runtime.js';
import {
  getSessionId,
  runStaticGate,
  setupScenario,
  teardownScenario,
} from '../support/index.js';
import { bindLaneSession } from '../support/lane.mjs';
import { WATCHDOG_TIMEOUT_MS } from '../support/time-budget.js';

const __filename = fileURLToPath(import.meta.url);
const rootText = 'Learn the canary topic and compile a reusable skill.';
const finalText = 'Created .agent/skills/student-canary/SKILL.md.';

const toolNames = (request) =>
  (request?.tools ?? []).map((tool) => tool?.function?.name ?? tool?.name);

const requestText = (request) =>
  (request?.messages ?? [])
    .map((message) => {
      const content = message?.content ?? '';
      return Array.isArray(content)
        ? content.map((part) => part?.text ?? '').join('')
        : String(content);
    })
    .join('\n');

const sessionsOf = (response) =>
  response.data?.data?.data ?? response.data?.data ?? response.data;

const messagesOf = (response) =>
  response.data?.data?.data ?? response.data?.data ?? response.data;

const messageText = (message) =>
  (message?.parts ?? [])
    .filter((part) => part?.type === 'text')
    .map((part) => part.text ?? '')
    .join('');

assert.equal(runStaticGate([__filename]).passed, true);

let scenario;
try {
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'student teacher Host canary\n' } },
    strict: true,
  });

  const source = fs.readFileSync(new URL('../scenarios/student-teacher.toml', import.meta.url), 'utf8');
  const compiled = compileScenario(source, { name: 'student-teacher.toml' });
  assert.equal(compiled.ok, true, compiled.ok ? '' : compiled.problems.join(' | '));
  const runtime = new ScenarioRuntime(compiled.scenario);
  scenario.provider.attachScenario(runtime);

  const created = await scenario.client.request('POST', '/api/session', {
    body: { agent: 'fast-student', model: { providerID: 'test', id: 'test-model' } },
  });
  const studentId = getSessionId(created);
  assert.ok(studentId, `Student creation failed: ${JSON.stringify(created)}`);
  scenario.sessionIds.push(studentId);
  bindLaneSession(scenario.provider, studentId, 'student-title', 'fast-student');

  const turn = scenario.turn.start(studentId);
  const prompt = await scenario.client.request('POST', `/session/${studentId}/prompt_async`, {
    body: {
      agent: 'fast-student',
      parts: [{ type: 'text', text: rootText }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.equal(prompt.ok, true, JSON.stringify(prompt.data));
  await turn.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });

  const deadline = Date.now() + WATCHDOG_TIMEOUT_MS;
  while (runtime.unmetMust().length > 0 && Date.now() < deadline) {
    await new Promise((resolve) => setTimeout(resolve, 50));
    scenario.watchdog?.advance({ reason: 'student-teacher-must', lane: 'student', blocking: true });
  }
  assert.deepEqual(runtime.unmetMust(), []);
  await scenario.provider.waitForExpectation('student-compile.2', WATCHDOG_TIMEOUT_MS);

  let messages = [];
  const completionDeadline = Date.now() + WATCHDOG_TIMEOUT_MS;
  while (Date.now() < completionDeadline) {
    const response = await scenario.client.request('GET', `/session/${studentId}/message`);
    assert.equal(response.ok, true, JSON.stringify(response.data));
    messages = messagesOf(response);
    const latest = Array.isArray(messages)
      ? messages.filter((message) => message?.info?.role === 'assistant').map(messageText).at(-1)
      : null;
    if (latest === finalText) break;
    await new Promise((resolve) => setTimeout(resolve, 50));
    scenario.watchdog?.advance({ reason: 'student-final-message', lane: 'student', blocking: true });
  }

  const sessionResponse = await scenario.client.request('GET', '/session', { query: { scope: 'project' } });
  assert.equal(sessionResponse.ok, true, JSON.stringify(sessionResponse.data));
  const sessions = sessionsOf(sessionResponse);
  assert.ok(Array.isArray(sessions), `session snapshot must be an array: ${JSON.stringify(sessions)}`);
  const teachers = sessions.filter((session) => session?.agent === 'fast-teacher');
  assert.equal(teachers.length, 1, 'one Student run must own exactly one Teacher Session');
  assert.equal(teachers[0].parentID, studentId, 'Teacher must be the direct private child of Student');
  if (!scenario.sessionIds.includes(teachers[0].id)) scenario.sessionIds.push(teachers[0].id);

  const studentRequests = scenario.provider.requests.filter((request) => request.sessionID === studentId);
  const learnRequest = studentRequests.find(
    (request) => requestText(request).includes(rootText) && toolNames(request).includes('teacher'),
  );
  const compileRequest = studentRequests.find(
    (request) => requestText(request).includes('你已经结束向 Teacher 提问') && toolNames(request).includes('write'),
  );
  assert.deepEqual(toolNames(learnRequest), ['teacher'], 'StudentLearn schema must contain only teacher');
  assert.deepEqual(
    toolNames(compileRequest).sort(),
    ['edit', 'glob', 'grep', 'read', 'return', 'write'].sort(),
    'StudentCompile schema must be the frozen six-tool set',
  );

  const teacherRequests = scenario.provider.requests.filter((request) => request.sessionID === teachers[0].id);
  assert.equal(teacherRequests.length, 1, 'Teacher return must end its turn without a prose continuation');
  const teacherTools = toolNames(teacherRequests[0]);
  assert.ok(teacherTools.includes('return'), 'Teacher schema must expose return');
  for (const forbidden of ['fork', 'fork-manager', 'join', 'list', 'fork-pty', 'suicide']) {
    assert.equal(teacherTools.includes(forbidden), false, `Teacher schema must exclude ${forbidden}`);
  }

  const skillPath = path.join(scenario.host.workDir, '.agent', 'skills', 'student-canary', 'SKILL.md');
  assert.equal(fs.readFileSync(skillPath, 'utf8'), '# Student canary\n\nPreserve one proven causal chain.\n');

  const absoluteGitDir = execFileSync('git', ['-C', scenario.host.workDir, 'rev-parse', '--absolute-git-dir'], {
    encoding: 'utf8',
  }).trim();
  assert.equal(
    fs.existsSync(path.join(absoluteGitDir, 'wanxiangshu', 'student', studentId)),
    false,
    'Student final return must remove its private QA tree before completion',
  );

  assert.ok(Array.isArray(messages), `message snapshot must be an array: ${JSON.stringify(messages)}`);
  const assistantTexts = messages
    .filter((message) => message?.info?.role === 'assistant')
    .map(messageText);
  assert.equal(assistantTexts.at(-1), finalText, 'same Host loop must commit the exact Student return text');
  assert.equal(
    assistantTexts.includes('provider paraphrase that must be replaced'),
    false,
    'provider paraphrase must not escape experimental.text.complete',
  );

  scenario.provider.expectSatisfied();
  console.log('Student Teacher Host canary passed.');
} catch (error) {
  console.error('Student Teacher Host canary failed:', error.stack || error);
  if (scenario) {
    console.error(`Host stdout:\n${scenario.host.stdoutLog}`);
    console.error(`Host stderr:\n${scenario.host.stderrLog}`);
    console.error(`Event tail:\n${scenario.events.dump(40)}`);
    console.error(`Provider requests:\n${JSON.stringify(scenario.provider.requests, null, 2)}`);
  }
  process.exitCode = 1;
} finally {
  if (scenario) await teardownScenario(scenario);
}
