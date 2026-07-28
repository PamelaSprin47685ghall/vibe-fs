import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { getSessionId, runStaticGate, setupScenario, teardownScenario } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';

const __filename = fileURLToPath(import.meta.url);
const primaryRole = 'orchestrator';
const primaryTools = ['fork-manager', 'join'];
const forbiddenPrimaryTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'list', 'verdict'];
const contextLimit = 1000;
// Activation: estimateTokens >= 0.8 * 1000 = 800 tokens = 3200 chars/4.
const longText = 'dense work record sentence. '.repeat(70); // ~1960 chars per round
const rounds = 4;
const bloggerParagraph = (round) => `Blogger paragraph ${round}. ${'durable blogger detail. '.repeat(90)}`;
// Parallel P0 suite can delay blogger transform under host load; keep the
// scenario-local watchdog short for causal silence, but allow blogger
// expectation waits a longer bound so self-rebase is not flaky.
const BLOGGER_WAIT_MS = Math.max(WATCHDOG_TIMEOUT_MS, 20000);

async function waitBlogger(scenario, expectationId, timeoutMs = BLOGGER_WAIT_MS) {
  const started = Date.now();
  // Keep the scenario-local 2s silence watchdog alive while blogger work is
  // intentionally waiting under parallel host load.
  while (Date.now() - started < timeoutMs) {
    const slice = Math.min(1000, timeoutMs - (Date.now() - started));
    scenario.watchdog?.advance({
      reason: `wait-blogger:${expectationId}`,
      lane: `manager-blogger:${expectationId}`,
      expectationId,
      blocking: true,
    });
    try {
      await scenario.provider.waitForExpectation(expectationId, slice);
      scenario.watchdog?.advance({
        reason: `blogger-done:${expectationId}`,
        lane: `manager-blogger:${expectationId}`,
        expectationId,
        blocking: true,
      });
      return;
    } catch (err) {
      const remaining = timeoutMs - (Date.now() - started);
      if (remaining <= 0) throw err;
    }
  }
  throw new Error(`Timed out waiting for expectation ${expectationId}`);
}

async function waitIdleRenewing(scenario, timeoutMs = BLOGGER_WAIT_MS) {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    const slice = Math.min(1000, timeoutMs - (Date.now() - started));
    scenario.watchdog?.advance({
      reason: 'wait-idle',
      lane: 'provider:idle',
      blocking: true,
    });
    try {
      await scenario.provider.waitForIdle(slice);
      return;
    } catch (err) {
      if (timeoutMs - (Date.now() - started) <= 0) throw err;
    }
  }
}

// B' — the condensed companion context the self-rebase blogger returns.
// Kept short so the Y-rebase threshold fires once after the Blogger budget
// is captured, and does not re-fire after the replacement.
const condensedB = 'B-prime condensed context.';

