/**
 * blogger-quiet-stop — ENFORCER-040/045 + P0 empty-messages loop regression (bdaa616d).
 *
 * Scenario: scenarios/blogger-quiet-stop.toml
 *
 * Proved here:
 *  - first main turn completes with assistant terminal and starts one Blogger cycle;
 *  - one Blogger request per main-turn material: the second main turn's offer
 *    resumes the parked transform with the cumulative delta, so exactly two
 *    Blogger requests arrive, both matched by the single declared step
 *    blogger.0 — step is the per-request count of assistant messages AFTER the
 *    last user message, and the second request's last user message is the
 *    new-material instruction with nothing after it, so it is step 0 too; each
 *    delivery answers a blog tool-call — exactly two blog calls total;
 *  - every Blogger provider request has messages.length > 0 (empty messages never reach provider);
 *  - journal has no ProviderRetryAttempt and no FallbackCursorAdvanced
 *    (park/StopPhysicalRun after commit is not a provider-failure continuation);
 *  - no third Blogger request: the old bug was unbounded (empty-messages 400 →
 *    continuation → blog again, ad infinitum). The strict mock fail-closes on an
 *    unmatched third call, and the exact count is asserted explicitly.
 *  - all declared steps consumed (unanswered/unmetMust/unexpected empty).
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { compileScenario } from '../support/scenario-schema.js';
import { ScenarioRuntime } from '../support/scenario-runtime.js';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../support/index.js';
import { WATCHDOG_TIMEOUT_MS, ENFORCER_POLL_SLICE_MS } from '../support/time-budget.js';
import { bindLaneSession } from '../support/lane.mjs';

const __filename = fileURLToPath(import.meta.url);

function runtimeFacts(workDir, factName) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(
    path.isAbsolute(common) ? common : path.resolve(workDir, common),
    'wanxiangshu-next',
    'runtimes',
  );
  if (!fs.existsSync(runtimeDir)) return [];

  return fs
    .readdirSync(runtimeDir)
    .filter((name) => name.endsWith('.ndjson'))
    .flatMap((name) => fs.readFileSync(path.join(runtimeDir, name), 'utf8').split('\n'))
    .filter((line) => line.trim() !== '')
    .map((line) => JSON.parse(line))
    .filter((fact) => JSON.stringify(fact).includes(factName));
}

function bloggerRequests(provider, bloggerId) {
  return provider.requests.filter((request) => request.sessionID === bloggerId);
}

async function waitForCount(scenario, predicate, minCount, reason) {
  const deadline = Date.now() + WATCHDOG_TIMEOUT_MS;
  while (Date.now() < deadline) {
    if (predicate()) {
      scenario.watchdog?.advance({ reason, lane: 'provider', blocking: true });
      return;
    }
    await new Promise((resolve) => setTimeout(resolve, ENFORCER_POLL_SLICE_MS));
  }
  assert.fail(`${reason}: condition not reached within ${WATCHDOG_TIMEOUT_MS}ms`);
}

function printDiagnostics(scenario) {
  if (!scenario) return;
  console.error('\n── blogger-quiet-stop provider diagnostics ──');
  console.error(
    JSON.stringify(
      { requests: scenario.provider.requests, unexpected: scenario.provider.unexpectedRequests },
      null,
      2,
    ),
  );
  console.error('\n── OpenCode events ──');
  console.error(scenario.events.dump(200));
  if (scenario.host?.stdoutLog) console.error(`\n── host stdout ──\n${scenario.host.stdoutLog.slice(-5000)}`);
  if (scenario.host?.stderrLog) console.error(`\n── host stderr ──\n${scenario.host.stderrLog.slice(-5000)}`);
}

const staticResult = runStaticGate([__filename]);
if (!staticResult.passed) {
  console.error('blogger-quiet-stop static gate failed:', JSON.stringify(staticResult.violations, null, 2));
  process.exit(1);
}

let scenario;
try {
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': '- blogger quiet stop P0 canary\n' } },
    strict: true,
  });

  const source = fs.readFileSync(new URL('../scenarios/blogger-quiet-stop.toml', import.meta.url), 'utf8');
  const compiled = compileScenario(source, { name: 'blogger-quiet-stop.toml' });
  assert.equal(compiled.ok, true, compiled.ok ? '' : compiled.problems.join(' | '));
  const runtime = new ScenarioRuntime(compiled.scenario);
  scenario.provider.attachScenario(runtime);

  // ── 1. first main turn ────────────────────────────────────────────────────
  const primaryResponse = await scenario.client.request('POST', '/api/session', {
    body: { agent: 'fast-coder', model: { providerID: 'test', id: 'test-model' } },
  });
  const primaryId = getSessionId(primaryResponse);
  assert.ok(primaryId, `primary session creation failed: ${JSON.stringify(primaryResponse)}`);
  scenario.sessionIds.push(primaryId);
  bindLaneSession(scenario.provider, primaryId, 'coder-title', 'fast-coder');

  const turn1 = scenario.turn.start(primaryId);
  const firstPrompt = await scenario.client.request('POST', `/session/${primaryId}/prompt_async`, {
    body: {
      agent: 'fast-coder',
      parts: [{ type: 'text', text: 'First coder turn.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(firstPrompt.ok, `first coder prompt failed: ${JSON.stringify(firstPrompt.data)}`);
  await turn1.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });
  scenario.watchdog?.advance({ reason: 'main-turn-1', lane: 'manager', blocking: true });

  await waitForCount(
    scenario,
    () => scenario.provider.requests.some((r) => r.sessionID !== primaryId),
    1,
    'blogger-first-request',
  );
  const bloggerId = [
    ...new Set(
      scenario.events.allEvents
        .map((e) => (e.type === 'session.created' ? e.sessionID : null))
        .filter(Boolean),
    ),
  ].find((id) => id !== primaryId);
  assert.ok(bloggerId, 'a Blogger child session must be created');

  const blogRequests = () => bloggerRequests(scenario.provider, bloggerId);
  await waitForCount(scenario, () => blogRequests().length >= 1, 1, 'blogger-request-1');

  // ── 2. BlogEntryCommitted (≥1) ────────────────────────────────────────────
  await waitForCount(
    scenario,
    () => runtimeFacts(scenario.host.workDir, 'BlogEntryCommitted').length >= 1,
    1,
    'blog-entry-committed',
  );

  // ── 3. second main turn — resumes the parked transform with new material ───
  // Each main turn's material legitimately starts one Blogger cycle, so the
  // second main turn must produce exactly one more Blogger request (the resumed
  // transform carrying the cumulative delta). Not zero, and never more than one.
  const turn2 = scenario.turn.start(primaryId);
  const secondPrompt = await scenario.client.request('POST', `/session/${primaryId}/prompt_async`, {
    body: {
      agent: 'fast-coder',
      parts: [{ type: 'text', text: 'Second coder turn.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(secondPrompt.ok, `second coder prompt failed: ${JSON.stringify(secondPrompt.data)}`);
  await turn2.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });
  scenario.watchdog?.advance({ reason: 'main-turn-2', lane: 'manager', blocking: true });

  await waitForCount(scenario, () => blogRequests().length >= 2, 2, 'blogger-request-2');
  assert.equal(
    blogRequests().length,
    2,
    `exactly two Blogger provider requests total (one per main turn, got ${blogRequests().length})`,
  );

  // ── 4. every Blogger provider request has non-empty messages ──────────────
  // Runs after both requests have arrived so it covers EVERY request.
  for (const request of blogRequests()) {
    const messages = request.messages ?? [];
    assert.ok(
      Array.isArray(messages) && messages.length > 0,
      `Blogger provider request must have non-empty messages (got length=${messages.length})`,
    );
  }

  // ── 5. no provider-failure continuation after either park/StopPhysicalRun ──
  const retryFacts = runtimeFacts(scenario.host.workDir, 'ProviderRetryAttempt');
  const fallbackFacts = runtimeFacts(scenario.host.workDir, 'FallbackCursorAdvanced');
  assert.equal(retryFacts.length, 0, `no ProviderRetryAttempt (got ${retryFacts.length})`);
  assert.equal(fallbackFacts.length, 0, `no FallbackCursorAdvanced (got ${fallbackFacts.length})`);

  // ── 6. one-shot consumption ───────────────────────────────────────────────
  assert.deepEqual(runtime.unanswered(), [], 'all declared steps must be consumed');
  assert.deepEqual(runtime.unmetMust(), [], 'all required scenario steps must complete');
  assert.equal(scenario.provider.unexpectedRequests.length, 0, 'scenario must not receive unexpected provider requests');

  await teardownScenario(scenario);

  console.log(
    'Blogger quiet-stop canary passed: exactly two Blogger requests (one per main turn), exactly two blog calls, non-empty Blogger messages, no ProviderRetryAttempt/FallbackCursorAdvanced, no third blog request.',
  );
} catch (error) {
  console.error(`blogger-quiet-stop canary failed: ${error.stack || error}`);
  printDiagnostics(scenario);
  if (scenario) {
    try {
      await teardownScenario(scenario, { keepOnFailure: true });
    } catch {}
  }
  process.exit(1);
}
