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
 * Phase 0 real-host Magic Todo canaries A/E/G/H: test-only wrapper plugin
 * (magic-todo-host-canary-plugin.mjs) overlays the production plugin for this
 * sole serve lifetime; entry asserts wrapper artifacts after flow, before
 * teardown (no second host, no production membrane).
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
} from './support/long-stroke-oracles.mjs';
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

const NATIVE_TODO_CANARY_PROMPT =
  'NATIVE_TODO_CANARY: exercise the default build session todowrite hook.';
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

const preFlowNativeTodoCanary = async (scenario) => {
  await runPreFlowPrompt(scenario, 'native-todo-canary', NATIVE_TODO_CANARY_PROMPT);
  await runPreFlowPrompt(scenario, 'strength-canary-owner', STRENGTH_HOST_CANARY_PROMPT, 'deep-coder');

  const replicaDeliveries = scenario.provider.matchCount('strength-canary-replica.0');
  assert.equal(
    replicaDeliveries,
    1,
    `Strength dry-run must issue exactly one real Replica provider request. Host stderr tail:\n${scenario.host.stderrLog.slice(-4000)}`,
  );
};

/**
 * Long-stroke custom that freezes real-host A/E/G/H from wrapper artifacts
 * after the adversity spine and before expectSatisfied / teardown.
 * Wired only via CUSTOMS so the sole entry remains the assertion owner.
 * Manager non-advertisement is checked independently of native canary artifacts.
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
  const result = assertMagicTodoHostCanariesAEGH(dir, { managerProviderWire });
  assert.equal(result.ok, true, 'HOST_CANARY A/E/G/H must pass');
  console.log(
    `[host-canary] A/E/G/H ok session=${result.canaries.H.sessionID} call=${result.canaries.H.callID} ` +
      `statusDuringAfter=${result.canaries.G.toolPartStatusDuringAfter}`,
  );
};

resetOpencodeSpawnCount();
const code = await runCanary('long-stroke', {
  preFlow: preFlowNativeTodoCanary,
  customs: {
    ...CUSTOMS,
    assertHostCanariesAEGH,
  },
});
assert.equal(
  getOpencodeSpawnCount(),
  1,
  `G4R §2: Long Stroke must spawn opencode serve exactly once (got ${getOpencodeSpawnCount()})`,
);
process.exit(code);
