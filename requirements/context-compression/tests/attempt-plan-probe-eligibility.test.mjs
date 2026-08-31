// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: context-compression.
//
// CTX-010 probe eligibility: only an armed work-main slot may carry a prefix
// probe. A slot that may not recover never asks; a Companion request never asks
// even when armed; an armed work-main carries the probe it selected; a refused
// candidate falls back to the committed epoch with the reason recorded.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as planner from '../../../dist/Context/Companion/CompressionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

const requestKind = prefix.requestKind

const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) => ({
  probeId: id,
  basedOnEpoch: 0,
  candidate: snapshotAt(cutoff),
})

// ── CTX-010: only a work main request carries a probe ─────────────────────

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_a_non_recovery_slot_never_asks_for_a_probe', () => {
  // `selectProbe` throws if called. A slot that may not recover must not pay for a
  // digest recomputation or a blob read to discover it has nothing to do.
  const plan = planner.attemptPlan({ kind: requestKind.workMain, mayRecover: false })

  assert.equal(plan.choice, 'UseCommittedEpoch')
  assert.equal(plan.probeId, null)
  assert.equal(plan.noProbeReason, null, 'not asking is not a refusal')
})

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_a_companion_request_never_asks_for_a_probe_even_when_armed', () => {
  // Enforced in the planner, not left to the caller. A Companion request has no prefix
  // to probe — its history is the frame sequence — and a repair reuses whatever the
  // attempt it repairs already sent.
  for (const kind of [
    requestKind.bloggerMain,
    requestKind.bloggerSquash,
    requestKind.interactionRepair,
    requestKind.strengthReplica,
  ]) {
    const plan = planner.attemptPlan({ kind, mayRecover: true })

    assert.equal(plan.choice, 'UseCommittedEpoch', `${kind} must not carry a probe`)
    assert.equal(plan.probeId, null)
  }
})

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_an_armed_work_main_carries_the_probe_it_selected', () => {
  const plan = planner.attemptPlan({
    kind: requestKind.workMain,
    mayRecover: true,
    probe: probeFor({ id: 'probe-abc' }),
  })

  assert.equal(plan.choice, 'UsePrefixProbe')
  assert.equal(plan.probeId, 'probe-abc')
  assert.equal(plan.noProbeReason, null)
})

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_invalid_role_tier_and_kind_fail_closed', () => {
  for (const input of [{ role: 'unknown' }, { tier: 'unknown' }, { kind: 'unknown' }]) {
    const result = planner.attemptPlan(input)
    assert.equal(result.ok, false)
    assert.match(result.error, /unknown (role|tier|request kind)/)
  }
})

test('WHAT[CONTEXT-COMPRESSION-009] CTX_011_a_refused_candidate_falls_back_to_the_committed_epoch_with_a_reason', () => {
  // The ordinary outcome when an armed slot has nothing to work with. The request still
  // goes out; only the reason is recorded, for diagnostics.
  const plan = planner.attemptPlan({
    kind: requestKind.workMain,
    mayRecover: true,
    noCandidateReason: 'NoCoverage',
  })

  assert.equal(plan.choice, 'UseCommittedEpoch')
  assert.equal(plan.probeId, null)
  assert.equal(plan.noProbeReason, 'NoCoverage')
})
