import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runStaticGate, setupScenario, teardownScenario, getSessionId } from '../index.js';
import { WATCHDOG_TIMEOUT_MS } from '../watchdog-constants.js';
import { bindLaneSession, expectationLane } from './lane.mjs';
import { requestRoleOf } from '../strict-mock-matches.js';

const __filename = fileURLToPath(import.meta.url);
const BLOGGER_MARKER = 'You are the blogger of a coding agent session.';
const primaryRole = 'orchestrator';
const primaryTools = ['fork-manager', 'join'];
const forbiddenPrimaryTools = ['read', 'write', 'edit', 'bash', 'glob', 'grep', 'list', 'verdict'];
const contextLimit = 1000;
const longText = 'dense work record sentence. '.repeat(70);

function primaryRequests(provider) {
  return provider.requests.filter((body) => requestRoleOf(body) === primaryRole);
}

function messageText(msg) {
  if (!msg) return '';
  const content = msg?.content || msg?.text || '';
  if (typeof content === 'string') return content;
  if (Array.isArray(content)) return content.map(p => p?.text || '').join('\n');
  return JSON.stringify(content);
}

function messagesSnapshot(messages) {
  // Return a stable representation for prefix comparison:
  // For each message, return [role, firstPartText, id]
  return messages.map(msg => {
    const id = msg?.info?.id || msg?.id || '';
    const role = msg?.role || msg?.info?.role || '';
    const text = messageText(msg);
    return { id, role, text };
  });
}

function longestCommonPrefix(prev, curr) {
  let i = 0;
  while (i < Math.min(prev.length, curr.length)) {
    if (prev[i].id !== curr[i].id || prev[i].role !== curr[i].role || prev[i].text !== curr[i].text) break;
    i++;
  }
  return i;
}

