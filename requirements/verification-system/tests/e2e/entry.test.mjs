/**
 * The Long Stroke — sole top-level E2E entry (G4R-3 / changes/active/test.md).
 *
 * Scenario: scenarios/long-stroke.toml
 * Oracles:  support/long-stroke-oracles.mjs
 *
 * NOT registered under cases/ — G4R-0 freeze forbids growing the multi-canary
 * ceiling; this is the required-exactly-one-when-present cutover path
 * (g4r-freeze gate retired 2026-08-14; the e2e-watchdog-feed gate keeps the
 * sole-entry scope).
 *
 * PHYSICAL CONTRACTS (VERIFICATION-SYSTEM-003): this file is the sole Long Stroke
 * because it depends on Host facts Pure/Temporal/Adapter cannot simulate:
 *   1. OpenCode process lifetime — spawn count === 1, one serve, one journal writer
 *   2. Host-assigned assistant messageID persisted before transform, then
 *      ToolContext.messageID at execute (HOST-010 共时)
 *   3. Host ToolPart / idle / abort / child-session physical threading
 * Repeat-until-pass is forbidden; semantic branches stay at Pure/Temporal/Adapter.
 *
 * G4R §2 / Exit: one continuous OpenCode lifetime — spawn count must be exactly 1.
 *
 * Real-host Magic Todo canaries A/E/G/H: a test-only wrapper plugin observes
 * the production membrane in the sole serve lifetime without changing its
 * definition, args, or result bytes.
 */
import assert from 'node:assert/strict';
import './support/env-pin.mjs';
import { fileURLToPath } from 'node:url';
import { runCanary } from './support/scenario-driver.mjs';
import { bindLaneSession } from './support/lane.mjs';
import { getSessionId } from './support/scenario-http.js';
import { runStaticGate } from './support/index.js';
import {
  CUSTOMS,
  G2_INSPECTOR_CANARY_PROMPT,
  G6_CANONICAL_A,
  G6_CANONICAL_Q,
  HUMANROOT_MANAGER_LOOP_CANARY_PROMPT,
  assertHumanRootManagerLoop,
  retireCompanionForDeletion,
  assertG2InspectorBatchCoalescing,
  assertG2InspectorPrefixLaw,
  assertG6BookkeeperFinalize,
  extractInspectorIdFromOwnerRequests,
} from './support/long-stroke-oracles.mjs';
import { countFactCase, factPayloads, readJournal, getOrCreateSharedObserver } from './support/journal-observer.js';
import { shelfmarkFor as casebookShelfmarkFor } from '../../../../dist/Repository/Knowledge/Casebook/IndexSurface.js';
import { WAIT_FACT_WINDOW_MS } from './support/time-budget.js';
import {
  getOpencodeSpawnCount,
  resetOpencodeSpawnCount,
} from './support/process-host-utils.js';
import {
  assertMagicTodoHostCanariesAEGH,
  collectManagerProviderToolEvidence,
} from './support/magic-todo-host-canary-plugin.mjs';

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('long-stroke entry static gate failed');
}

const STRENGTH_HOST_CANARY_PROMPT =
  'STRENGTH_HOST_CANARY: inspect README.md through the real nested Replica path.';

const runPreFlowPrompt = async (scenario, lane, prompt, agent) => {
  const created = await scenario.client.createSession(agent ? { agent } : {});
  const sessionID = getSessionId(created);
  assert.ok(sessionID, `${lane} session creation failed: ${JSON.stringify(created)}`);
  if (!scenario.sessionIds.includes(sessionID)) scenario.sessionIds.push(sessionID);
  bindLaneSession(scenario.provider, sessionID, lane);

  const turn = scenario.turn.start(sessionID);
  const response = await scenario.client.request('POST', `/session/${sessionID}/prompt_async`, {
    body: {
      parts: [{ type: 'text', text: prompt }],
      ...(agent ? { agent } : {}),
    },
  });
  assert.ok(response.ok, `${lane} prompt failed: ${JSON.stringify(response.data)}`);
  await turn.awaitTerminal();
};

