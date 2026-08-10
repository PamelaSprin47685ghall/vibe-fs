/**
 * orchestrator-unhappy-path — one-stroke Orchestrator unhappy-path traversal.
 *
 * Scenario: scenarios/orchestrator-unhappy-path.toml.
 * Trajectory (ORCH-003/004/005/006/007):
 *   ManagerJobCreated → ConflictDetected → same Manager CONFLICT RESUMPTION
 *   → Published eq 1 → worktree clean → ManagerAgent identity preserved.
 *
 * Restart-after-claim remains orchestrator-restart-publish*; this canary owns
 * the conflict/resume path itself, not Host crash recovery.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';
import { runCanary } from '../support/scenario-driver.mjs';
import { readJournal, journalEventLines } from '../support/journal-observer.js';

function journalLines(workDir) {
  return journalEventLines(workDir);
}

function factPayloads(lines, caseName) {
  const found = [];
  const walk = (value) => {
    if (Array.isArray(value)) {
      if (typeof value[0] === 'string' && value[0] === caseName) found.push(value[1]);
      for (const item of value) walk(item);
    } else if (value && typeof value === 'object') {
      for (const child of Object.values(value)) walk(child);
    }
  };
  for (const line of lines) walk(line.Fact);
  return found;
}

const countCase = (lines, caseName) => factPayloads(lines, caseName).length;

function lastUserText(request) {
  const users = (request?.messages ?? []).filter((message) => message?.role === 'user');
  const last = users.at(-1);
  const content = last?.content;
  return Array.isArray(content) ? content.map((part) => part?.text ?? '').join('') : String(content ?? '');
}

/**
 * Stroke 4 checkpoint: conflict resume landed on the wire.
 * Do not require Published=0 here — wait("conflict-resume.0") only proves the
 * request matched; the engine may already have continued rebase/review/publish
 * by the time the flow step returns (async Manager completion).
 */
async function afterConflictResume(scenario) {
  const workDir = scenario.host.workDir;
  assert.ok(
    readJournal(workDir, 'ConflictDetected').named >= 1,
    'stroke 3/4: ConflictDetected must be durable at resume',
  );

  const resumeRequests = scenario.provider.requests.filter((request) =>
    lastUserText(request).includes('[CONFLICT RESUMPTION]'),
  );
  assert.ok(resumeRequests.length >= 1, 'stroke 4: CONFLICT RESUMPTION must reach a provider request');
  assert.ok(
    resumeRequests.some((request) => lastUserText(request).includes('do NOT restart the original task')),
    'stroke 4: resume prompt must forbid restarting the original task',
  );
  assert.ok(
    scenario.provider.matchCount('conflict-resume.0') >= 1,
    'stroke 4: conflict-resume.0 must be matched',
  );
}

/** Full trajectory oracle after Published. */
async function finalOracle(scenario) {
  const workDir = scenario.host.workDir;
  const lines = journalLines(workDir);

  assert.equal(countCase(lines, 'ManagerJobCreated'), 1, 'stroke 1: exactly one ManagerJobCreated');
  assert.ok(countCase(lines, 'ConflictDetected') >= 1, 'stroke 3: ConflictDetected required');
  assert.equal(countCase(lines, 'Published'), 1, 'stroke 5: exactly-once Orchestrator Published case');
  assert.equal(countCase(lines, 'JobFailed'), 0, 'job must not fail after conflict resume');
  assert.equal(countCase(lines, 'JobAbandoned'), 0, 'job must not be abandoned after conflict resume');

  // Master must actually receive the candidate (not a waitFact false-positive).
  const masterBody = execFileSync('git', ['-C', workDir, 'show', 'master:publish_proof.txt'], {
    encoding: 'utf8',
  });
  assert.equal(
    masterBody,
    'Published by orchestrator canary\n',
    'ORCH-005: master publish_proof.txt after ff-only publish',
  );

  const created = factPayloads(lines, 'ManagerJobCreated');
  assert.equal(created.length, 1, 'ManagerJobCreated payload present');
  const agentText = JSON.stringify(created[0]);
  assert.ok(
    agentText.includes('fast-manager') || agentText.includes('deep-manager'),
    `ORCH-006: ManagerAgent must be durable manager agent (got ${agentText})`,
  );

  const managerSessions = new Set(
    factPayloads(lines, 'ManagerJobCreated').map((payload) =>
      JSON.stringify(payload.ManagerSessionId ?? payload.managerSessionId ?? payload),
    ),
  );
  assert.equal(managerSessions.size, 1, 'ORCH-003: one Manager session for the job');

  assert.equal(
    scenario.provider.matchCount('manager.0'),
    1,
    'stroke 4: manager.0 (original task birth) must not re-fire as a second job start',
  );
  assert.ok(
    scenario.provider.matchCount('conflict-resume.0') >= 1,
    'stroke 4: conflict-resume must be delivered',
  );
  assert.ok(
    scenario.provider.matchCount('coder-resolve.0') >= 1,
    'stroke 4: coder-resolve must rewrite the conflicted file',
  );
}

const customs = {
  afterConflictResume,
  finalOracle,
};

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('orchestrator-unhappy-path canary static gate failed');
}
process.exit(await runCanary('orchestrator-unhappy-path', { customs }));