let scenario;
try {
  assert.equal(runStaticGate([__filename]).passed, true);

  scenario = await setupScenario({
    project: { files: { 'AGENTS.md': 'companion cache canary\n' } },
    strict: true,
    contextLimit,
  });

  scenario.provider.expectTitle({
    id: 'primary-title',
    lane: expectationLane('companion-cache', 'primary-title', 'title', 1, 'title'),
  });

  const parent = await scenario.client.request('POST', '/api/session', {
    body: { agent: primaryRole, model: { providerID: 'test', id: 'test-model' } },
  });
  const parentId = getSessionId(parent);
  assert.ok(parentId, `parent creation failed: ${JSON.stringify(parent)}`);
  scenario.sessionIds.push(parentId);
  bindLaneSession(scenario.provider, parentId, 'primary-title', 'primary');

  const capturedSnapshots = [];
  const rounds = 4;

  for (let round = 1; round <= rounds; round++) {
    scenario.provider.expectText({
      id: `round-${round}`,
      lane: expectationLane('companion-cache', 'primary', primaryRole, round),
      text: `round ${round}: ${longText}`,
      match: { requiredTools: primaryTools, forbiddenTools: forbiddenPrimaryTools },
    });
  }
  // One neverEnd blogger absorbs all deltas (busy-skip means not every primary
  // round schedules a blogger run under parallel host load).
  scenario.provider.expectText({
    id: 'session-blogger',
    lane: expectationLane('companion-cache', 'primary-blogger', 'blogger', 1, 'chat', 'primary'),
    blocking: false,
    neverEnd: true,
    text: 'Blogger paragraph.',
    match: { containsText: [BLOGGER_MARKER] },
  });

  for (let round = 1; round <= rounds; round++) {
    const turn = scenario.turn.start(parentId);
    const prompt = await scenario.client.request('POST', `/session/${parentId}/prompt_async`, {
      body: {
        agent: primaryRole,
        parts: [{ type: 'text', text: `Record round ${round}.` }],
        model: { providerID: 'test', modelID: 'test-model' },
      },
    });
    assert.ok(prompt.ok, `round ${round} prompt failed: ${JSON.stringify(prompt.data)}`);
    await turn.awaitTerminal({ timeoutMs: WATCHDOG_TIMEOUT_MS, requireActivity: true, requireAssistantTerminal: false, requireIdleAfterActivity: true });
    scenario.watchdog?.advance({ reason: `round-${round}-terminal`, lane: 'primary', blocking: true });

    // Capture the primary provider request for prefix analysis
    const reqs = primaryRequests(scenario.provider);
    if (reqs.length > 0) {
      const lastReq = reqs[reqs.length - 1];
      if (lastReq?.messages) {
        capturedSnapshots.push({
          round,
          messages: messagesSnapshot(lastReq.messages),
          count: lastReq.messages.length,
        });
      }
    }
  }

  // Analyze prefix invariance across rounds where B should be stable
  assert.ok(capturedSnapshots.length >= 2, `Need at least 2 captured snapshots, got ${capturedSnapshots.length}`);

  // The first captured snapshot is the initial request (no companion-b-head yet).
  // Later snapshots should have companion-b-head as the first user message
  // (if prefix replacement was activated by the budget threshold).

  // Find the snapshot where companion-b-head first appears (epoch frozen)
  const epochStart = capturedSnapshots.findIndex(s =>
    s.messages.some(m => ((m.id === 'companion-b-head' || (typeof m.id === 'string' && m.id.startsWith('companion-b-head-'))) || (typeof m.id === 'string' && m.id.startsWith('companion-b-head-'))))
  );

  if (epochStart >= 0) {
    // From epochStart+1 onward, the prefix (including companion-b-head) must be stable
    for (let i = epochStart + 1; i < capturedSnapshots.length; i++) {
      const prev = capturedSnapshots[i - 1];
      const curr = capturedSnapshots[i];
      const common = longestCommonPrefix(prev.messages, curr.messages);

      // The companion-b-head message (and everything before it) must be identical
      const bHeadIdxPrev = prev.messages.findIndex(m => ((m.id === 'companion-b-head' || (typeof m.id === 'string' && m.id.startsWith('companion-b-head-'))) || (typeof m.id === 'string' && m.id.startsWith('companion-b-head-'))));
      const bHeadIdxCurr = curr.messages.findIndex(m => ((m.id === 'companion-b-head' || (typeof m.id === 'string' && m.id.startsWith('companion-b-head-'))) || (typeof m.id === 'string' && m.id.startsWith('companion-b-head-'))));

      // Both must have companion-b-head at the same index with the same content
      assert.equal(
        bHeadIdxCurr,
        bHeadIdxPrev,
        `companion-b-head must appear at the same index across epochs (round ${curr.round} idx ${bHeadIdxCurr} vs round ${prev.round} idx ${bHeadIdxPrev})`,
      );

      const headContent = curr.messages[bHeadIdxCurr].text;
      const prevHeadContent = prev.messages[bHeadIdxPrev].text;
      assert.equal(
        headContent,
        prevHeadContent,
        `FrozenB must NOT change across Blogger rounds: round ${curr.round} != round ${prev.round}`,
      );

      // The entire prefix up to and including companion-b-head must be identical
      assert.ok(
        common >= bHeadIdxPrev + 1,
        `Prefix must include companion-b-head (common ${common} >= ${bHeadIdxPrev + 1}) for round ${curr.round}`,
      );

      console.log(`  ✓ Round ${curr.round}: prefix stable (common ${common}/${curr.messages.length} messages, B head idx ${bHeadIdxCurr})`);
    }
  } else {
    // Provider-visible OpenAI shape may strip synthetic info.id. Detect epoch by
    // a sudden shrink (B-head replacement shortens the list), then require the
    // shortened prefix to stay stable afterwards.
    const shrinkAt = capturedSnapshots.findIndex((s, i) =>
      i > 0 && s.count < capturedSnapshots[i - 1].count);
    if (shrinkAt > 0) {
      console.log(`  ℹ Epoch inferred by message-count shrink at round ${capturedSnapshots[shrinkAt].round}`);
      // Within the same epoch (no further shrink), the shortened prefix is stable.
      // A later shrink is a new SSOT epoch switch (FrozenB re-frozen from LatestB).
      for (let i = shrinkAt + 1; i < capturedSnapshots.length; i++) {
        const prev = capturedSnapshots[i - 1];
        const curr = capturedSnapshots[i];
        if (curr.count < prev.count) {
          console.log(`  ℹ Round ${curr.round}: subsequent epoch switch (count ${prev.count} → ${curr.count})`);
          continue;
        }
        const n = Math.min(prev.messages.length, curr.messages.length);
        for (let j = 0; j < n - 1; j++) {
          // Compare all but the trailing current-user turn (may append).
          assert.equal(
            JSON.stringify(curr.messages[j]),
            JSON.stringify(prev.messages[j]),
            `within-epoch prefix frozen at idx ${j} round ${curr.round}`,
          );
        }
        console.log(`  ✓ Round ${curr.round}: within-epoch prefix stable (${n} messages)`);
      }
    } else {
      console.log('ℹ Prefix replacement did not activate within test rounds (budget may not be crossed)');
      for (let i = 1; i < capturedSnapshots.length; i++) {
        const prev = capturedSnapshots[i - 1];
        const curr = capturedSnapshots[i];
        assert.ok(
          curr.count >= prev.count,
          `Messages must be append-only before epoch: round ${curr.round} count ${curr.count} >= round ${prev.round} count ${prev.count}`,
        );
      }
    }
  }

  // Verify the idempotency guard: no duplicate companion-b-head in any request
  for (const snap of capturedSnapshots) {
    const bHeadCount = snap.messages.filter(m => ((m.id === 'companion-b-head' || (typeof m.id === 'string' && m.id.startsWith('companion-b-head-'))) || (typeof m.id === 'string' && m.id.startsWith('companion-b-head-')))).length;
    assert.ok(
      bHeadCount <= 1,
      `Round ${snap.round} must have at most 1 companion-b-head (found ${bHeadCount})`,
    );
  }

  scenario.provider.expectSatisfied();
  await teardownScenario(scenario);
  console.log('Companion cache canary passed: prefix stable across Blogger rounds, no duplicate B head.');
} catch (error) {
  console.error(`Companion cache canary failed: ${error.stack || error}`);
  if (scenario?.host?.workDir) console.error(`workDir: ${scenario.host.workDir}`);
  if (scenario?.provider?.unexpectedRequests) {
    console.error('unexpected:', JSON.stringify(scenario.provider.unexpectedRequests.slice(0, 3).map(r => ({ reason: r.reason, candidates: r.candidates }))));
  }
  if (scenario) {
    try { await teardownScenario(scenario, { keepOnFailure: true }); } catch {}
  }
  process.exit(1);
}
