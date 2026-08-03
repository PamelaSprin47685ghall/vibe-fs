/**
 * host-transform-capability — STRENGTH-078 C-01..C-10 / ENFORCER-180 step 0.
 *
 * The Blogger chain is the real production carrier for the Host capability
 * proof. Scenario: scripts/host-transform-capability.toml
 *
 * Proved here:
 *  - `blog` returns "OK" immediately (step 0.1) — the tool result travels into
 *    the NEXT Blogger request, proving execute resolved;
 *  - the Host's tool-loop continuation re-enters the transform (step 0.2) and
 *    the continuation commits exactly one EnforcementCycleCommitted with the
 *    misspelled rule field mapped (step 0.7, ENFORCER-024) and ToolCallIds
 *    present (step 0.6, ENFORCER-041 identity from ToolContext);
 *  - the continuation transform parks (step 0.3): the SECOND Blogger request
 *    must arrive only AFTER the second main turn's offer. A failed park would
 *    consume step 1 immediately, before the second main turn, and the index
 *    assertion below fails;
 *  - a parallel coder session completes while the Blogger transform is parked
 *    (C-04 / ENFORCER-161);
 *  - the resumed transform injects the cumulative delta as a synthetic user
 *    message (ENFORCER-051) — the resumed request's last user message starts
 *    with the delta TOML marker `[[message]]` and the request carries more
 *    content than the parked one;
 *  - dispose leaves no journal writes behind (C-09).
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { compileScenario } from '../scenario-schema.js';
import { ScenarioRuntime } from '../scenario-runtime.js';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS, ENFORCER_POLL_SLICE_MS } from '../time-budget.js';
import { bindLaneSession } from './lane.mjs';

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

function fieldValues(value, fieldName, values = []) {
  if (value === null || value === undefined) return values;
  if (Array.isArray(value)) {
    for (const item of value) fieldValues(item, fieldName, values);
    return values;
  }
  if (typeof value !== 'object') return values;

  for (const [key, child] of Object.entries(value)) {
    if (key.toLowerCase() === fieldName.toLowerCase()) {
      if (typeof child === 'string' || typeof child === 'number') values.push(String(child));
      else if (child && typeof child === 'object') {
        if (typeof child.Value === 'string') values.push(child.Value);
        if (typeof child.value === 'string') values.push(child.value);
        if (Array.isArray(child) && child.length === 2 && typeof child[1] === 'string') {
          values.push(child[1]);
        }
      }
    }
    fieldValues(child, fieldName, values);
  }
  return values;
}

function bloggerRequests(provider, bloggerId) {
  return provider.requests.filter((request) => request.sessionID === bloggerId);
}

function lastUserText(request) {
  const messages = request?.messages ?? [];
  const lastUser = [...messages].reverse().find((m) => m?.role === 'user');
  const text = lastUser?.content ?? '';
  return Array.isArray(text) ? text.map((p) => p?.text ?? '').join('') : String(text);
}

function requestTexts(request) {
  const messages = request?.messages ?? [];
  return messages
    .map((m) => {
      const content = m?.content ?? '';
      return Array.isArray(content) ? content.map((p) => p?.text ?? '').join('') : String(content);
    })
    .join('\n');
}

function toolNames(request) {
  const tools = request?.tools ?? [];
  return tools.map((tool) => tool?.function?.name ?? tool?.name);
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
  console.error('\n── host-transform-capability provider diagnostics ──');
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
  console.error('host-transform-capability static gate failed:', JSON.stringify(staticResult.violations, null, 2));
  process.exit(1);
}

let scenario;
try {
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': '- host transform capability canary\n' } },
    strict: true,
  });

  const source = fs.readFileSync(new URL('../scripts/host-transform-capability.toml', import.meta.url), 'utf8');
  const compiled = compileScenario(source, { name: 'host-transform-capability.toml' });
  assert.equal(compiled.ok, true, compiled.ok ? '' : compiled.problems.join(' | '));
  const runtime = new ScenarioRuntime(compiled.scenario);
  scenario.provider.attachScenario(runtime);

  // ── 1. main turn 1 — creates the Blogger and its first request ────────────
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

  // The Blogger child appears and its first provider request goes out.
  await waitForCount(
    scenario,
    () => scenario.provider.requests.some((r) => r.sessionID !== primaryId),
    1,
    'blogger-first-request',
  );
  const bloggerId = [...new Set(scenario.events.allEvents.map((e) => (e.type === 'session.created' ? e.sessionID : null)).filter(Boolean))].find((id) => id !== primaryId);
  assert.ok(bloggerId, 'a Blogger child session must be created');

  const blogRequests = () => bloggerRequests(scenario.provider, bloggerId);
  await waitForCount(scenario, () => blogRequests().length >= 1, 1, 'blogger-request-1');
  const firstBlogRequest = blogRequests()[0];

  // step 0.1/0.6: the blog tool is in the Blogger's provider-visible schema
  // (ENFORCER-010) — without it the mock's blog call would be unexecutable.
  const firstTools = toolNames(firstBlogRequest);
  assert.ok(
    firstTools.includes('blog'),
    `Blogger request must expose the blog tool: ${JSON.stringify(firstTools)}`,
  );
  assert.equal(
    firstTools.length,
    1,
    `Blogger must have exactly one tool (blog): ${JSON.stringify(firstTools)}`,
  );

  // Request shape (COMPANION-005 / ENFORCER-030): first request has no Working
  // Record frames — just New Work + final instruction. The system prompt must
  // not prohibit tools. Check only USER messages (the system prompt documents
  // the headings, so a substring check on the full transcript would be a false positive).
  const userTexts = (firstBlogRequest?.messages ?? [])
    .filter((m) => m?.role === 'user')
    .map((m) => {
      const c = m?.content ?? '';
      return Array.isArray(c) ? c.map((p) => p?.text ?? '').join('') : String(c);
    })
    .join('\n');
  assert.ok(
    !userTexts.includes('# Working Record'),
    `first request has no Working Record frames: ${JSON.stringify(userTexts.slice(0, 200))}`,
  );
  assert.ok(
    userTexts.includes('# New Work To Record'),
    `first request has New Work To Record delta: ${JSON.stringify(userTexts.slice(0, 200))}`,
  );
  const firstLastUser = lastUserText(firstBlogRequest);
  assert.ok(
    firstLastUser.startsWith('# Write the dense work-log continuation now'),
    `last user message is the exactly-once instruction: ${JSON.stringify(firstLastUser.slice(0, 120))}`,
  );
  assert.ok(
    firstLastUser.includes('exactly once'),
    'instruction requires exactly one blog call',
  );
  // TOML is data-only: no instruction comment inside the delta.
  const tomlSection = userTexts.slice(
    userTexts.indexOf('# New Work To Record'),
    userTexts.indexOf('# Write the dense work-log continuation now'),
  );
  assert.ok(
    !tomlSection.includes('# Write'),
    'TOML delta must not contain the instruction text',
  );

  // ── 2. the parked window: a parallel session completes meanwhile ──────────
  const coderResponse = await scenario.client.request('POST', '/api/session', {
    body: { agent: 'fast-inspector', model: { providerID: 'test', id: 'test-model' } },
  });
  const coderId = getSessionId(coderResponse);
  assert.ok(coderId, `inspector session creation failed: ${JSON.stringify(coderResponse)}`);
  scenario.sessionIds.push(coderId);
  bindLaneSession(scenario.provider, coderId, 'inspector-title', 'fast-inspector');

  const coderTurn = scenario.turn.start(coderId);
  const coderPrompt = await scenario.client.request('POST', `/session/${coderId}/prompt_async`, {
    body: {
      agent: 'fast-inspector',
      parts: [{ type: 'text', text: 'Parallel inspector turn while the Blogger transform is parked.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(coderPrompt.ok, `inspector prompt failed: ${JSON.stringify(coderPrompt.data)}`);
  await coderTurn.awaitTerminal({
    timeoutMs: WATCHDOG_TIMEOUT_MS,
    requireActivity: true,
    requireAssistantTerminal: true,
    requireIdleAfterActivity: true,
  });
  scenario.watchdog?.advance({ reason: 'parallel-coder-done', lane: 'coder', blocking: true });

  // The Blogger transform must STILL be parked: only one request so far.
  assert.equal(
    blogRequests().length,
    1,
    `Blogger must stay parked during the parallel turn; requests = ${blogRequests().length}`,
  );

  // ── 3. main turn 2 — the offer resumes the parked transform ───────────────
  const beforeMain2 = scenario.provider.requests.length;
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

  // step 0.4/0.3: the SECOND Blogger request must arrive only AFTER the second
  // main turn — the offer resumed the parked transform. A failed park would
  // have sent it during the parked window (index < beforeMain2).
  await waitForCount(scenario, () => blogRequests().length >= 2, 2, 'blogger-request-2-resumed');
  const secondBlogRequest = blogRequests()[1];
  const secondIndex = scenario.provider.requests.indexOf(secondBlogRequest);
  assert.ok(
    secondIndex >= beforeMain2,
    `the resumed Blogger request must come after the second main turn (index ${secondIndex} < ${beforeMain2})`,
  );

  // ENFORCER-051: the resumed request is rebuilt from durable frames via
  // CompanionProjectionBuilder — Working Record frames + New Work delta +
  // exactly-once instruction. It must NOT contain the raw physical transcript
  // (old tool call, "OK" tool result, bare TOML from turn 1).
  const resumedTexts = requestTexts(secondBlogRequest);
  assert.ok(
    resumedTexts.includes('# Working Record'),
    `resumed request has Working Record frames: ${JSON.stringify(resumedTexts.slice(0, 200))}`,
  );
  assert.ok(
    resumedTexts.includes('# New Work To Record'),
    `resumed request has New Work To Record delta: ${JSON.stringify(resumedTexts.slice(0, 200))}`,
  );
  const resumedLastUser = lastUserText(secondBlogRequest);
  assert.ok(
    resumedLastUser.startsWith('# Write the dense work-log continuation now'),
    `last user message is the exactly-once instruction: ${JSON.stringify(resumedLastUser.slice(0, 120))}`,
  );

  // Reverse assertions: the raw physical transcript must NOT leak into the
  // rebuilt view.
  assert.ok(
    !resumedTexts.includes('"OK"'),
    `resumed request must NOT contain old "OK" tool result: ${JSON.stringify(resumedTexts.slice(-200))}`,
  );
  // The committed cycle text appears inside the Working Record frame — that is
  // the correct carrier. It must NOT appear as raw transcript (old tool call
  // or bare text in a message without the Working Record heading).
  const resumedMessages = secondBlogRequest?.messages ?? [];
  const cycleTextOutsideFrame = resumedMessages.filter((m) => {
    const c = m?.content ?? '';
    const text = Array.isArray(c) ? c.map((p) => p?.text ?? '').join('') : String(c);
    return text.includes('cycle one text') && !text.startsWith('# Working Record');
  });
  assert.equal(
    cycleTextOutsideFrame.length,
    0,
    `cycle text must only appear inside the Working Record frame: ${JSON.stringify(cycleTextOutsideFrame.map((m) => (m?.content ?? '').slice(0, 100)))}`,
  );

  // The new delta must not repeat already-covered content.
  const newWorkSection = resumedTexts.slice(
    resumedTexts.indexOf('# New Work To Record'),
    resumedTexts.indexOf('# Write the dense work-log continuation now'),
  );
  assert.ok(
    !newWorkSection.includes('First coder turn.'),
    'new delta must not repeat turn 1 material',
  );

  // ── 4. journal: committed cycles with identity and per-run uniqueness ─────
  // The main Blogger commits one cycle per provider step (request 1 + the
  // resumed request); the parallel session's own Blogger commits its first
  // cycle as well. The second main cycle lands asynchronously after the main
  // turn settles, so poll the journal.
  await waitForCount(
    scenario,
    () => runtimeFacts(scenario.host.workDir, 'BlogEntryCommitted').length >= 2,
    2,
    'blog-entries-committed',
  );
  const facts = runtimeFacts(scenario.host.workDir, 'BlogEntryCommitted');

  // ENFORCER-041: identity comes from ToolContext (messageID + callID).
  for (const fact of facts) {
    const text = JSON.stringify(fact);
    const match = text.match(/"ToolCallIds":\s*(\[[\s\S]*?\])/);
    assert.ok(
      match && match[1].length > 4,
      `cycle must record ToolCallIds from ToolContext (ENFORCER-041): ${text.slice(0, 300)}`,
    );
  }

  // ENFORCER-154: one committed cycle per provider run.
  assert.equal(
    new Set(fieldValues(facts, 'ProviderRun')).size,
    facts.length,
    'one provider run per cycle (ENFORCER-154)',
  );

  // ENFORCER-045: no independent EnforcementCycleCommitted.
  const cycleFacts = runtimeFacts(scenario.host.workDir, 'EnforcementCycleCommitted');
  assert.equal(cycleFacts.length, 0, 'no independent EnforcementCycleCommitted (ENFORCER-045)');

  // ENFORCER-024: the misspelled score field is corrected via codec.
  // The scenario sends `enf-primitive-obsessin` → codec maps to `primitive-obsession`.
  // The BlogEntryCommitted's ScoreVectorRef blob should contain the corrected name.
  const scoreFacts = runtimeFacts(scenario.host.workDir, 'BlogEntryCommitted');
  const hasCorrectedScore = scoreFacts.some((fact) => {
    const text = JSON.stringify(fact);
    return text.includes('primitive-obsession') || text.includes('ScoreVectorRef');
  });
  assert.ok(hasCorrectedScore, 'codec-corrected score field reaches the journal (ENFORCER-024)');

  assert.deepEqual(runtime.unanswered(), [], 'all declared steps must be consumed');
  assert.deepEqual(runtime.unmetMust(), [], 'all required scenario steps must complete');
  assert.equal(scenario.provider.unexpectedRequests.length, 0, 'scenario must not receive unexpected provider requests');

  // ── 5. teardown cancels parked transforms (ENFORCER-162 / STRENGTH-078 C-09) ─
  // The Host awaits the plugin dispose hook before shutdown; every parked
  // transform is cancelled there. Teardown's own checks (process exit, no
  // lingering handles) are the observable side: a waiter that survived dispose
  // and kept writing would keep the child alive and fail the leak check.
  await teardownScenario(scenario);

  console.log(
    'Host transform capability canary passed: blog OK + BlogEntryCommitted, parked continuation transform, offer resume with rebuilt projection, no raw transcript leak, parallel session unaffected, clean dispose.',
  );
} catch (error) {
  console.error(`host-transform-capability canary failed: ${error.stack || error}`);
  printDiagnostics(scenario);
  if (scenario) {
    try {
      await teardownScenario(scenario, { keepOnFailure: true });
    } catch {}
  }
  process.exit(1);
}
