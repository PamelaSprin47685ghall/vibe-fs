/**
 * The Long Stroke — sole top-level E2E entry (G4R-3 / changes/active/test.md).
 *
 * Scenario: scenarios/long-stroke.toml
 * Oracles:  support/long-stroke-oracles.mjs
 *
 * NOT registered under cases/ — G4R-0 freeze forbids growing the multi-canary
 * ceiling; this is the required-exactly-one-when-present cutover path
 * (scripts/checks/g4r-freeze.mjs LONG_STROKE_ENTRY_REL).
 *
 * G4R §2 / Exit: one continuous OpenCode lifetime — spawn count must be exactly 1.
 *
 * Real-host Magic Todo canaries A/E/G/H: a test-only wrapper plugin observes
 * the production membrane in the sole serve lifetime without changing its
 * definition, args, or result bytes.
 *
 * §21 adversity oracles are imported by name so each stroke stays addressable
 * from this sole entry (CUSTOMS drives the scenario flow; ADVERSITY_ORACLES is
 * the per-class import surface for freeze/cutover review).
 */
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import { runCanary } from './support/scenario-driver.mjs';
import { bindLaneSession } from './support/lane.mjs';
import { getSessionId } from './support/scenario-http.js';
import { runStaticGate } from './support/index.js';
import {
  CUSTOMS,
  ADVERSITY_ORACLES,
  ADVERSITY_CHECKLIST,
  G2_INSPECTOR_CANARY_PROMPT,
  G6_CANONICAL_A,
  assertG2InspectorPrefixLaw,
  assertG6BookkeeperFinalize,
  extractInspectorIdFromOwnerRequests,
} from './support/long-stroke-oracles.mjs';
import { countFactCase, factPayloads, readJournal } from './support/journal-observer.js';
import { WAIT_FACT_WINDOW_MS } from './support/time-budget.js';
import {
  getOpencodeSpawnCount,
  resetOpencodeSpawnCount,
} from './support/process-host-utils.js';
import {
  assertMagicTodoHostCanariesAEGH,
  collectManagerProviderToolEvidence,
} from './support/magic-todo-host-canary-plugin.mjs';

// Retain named imports so entry ↔ §21 oracle mapping cannot drift silently.
assert.equal(typeof ADVERSITY_ORACLES.assertProviderTransientFailure, 'function');
assert.equal(typeof ADVERSITY_ORACLES.assertFallbackContinuation, 'function');
assert.equal(typeof ADVERSITY_ORACLES.assertJoinWakePath, 'function');
assert.equal(typeof ADVERSITY_ORACLES.assertInterruptedJoin, 'function');
assert.equal(typeof ADVERSITY_ORACLES.assertReviewerRevise, 'function');
assert.equal(typeof ADVERSITY_ORACLES.assertFinalityTemporarilyBlocked, 'function');
assert.equal(typeof ADVERSITY_ORACLES.assertDurableRecovery, 'function');
assert.equal(typeof ADVERSITY_ORACLES.assertPublishConflict, 'function');
assert.equal(typeof ADVERSITY_ORACLES.assertSuccessfulReconciliation, 'function');
assert.equal(typeof ADVERSITY_ORACLES.assertLaterSuccessfulFinality, 'function');
assert.equal(typeof CUSTOMS.holdChildC1UntilLabor, 'function');
assert.equal(typeof CUSTOMS.bindFinalityReviseThenPerfect, 'function');
assert.equal(typeof CUSTOMS.oracleLongStroke, 'function');
assert.ok(
  ADVERSITY_CHECKLIST.every((row) => row.covered === true && row.oracle && row.injection),
  '§21 ADVERSITY_CHECKLIST must mark every adversity class covered with injection+oracle',
);
assert.equal(typeof assertMagicTodoHostCanariesAEGH, 'function');

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('long-stroke entry static gate failed');
}

const STRENGTH_HOST_CANARY_PROMPT =
  'STRENGTH_HOST_CANARY: inspect README.md through the real nested Replica path.';