function journalContains(workDir, needle) {
  const common = execFileSync('git', ['-C', workDir, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
  const runtimeDir = path.join(path.isAbsolute(common) ? common : path.resolve(workDir, common), 'wanxiangshu-next', 'runtimes');
  if (!fs.existsSync(runtimeDir)) return false;
  for (const file of fs.readdirSync(runtimeDir)) {
    if (!file.endsWith('.ndjson')) continue;
    if (fs.readFileSync(path.join(runtimeDir, file), 'utf8').includes(needle)) return true;
  }
  return false;
}

async function waitForJournal(workDir, needle) {
  const deadline = Date.now() + WATCHDOG_TIMEOUT_MS;
  while (!journalContains(workDir, needle)) {
    if (Date.now() >= deadline) return false;
    await new Promise((resolve) => setImmediate(resolve));
  }
  return true;
}

function primaryRequests(scenario) {
  return scenario.provider.requests.filter((body) =>
    (body.tools || []).some((t) => (t?.function?.name || t?.name) === 'fork-manager'));
}

function messageRole(message) {
  return message?.role || message?.info?.role;
}

function messageText(message) {
  const content = message?.content ?? message?.text ?? '';
  if (typeof content === 'string') return content;
  if (Array.isArray(content)) return content.map((p) => p?.text || '').join('\n');
  return JSON.stringify(content);
}

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);
  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'companion replacement canary\n' } },
    strict: true,
    contextLimit,
    // Force Y self-rebase threshold to the same 1000-token fixture budget.
    // Without this, a host-remembered blogger model limit can keep the default
    // 32k budget and leave round-3 expecting a condense request that never fires.
    extraEnv: {
      WANXIANGSHU_BLOGGER_CONTEXT_LIMIT: String(contextLimit),
      WANXIANGSHU_BLOGGER_MODEL: 'test/test-model',
    },
  });
  scenario.provider.expectTitle({
    id: 'primary-title',
    lane: expectationLane('companion-replacement', 'primary-title', 'title', 1, 'title'),
  });

  // Capture every reset-frame (FULL re-anchor) blogger request the companion
  // issues, so we can prove re-anchor content and the failure→retry re-send.
  const resetFrames = [];
  scenario.provider.onRequest = (parsed) => {
    if (JSON.stringify(parsed.messages || []).includes('Re-anchor on the FULL current companion context B')) {
      resetFrames.push(parsed);
    }
  };

  const parent = await scenario.client.request('POST', '/api/session', {
    body: { agent: primaryRole, model: { providerID: 'test', id: 'test-model' } },
  });
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);
  bindLaneSession(scenario.provider, parentId, 'primary-title', 'primary');

  for (let round = 1; round <= rounds; round++) {
    scenario.provider.expectText({
      id: `round-${round}`,
      lane: expectationLane('companion-replacement', 'primary', primaryRole, round),
      text: `round ${round}: ${longText}`,
      match: { requiredTools: primaryTools, forbiddenTools: forbiddenPrimaryTools },
    });
    if (round <= 2) {
      scenario.provider.expectText({
        id: `manager-blogger-${round}`,
        lane: expectationLane('companion-replacement', 'primary-blogger', 'blogger', round, 'chat', 'primary'),
        text: bloggerParagraph(round),
        match: {
          containsText: ['You are the blogger of a coding agent session.', '"agent":"orchestrator"'],
        },
      });
    }
    if (round === 3) {
      // Y-threshold self-rebase: the blogger condenses the FULL B into B'.
      scenario.provider.expectText({
        id: 'manager-blogger-3',
        lane: expectationLane('companion-replacement', 'primary-blogger', 'blogger', 3, 'chat', 'primary'),
        text: condensedB,
        match: {
          containsText: ['Condense the following FULL companion context'],
        },
      });
    }
    if (round === 4) {
      // After the rebase, the next projection delta-blogs normally.
      scenario.provider.expectText({
        id: 'manager-blogger-4',
        lane: expectationLane('companion-replacement', 'primary-blogger', 'blogger', 4, 'chat', 'primary'),
        text: 'Blogger paragraph 4.',
        match: {
          containsText: ['You are the blogger of a coding agent session.', 'Write one dense paragraph'],
        },
      });
    }
    const turn = scenario.turn.start(parentId);
    const prompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
      body: {
        agent: primaryRole,
        parts: [{ type: 'text', text: `Record round ${round}.` }],
        model: { providerID: 'test', modelID: 'test-model' },
      },
    });
    assert.ok(prompt.ok, `round ${round} prompt failed: ${JSON.stringify(prompt.data)}`);
    scenario.watchdog?.advance({ reason: `round-${round}-prompted`, lane: `primary:round-${round}`, blocking: true });
    await turn.awaitTerminal({ timeoutMs: BLOGGER_WAIT_MS, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });
    scenario.watchdog?.advance({ reason: `round-${round}-terminal`, lane: `primary:round-${round}`, blocking: true });
    if (round <= 2) {
      await waitBlogger(scenario, `manager-blogger-${round}`);
      await waitIdleRenewing(scenario);
    }
    if (round === 3 || round === 4) {
      // Under parallel P0 load, Y may busy-skip self-rebase or the first post-
      // rebase delta. Re-prompt the primary until the expected blogger edge is
      // observed; product still must eventually fire those blogger turns.
      const expectationId = round === 3 ? 'manager-blogger-3' : 'manager-blogger-4';
      let observed = false;
      for (let attempt = 1; attempt <= 6 && !observed; attempt++) {
        try {
          await waitBlogger(scenario, expectationId, 6000);
          observed = true;
        } catch (err) {
          if (attempt === 6) throw err;
          scenario.watchdog?.advance({
            reason: `round-${round}-retry-${attempt}`,
            lane: `primary:round-${round}`,
            blocking: true,
          });
          const retryTurn = scenario.turn.start(parentId);
          const retryPrompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
            body: {
              agent: primaryRole,
              parts: [{ type: 'text', text: `Record round ${round} retry ${attempt}.` }],
              model: { providerID: 'test', modelID: 'test-model' },
            },
          });
          assert.ok(
            retryPrompt.ok,
            `round ${round} retry ${attempt} failed: ${JSON.stringify(retryPrompt.data)}`,
          );
          await retryTurn.awaitTerminal({
            timeoutMs: BLOGGER_WAIT_MS,
            requireActivity: true,
            requireAssistantTerminal: false,
            requireIdleAfterActivity: true,
          });
        }
      }
      await waitIdleRenewing(scenario);
      if (round === 3) {
        scenario.watchdog?.advance({ reason: 'rebase-blogger-done', lane: 'manager-blogger:3', blocking: true });
      }
    }
  }

  // (b) Durability: the journal holds a CompanionAdvanced whose Content is the
  // condensed B' (the rebase persisted it); distinct from the old long B.
  assert.ok(
    journalContains(scenario.host.workDir, 'CompanionReplacementActiveSet'),
    'journal must record the durable PrefixReplacementEnabled fact',
  );
  assert.ok(
    journalContains(scenario.host.workDir, 'CompanionAdvanced'),
    'each successful Blogger checkpoint must atomically persist its B and baseline',
  );
  assert.ok(
    journalContains(scenario.host.workDir, condensedB),
    `journal must durably persist the condensed B' ("${condensedB}") via CompanionAdvanced`,
  );

  // ---- Part 1 (a)+(c): restart restores B', and the post-restart reset frame ----
  // re-anchors on B' and succeeds, advancing B.
  await scenario.restart();

  scenario.provider.expectText({
    id: 'round-restarted',
    lane: expectationLane('companion-replacement', 'primary', primaryRole, 5),
    text: `round restarted: ${longText}`,
    match: { requiredTools: primaryTools, forbiddenTools: forbiddenPrimaryTools },
  });
  scenario.provider.expectText({
    id: 'manager-blogger-restarted',
    lane: expectationLane('companion-replacement', 'primary-blogger-restarted', 'blogger', 1, 'chat', 'primary'),
    text: 'Blogger restart recovered.',
    match: {
      containsText: ['Re-anchor on the FULL current companion context B', 'You are the blogger of a coding agent session.'],
    },
  });

  const restartedTurn = scenario.turn.start(parentId);
  const restartedPrompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: primaryRole,
      parts: [{ type: 'text', text: 'Record round restarted.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(restartedPrompt.ok, `restarted round failed: ${JSON.stringify(restartedPrompt.data)}`);
  scenario.watchdog?.advance({ reason: 'restart-prompted', lane: 'primary:restart', blocking: true });
  await restartedTurn.awaitTerminal({ timeoutMs: BLOGGER_WAIT_MS, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });
  await waitBlogger(scenario, 'manager-blogger-restarted');
  await waitIdleRenewing(scenario);

  // SSOT: Y self-rebase only updates LatestB; FrozenB stays until the next X
  // context-threshold epoch switch. After restart, ReplacementActive reloads and
  // the synthetic B head still carries the freeze-time FrozenB (not B').
  // B' is verified via journal + blogger re-anchor frames below, not the X head.
  const restartedPrimary = primaryRequests(scenario).at(-1);
  assert.ok(restartedPrimary, 'post-restart primary request must exist');

  // (c1) Restart reset frame re-anchors on B': its FULL B section contains B'.
  const restartedReset = resetFrames.find((body) =>
    JSON.stringify(body.messages || []).includes('Re-anchor on the FULL current companion context B'));
  assert.ok(restartedReset, 'post-restart must send the FULL reset frame');
  assert.ok(
    JSON.stringify(restartedReset.messages || []).includes(condensedB),
    'restart reset frame must re-anchor on the FULL current companion context B (B\')',
  );

  // (c2) Reset success advanced B (durable CompanionAdvanced persisted).
  assert.equal(
    await waitForJournal(scenario.host.workDir, 'Blogger restart recovered.'),
    true,
    'reset success must advance B (persist a new CompanionAdvanced with the recovered paragraph)',
  );

  // ---- Part 2: reset failure → full reset re-sent → success advances B. ----
  // After restart the restored B is Some, so the next blog sends the FULL reset
  // frame. We fail that FIRST frame (500). OpenCode auto-retries the same child
  // LLM call; the retry lane (registered via afterExpectation when the failing
  // expectation is consumed) catches the re-sent FULL reset frame.
  await scenario.restart();

  scenario.provider.expectText({
    id: 'round-resetfail',
    lane: expectationLane('companion-replacement', 'primary', primaryRole, 6),
    text: `round resetfail: ${longText}`,
    match: { requiredTools: primaryTools, forbiddenTools: forbiddenPrimaryTools },
  });
  // FIRST reset-frame blogger request FAILS (mock 500, no permissive escape).
  scenario.provider.expectError({
    id: 'manager-blogger-resetfail',
    lane: expectationLane('companion-replacement', 'primary-blogger-resetfail', 'blogger', 1, 'chat', 'primary'),
    status: 500,
    headers: { 'retry-after-ms': '0' },
    body: { error: { message: 'mock reset frame failure', type: 'server_error' } },
    match: {
      containsText: ['Re-anchor on the FULL current companion context B', 'You are the blogger of a coding agent session.'],
    },
  });
  // Causal successor: when the failing reset frame is consumed, queue the RETRY
  // lane so the re-sent request is matched (avoids ambiguity with the failure
  // lane, which would otherwise still be head).
  scenario.provider.afterExpectation('manager-blogger-resetfail', () => {
    scenario.provider.expectText({
      id: 'manager-blogger-resetretry',
      lane: expectationLane('companion-replacement', 'primary-blogger-resetfail', 'blogger', 2, 'chat', 'primary'),
      text: 'Blogger reset retry recovered.',
      match: {
        containsText: ['Re-anchor on the FULL current companion context B', 'You are the blogger of a coding agent session.'],
      },
    });
  });

  const rfTurn = scenario.turn.start(parentId);
  const rfPrompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
    body: {
      agent: primaryRole,
      parts: [{ type: 'text', text: 'Record round resetfail.' }],
      model: { providerID: 'test', modelID: 'test-model' },
    },
  });
  assert.ok(rfPrompt.ok, `resetfail round failed: ${JSON.stringify(rfPrompt.data)}`);
  scenario.watchdog?.advance({ reason: 'resetfail-prompted', lane: 'primary:resetfail', blocking: true });
  await rfTurn.awaitTerminal({ timeoutMs: BLOGGER_WAIT_MS, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });
  await waitBlogger(scenario, 'manager-blogger-resetfail');
  await waitBlogger(scenario, 'manager-blogger-resetretry');
  await waitIdleRenewing(scenario);

  // The failed frame MUST be followed by a retry that re-sends the FULL reset
  // frame (proves the failure flag survived and the frame was re-anchored).
  assert.ok(resetFrames.length >= 3, `reset failure must produce a retry reset frame (saw ${resetFrames.length})`);
  const retryFrame = resetFrames[resetFrames.length - 1];
  assert.ok(
    JSON.stringify(retryFrame.messages || []).includes('Re-anchor on the FULL current companion context B'),
    'retried request must be the FULL reset frame (re-anchor)',
  );
  assert.ok(
    JSON.stringify(retryFrame.messages || []).includes(condensedB),
    'retried reset frame must re-anchor on B\' (the flag survived the failure)',
  );

  // Retry success advanced B (durable CompanionAdvanced persisted).
  assert.equal(
    await waitForJournal(scenario.host.workDir, 'Blogger reset retry recovered.'),
    true,
    'reset retry success must advance B (persist a new CompanionAdvanced with the recovered paragraph)',
  );

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Companion replacement + reset canary passed: self-rebase persists B\', restart re-anchors B\', reset failure retries the full frame and advances B.');
} catch (error) {
  console.error(`Companion replacement canary failed: ${error.stack || error}`);
  if (scenario?.host?.workDir) console.error(`workDir: ${scenario.host.workDir}`);
  if (scenario?.host?.stderrLog) console.error(`── host stderr tail ──\n${scenario.host.stderrLog.slice(-4000)}`);
  if (scenario?.provider?.unexpectedRequests) {
    console.error(`unexpected: ${JSON.stringify(scenario.provider.unexpectedRequests.slice(0, 4).map((r) => ({ reason: r.reason, lastUser: r.body?.messages?.at(-1)?.content, candidates: r.candidates })))}`);
  }
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