const waitCaptured = async (scenario) => {
  const observer = getOrCreateSharedObserver(scenario.host.workDir);
  await observer.refresh();
  const posBefore = observer.position();
  const existing = observer.select({ caseName: 'InspectorCaseCaptured' });
  if (existing.length >= 1) return;

  const deadline = Date.now() + WAIT_FACT_WINDOW_MS;
  while (Date.now() < deadline) {
    const remaining = Math.max(1, deadline - Date.now());
    await new Promise((resolve) => {
      let settled = false;
      let unsub = () => {};
      let delay = null;
      const done = () => {
        if (settled) return;
        settled = true;
        try { unsub(); } catch {}
        try { delay?.cancel?.(); } catch {}
        resolve();
      };
      unsub = observer.subscribe(() => {
        const found = observer.select({ caseName: 'InspectorCaseCaptured', after: posBefore });
        if (found.length >= 1) done();
      });
      // Check once right after subscribe to prevent missing notification in between
      const foundNow = observer.select({ caseName: 'InspectorCaseCaptured', after: posBefore });
      if (foundNow.length >= 1) {
        done();
        return;
      }
      const delayPort = scenario.delayPort ?? { delay: (ms) => new Promise((res) => setTimeout(res, ms)) };
      delay = delayPort.delay(Math.min(remaining, 50));
      delay.then(done);
    });

    await observer.refresh();
    const captured = observer.select({ caseName: 'InspectorCaseCaptured', after: posBefore });
    if (captured.length >= 1) return;
  }
  throw new Error('G6: InspectorCaseCaptured did not land after owner session.deleted');
};

const preFlowCanaries = async (scenario) => {
  await CUSTOMS.bindManagerLoopSequence(scenario);
  await runPreFlowPrompt(scenario, 'strength-canary-owner', STRENGTH_HOST_CANARY_PROMPT, 'coder');

  assert.equal(
    scenario.provider.matchCount('strength-canary-replica.0'),
    1,
    `Strength dry-run must physically start its Replica without blocking the owner. Host stderr tail:\n${scenario.host.stderrLog.slice(-4000)}`,
  );

  // SPEC-INV-003/013: request #2 is a legitimate race tail. A nonblocking
  // DryRun may reach it before the owner's exact target terminal, or the owner
  // may causally close the observation horizon first. Do not wait for either
  // ordering. The optional scripted step answers #2 if it races through; an
  // undeclared request #3 remains fatal under strict scenario matching. The
  // exact K2 "allow #2, block #3" state-machine law is proved without clocks in
  // speculative-investigation/replica-transform.test.mjs.

  scenario.provider._state.rewriteToolArgs = (entry, args) => {
    if (entry?.turnId === 'coder' && args?.shelfmark === '$inspector-case') {
      const inspectorId = extractInspectorIdFromOwnerRequests(scenario.provider.requests);
      assert.ok(inspectorId, 'G6 fetch rewrite needs the Inspector durable identity to derive its shelfmark');
      return { shelfmark: casebookShelfmarkFor(inspectorId, G6_CANONICAL_Q) };
    }
    return undefined;
  };

  const inspectorOwner = await scenario.client.createSession({ title: 'G2 inspector owner' });
  const inspectorOwnerId = getSessionId(inspectorOwner);
  assert.ok(inspectorOwnerId, `g2-inspector-owner session creation failed: ${JSON.stringify(inspectorOwner)}`);
  if (!scenario.sessionIds.includes(inspectorOwnerId)) scenario.sessionIds.push(inspectorOwnerId);
  bindLaneSession(scenario.provider, inspectorOwnerId, 'g2-inspector-owner');

  const inspectorTurn = scenario.turn.start(inspectorOwnerId);
  const inspectorPrompt = await scenario.client.request('POST', `/session/${inspectorOwnerId}/prompt_async`, {
    body: {
      messageID: 'msg-g2-inspector-owner',
      parts: [{ type: 'text', text: G2_INSPECTOR_CANARY_PROMPT }],
      agent: 'coder',
    },
  });
  assert.ok(inspectorPrompt.ok, `g2 inspector prompt failed: ${JSON.stringify(inspectorPrompt.data)}`);
  await inspectorTurn.awaitTerminal();

  const g2 = assertG2InspectorPrefixLaw(scenario);
  scenario.g6InspectorSessionId = g2.inspectorSessionId;
  assertG2InspectorBatchCoalescing(scenario, g2.inspectorSessionId);
  await Promise.all([
    retireCompanionForDeletion(scenario, inspectorOwnerId),
    retireCompanionForDeletion(scenario, g2.inspectorSessionId),
  ]);

  const deleted = await scenario.client.deleteSession(inspectorOwnerId);
  assert.ok(deleted.ok, `G6 owner session.deleted failed: ${JSON.stringify(deleted.data)}`);
  await waitCaptured(scenario);
  assertG6BookkeeperFinalize(scenario);

  // HumanRoot manager loop canary (sole serve, before orchestrator main flow).
  // Direct HumanRoot Manager — the main spine uses AgentOwnerRoot. First iteration
  // reviews one dimension low then retires with Outcome Continue; the next
  // iteration must arrive as another ordinary IncumbencyOpened event plus a
  // physically observed provider request (not merely the fact) on the same
  // SessionId/LogicalRun with the same typed authority user messages, then reviews
  // 8×10 and retires with Outcome Accepted.
  const humanrootCreated = await scenario.client.createSession({ agent: 'manager' });
  const humanrootSessionId = getSessionId(humanrootCreated);
  assert.ok(humanrootSessionId, `humanroot-manager session creation failed: ${JSON.stringify(humanrootCreated)}`);
  if (!scenario.sessionIds.includes(humanrootSessionId)) scenario.sessionIds.push(humanrootSessionId);
  bindLaneSession(scenario.provider, humanrootSessionId, 'humanroot-manager');

  const humanrootPrompt = await scenario.client.request('POST', `/session/${humanrootSessionId}/prompt_async`, {
    body: {
      parts: [{ type: 'text', text: HUMANROOT_MANAGER_LOOP_CANARY_PROMPT }],
      agent: 'manager',
    },
  });
  assert.ok(humanrootPrompt.ok, `humanroot-manager prompt failed: ${JSON.stringify(humanrootPrompt.data)}`);

  // ONE reusable humanroot-loop family: each step delivered twice (initial +
  // next iteration). Barrier the second delivery, not distinct trigger ids.
  for (const id of ['humanroot-loop.0', 'humanroot-loop.1']) {
    await scenario.provider.waitForExpectationAttempt(id, 2, WAIT_FACT_WINDOW_MS);
  }
  await assertHumanRootManagerLoop(scenario, humanrootSessionId);

  const linkedBlogger = factPayloads(scenario.host.workDir, 'CompanionBloggerLinked')
    .filter((payload) => {
      const text = JSON.stringify(payload ?? {});
      return text.includes(humanrootSessionId);
    });
  if (linkedBlogger.length > 0) {
    await retireCompanionForDeletion(scenario, humanrootSessionId);
  }
};

