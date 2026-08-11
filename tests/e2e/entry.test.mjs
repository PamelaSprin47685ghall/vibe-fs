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

/**
 * Long-stroke custom that freezes real-host A/E/G/H from wrapper artifacts
 * after the adversity spine and before expectSatisfied / teardown.
 * Wired only via CUSTOMS so the sole entry remains the assertion owner.
 *
 * When Manager provider wire never advertises `todowrite`, the canary result is
 * the precise Host unavailability (not a hang on an unreachable manager.0 step).
 */
const assertHostCanariesAEGH = async (scenario, ctx) => {
  const dir = scenario.magicTodoHostCanaryDirectory;
  assert.ok(
    dir,
    'HOST_CANARY: scenario.magicTodoHostCanaryDirectory missing — setup.magicTodoHostCanary must be true',
  );
  const providerWire = collectManagerProviderToolEvidence(scenario, {
    childSessionId: ctx?.childId ?? null,
  });
  const result = assertMagicTodoHostCanariesAEGH(dir, { providerWire });
  if (result.ok !== true) {
    assert.equal(
      result.reason,
      'todowrite-not-advertised-on-manager',
      `HOST_CANARY unexpected failure: ${result.message ?? JSON.stringify(result)}`,
    );
    console.log(
      `[host-canary] V2 todowrite unavailable on Manager provider wire — fail-closed canary: ` +
        `${result.message}`,
    );
    // Contract: precise runtime failure is the canary result (not hang, not silent pass).
    throw new Error(result.message);
  }
  assert.equal(result.ok, true, 'HOST_CANARY A/E/G/H must pass');
  console.log(
    `[host-canary] A/E/G/H ok session=${result.canaries.H.sessionID} call=${result.canaries.H.callID} ` +
      `statusDuringAfter=${result.canaries.G.toolPartStatusDuringAfter} ` +
      `carrierFailClosed=${result.canaries.H.carrier.carrierFailClosed}`,
  );
};

resetOpencodeSpawnCount();
const code = await runCanary('long-stroke', {
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
