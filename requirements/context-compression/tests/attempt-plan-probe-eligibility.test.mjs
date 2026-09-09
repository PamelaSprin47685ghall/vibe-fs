// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: context-compression.
//
// CTX-010 probe eligibility: only a probe-allowed work-main attempt may carry a
// prefix probe. A denied policy never asks; a Companion request never asks
// even when allowed; an allowed work-main carries the probe it selected; a refused
// candidate falls back to the committed epoch with the reason recorded.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as planner from '../../../dist/Context/Companion/CompressionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as xwire from '../../../dist/Context/Prefix/XWireSurface.js'
import { budget } from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

test('WHAT[CONTEXT-COMPRESSION-002] successful retry tool steps keep the committed prefix despite new coverage', () => {
  const projection = {
    messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'opening' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'first result' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'second result' }] },
    ],
  }
  const failed = budget.recordFailure(budget.initial)
  const input = {
    journal: true,
    sessionId: 'retry-session',
    acceptedRetry: true,
    physicalUser: 'retry-user',
    acceptedPhysicalUser: 'retry-user',
    prefixEpoch: 0,
    failures: failed.failures,
    currentProjection: projection,
    committedSnapshot: null,
    coverableCutoff: 1,
    requestStartCutoff: 2,
    coveredDigest: xwire.coveredPrefixDigest(projection, 1),
    frozenRecordPrefixRef: 'frozen-first',
    frozenRecordPrefixDigest: 'digest-first',
    frozenRecordPrefixBody: 'first frozen record',
    memoryPreamble: 'prior responsibility',
    outcome: 'tool-calls',
  }
  const first = xwire.transform(input)
  assert.equal(first.probe.candidate.cutoff, 1)
  assert.equal(first.promoted, true)

  const succeeded = budget.recordSuccess(failed)
  const nextInput = {
    ...input,
    failures: succeeded.failures,
    prefixEpoch: 1,
    committedSnapshot: first.probe.candidate,
    coverableCutoff: 2,
    coveredDigest: xwire.coveredPrefixDigest(projection, 2),
    frozenRecordPrefixRef: 'frozen-later',
    frozenRecordPrefixDigest: 'digest-later',
    outcome: null,
  }
  const next = xwire.transform(nextInput)
  assert.equal(next.probe, null, 'a retained retry row is not a new failure')
  assert.deepEqual(next.output, first.output, 'new coverage must not replace the sealed prefix')

  const failedAgain = budget.recordFailure(succeeded)
  const recovery = xwire.transform({ ...nextInput, failures: failedAgain.failures })
  assert.equal(recovery.probe.candidate.cutoff, 2, 'a new failure may select the newer coverage')
})

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

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_a_probe_denied_policy_never_asks_for_a_probe', () => {
  // `selectProbe` throws if called. A denied policy must not pay for a
  // digest recomputation or a blob read to discover it has nothing to do.
  const plan = planner.attemptPlan({ kind: requestKind.workMain, mayRecover: false })

  assert.equal(plan.choice, 'UseCommittedEpoch')
  assert.equal(plan.probeId, null)
  assert.equal(plan.noProbeReason, null, 'not asking is not a refusal')
})

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_a_companion_request_never_asks_for_a_probe_even_when_allowed', () => {
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

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_an_allowed_work_main_carries_the_probe_it_selected', () => {
  const plan = planner.attemptPlan({
    kind: requestKind.workMain,
    mayRecover: true,
    probe: probeFor({ id: 'probe-abc' }),
  })

  assert.equal(plan.choice, 'UsePrefixProbe')
  assert.equal(plan.probeId, 'probe-abc')
  assert.equal(plan.noProbeReason, null)
})

test('WHAT[CONTEXT-COMPRESSION-008] CTX_010_invalid_role_and_kind_fail_closed', () => {
  // CompressionSurface.attemptPlanCore validates only role+kind; tier was dropped
  // from the surface (participantIdentityToJs pins selectedTier:"deep"), so a
  // stray tier field is ignored rather than rejected.
  for (const input of [{ role: 'unknown' }, { kind: 'unknown' }]) {
    const result = planner.attemptPlan(input)
    assert.equal(result.ok, false)
    assert.match(result.error, /unknown (role|request kind)/)
  }
  const tierIgnored = planner.attemptPlan({ tier: 'unknown' })
  assert.equal(tierIgnored.choice, 'UseCommittedEpoch')
})

test('WHAT[CONTEXT-COMPRESSION-009] CTX_011_a_refused_candidate_falls_back_to_the_committed_epoch_with_a_reason', () => {
  // The ordinary outcome when an allowed attempt has nothing to work with. The request still
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