const awaitManagerJoinRunning = async (scenario, ctx) => {
  const managerSessionId = ctx?.childId;
  assert.ok(managerSessionId, 'Long Stroke join-running barrier requires the bound Manager session');

  const isRunningJoin = (event) =>
    event?.type === 'message.part.updated'
    && event?.sessionID === managerSessionId
    && event?.toolName === 'join'
    && event?.toolStatus === 'running';

  await scenario.events.awaitEvent(isRunningJoin, null);
};

const assertG6ColdFetch = async (scenario) => {
  const fetchResults = (scenario.provider.requests ?? [])
    .flatMap((request) => request?.messages ?? [])
    .filter((message) => message?.role === 'tool' || message?.role === 'toolResult')
    .map((message) => String(message?.content ?? ''));
  assert.ok(
    fetchResults.some(
      (text) =>
        text.includes('No change was found in the evidence this answer depended on.')
        && text.includes(G6_CANONICAL_A),
    ),
    `G6 fetch must return the no-change consequence plus canonical A from the later Coder session; inspector=${scenario.g6InspectorSessionId ?? 'unknown'} results=${JSON.stringify(fetchResults).slice(0, 1200)}`,
  );
};

/**
 * Long-stroke custom that freezes real-host A/E/G/H from pure-observer wrapper
 * artifacts after the adversity spine and before expectSatisfied / teardown.
 * Wired only via CUSTOMS so the sole entry remains the assertion owner.
 */
const assertHostCanariesAEGH = async (scenario, ctx) => {
  const dir = scenario.magicTodoHostCanaryDirectory;
  assert.ok(
    dir,
    'HOST_CANARY: scenario.magicTodoHostCanaryDirectory missing — setup.magicTodoHostCanary must be true',
  );
  const managerProviderWire = collectManagerProviderToolEvidence(scenario, {
    childSessionId: ctx?.childId ?? null,
  });
  const result = assertMagicTodoHostCanariesAEGH(dir, {
    managerProviderWire,
    xTraceParts: factPayloads(scenario.host.workDir, 'XTracePartAppended'),
  });
  assert.equal(result.ok, true, 'HOST_CANARY A/E/G/H must pass');
  console.log(
    `[host-canary] A/E/G/H ok session=${result.canaries.H.sessionID} call=${result.canaries.H.callID} ` +
      `statusDuringAfter=${result.canaries.G.toolPartStatusDuringAfter}`,
  );
};

resetOpencodeSpawnCount();
const code = await runCanary('long-stroke', {
  preFlow: preFlowCanaries,
  customs: {
    ...CUSTOMS,
    awaitManagerJoinRunning,
    assertG6ColdFetch,
    assertHostCanariesAEGH,
  },
});
assert.equal(
  getOpencodeSpawnCount(),
  1,
  `G4R §2: Long Stroke must spawn opencode serve exactly once (got ${getOpencodeSpawnCount()})`,
);
process.exit(code);