const runPreFlowPrompt = async (scenario, lane, prompt, agent) => {
  const created = await scenario.client.createSession({});
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
  const deadline = Date.now() + WAIT_FACT_WINDOW_MS;
  while (Date.now() < deadline) {
    const captured = countFactCase(scenario.host.workDir, 'InspectorCaseCaptured');
    const named = readJournal(scenario.host.workDir, 'InspectorCaseCaptured').named;
    if (captured >= 1 || named >= 1) return;
    await new Promise((resolve) => setImmediate(resolve));
  }
  throw new Error('G6: InspectorCaseCaptured did not land after owner session.deleted');
};

const preFlowCanaries = async (scenario) => {
  await runPreFlowPrompt(scenario, 'strength-canary-owner', STRENGTH_HOST_CANARY_PROMPT, 'deep-coder');

  for (const step of ['strength-canary-replica.0', 'strength-canary-replica.1']) {
    assert.equal(
      scenario.provider.matchCount(step),
      1,
      `Strength K2 dry-run must issue each physical Replica provider request exactly once (${step}). Host stderr tail:\n${scenario.host.stderrLog.slice(-4000)}`,
    );
  }
  assert.equal(
    scenario.provider.matchCount('strength-canary-replica'),
    2,
    'Strength K2 dry-run must stop physically after provider request #2; request #3 is undeclared and fatal',
  );

  scenario.provider._state.rewriteToolArgs = (entry, args) => {
    if (entry?.turnId === 'coder' && args?.session_id === '$inspector') {
      const inspectorId = extractInspectorIdFromOwnerRequests(scenario.provider.requests);
      assert.ok(inspectorId, 'G6 fetch rewrite needs inspector_id from InspectorTool result');
      return { session_id: inspectorId };
    }
    return undefined;
  };

  const inspectorOwner = await scenario.client.createSession({ agent: 'fast-coder' });
  const inspectorOwnerId = getSessionId(inspectorOwner);
  assert.ok(inspectorOwnerId, `g2-inspector-owner session creation failed: ${JSON.stringify(inspectorOwner)}`);
  if (!scenario.sessionIds.includes(inspectorOwnerId)) scenario.sessionIds.push(inspectorOwnerId);
  bindLaneSession(scenario.provider, inspectorOwnerId, 'g2-inspector-owner');

  const inspectorTurn = scenario.turn.start(inspectorOwnerId);
  const inspectorPrompt = await scenario.client.request('POST', `/session/${inspectorOwnerId}/prompt_async`, {
    body: { parts: [{ type: 'text', text: G2_INSPECTOR_CANARY_PROMPT }], agent: 'fast-coder' },
  });
  assert.ok(inspectorPrompt.ok, `g2 inspector prompt failed: ${JSON.stringify(inspectorPrompt.data)}`);
  await inspectorTurn.awaitTerminal();

  const g2 = assertG2InspectorPrefixLaw(scenario);
  scenario.g6InspectorSessionId = g2.inspectorSessionId;

  const deleted = await scenario.client.deleteSession(inspectorOwnerId);
  assert.ok(deleted.ok, `G6 owner session.deleted failed: ${JSON.stringify(deleted.data)}`);
  await waitCaptured(scenario);
  assertG6BookkeeperFinalize(scenario);
};

const assertG6ColdFetch = async (scenario) => {
  const fetchResults = (scenario.provider.requests ?? [])
    .flatMap((request) => request?.messages ?? [])
    .filter((message) => message?.role === 'tool' || message?.role === 'toolResult')
    .map((message) => String(message?.content ?? ''));
  assert.ok(
    fetchResults.some((text) => text.includes('fresh') && text.includes(G6_CANONICAL_A)),
    `G6 fetch must return fresh canonical A from the later Coder session; inspector=${scenario.g6InspectorSessionId ?? 'unknown'} results=${JSON.stringify(fetchResults).slice(0, 800)}`,
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
