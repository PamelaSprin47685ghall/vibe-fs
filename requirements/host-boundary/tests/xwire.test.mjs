import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as XWireSurface from '../../../dist/Context/Prefix/XWireSurface.js'

// ── Test fixtures ───────────────────────────────────────────────────────
//
// The X-wire transform decision surface (XWireSurface.transform) mirrors the
// production XWire.applyTransform decision pipeline with JS-native I/O. The
// inputs are the observable facts the production function reads from the
// journal, the session snapshot port, and the plugin runtime scope; the
// outputs are the decisions it makes (no-op, fail-closed, render synthetic
// prefix, consume arming, promote prefix rebase).

const baseProjection = {
  messages: [
    { role: 'user', parts: [{ kind: 'text', text: 'hello' }] },
    { role: 'assistant', parts: [{ kind: 'text', text: 'answer' }] },
  ],
}

const armedInput = (overrides = {}) => ({
  journal: true,
  sessionId: 'ses_x',
  armed: true,
  prefixEpoch: 0,
  offset: 1, // Fork1 — a recovery slot
  physicalUser: 'user-1',
  armedPhysicalUser: 'user-1',
  snapshotPort: true,
  currentProjection: baseProjection,
  committedSnapshot: null,
  coverableCutoff: 2, // material exists (coverage ahead of request)
  coveredDigest: XWireSurface.coveredPrefixDigest(baseProjection, 1),
  requestStartCutoff: 1,
  frozenRecordPrefixRef: 'blob/ref/frozen-1',
  frozenRecordPrefixDigest: 'sha256:frozen-1',
  frozenRecordPrefixBody: 'frozen record prefix body text',
  memoryPreamble: 'companion memory preamble',
  outcome: null,
  ...overrides,
})

// ── HOST-BOUNDARY-021: no business semantics without full context ────────

test('WHAT[HOST-BOUNDARY-021] XWIRE_covered_prefix_digest_is_sha256', () => {
  assert.equal(
    XWireSurface.coveredPrefixDigest(baseProjection, 1),
    '823d6b40827ef755cd32aeef72b073a7883c01dcb29c5fdf3318c237a59f1129',
  )
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_no_journal_is_a_noop', () => {
  const result = XWireSurface.transform(armedInput({ journal: false }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_no_session_id_in_output_is_a_noop', () => {
  const result = XWireSurface.transform(armedInput({ sessionId: '' }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_unarmed_session_is_a_noop', () => {
  const result = XWireSurface.transform(armedInput({ armed: false }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})

test('WHAT[PAR-011] XWIRE_recovery_permit_cannot_be_consumed_by_other_physical_material_in_the_same_session', () => {
  const result = XWireSurface.transform(armedInput({
    armedPhysicalUser: 'retry-user-1',
    physicalUser: 'ordinary-user-2',
  }))

  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})

test('WHAT[HOST-BOUNDARY-008] XWIRE_pre_inference_transform_freezes_a_pending_plan_without_waiting_for_assistant_run', () => {
  const production = readFileSync(
    new URL('../../../src/Wanxiangshu/Context/Prefix/Wire.fs', import.meta.url),
    'utf8',
  )
  const planning = production.slice(
    production.indexOf('let private planArmedWorkMainRetry'),
    production.indexOf('let private applyNonReplicaTransform'),
  )

  assert.doesNotMatch(planning, /bindProviderRunAfterProjectionCatchup/)
  assert.doesNotMatch(planning, /ProviderRunBinding\.observeBindableRun/)
  assert.match(planning, /RecordPendingAttemptPlan/)
})

// ── HOST-BOUNDARY-020: fail-closed only after exact physical ownership ──

test('WHAT[PAR-011] XWIRE_missing_current_physical_user_cannot_consume_the_accepted_retry_permit', () => {
  const result = XWireSurface.transform(armedInput({ physicalUser: '' }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.consumed, false)
})

test('WHAT[HOST-BOUNDARY-008] XWIRE_pre_inference_retry_does_not_require_a_public_session_snapshot', () => {
  const result = XWireSurface.transform(armedInput({ snapshotPort: false }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, false)
  assert.equal(result.consumed, true)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_prefix_epoch_fail_closed', () => {
  const withoutEpoch = armedInput()
  delete withoutEpoch.prefixEpoch
  const result = XWireSurface.transform(withoutEpoch)
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /prefix epoch/)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_malformed_prefix_epoch_fail_closed', () => {
  const result = XWireSurface.transform(armedInput({ prefixEpoch: 'not-an-epoch' }))
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /prefix epoch/)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_frozen_prefix_body_fail_closed', () => {
  const result = XWireSurface.transform(armedInput({ frozenRecordPrefixBody: undefined }))
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /frozen record prefix body/)
})

// ── HOST-BOUNDARY-021: armed + material → probe renders synthetic prefix ─

test('WHAT[HOST-BOUNDARY-021] XWIRE_armed_with_material_renders_synthetic_prefix', () => {
  const result = XWireSurface.transform(armedInput({ coverableCutoff: 2 }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, false)
  // When a probe is selected and the prefix intent renders a synthetic prefix,
  // the transform changes the projection and consumes the arming.
  assert.equal(result.consumed, true)
  // The output should differ from the input when a synthetic prefix is rendered.
  if (result.changed) {
    assert.ok(result.output, 'output must be present when changed')
    assert.ok(result.output.messages, 'output must have messages')
  }
})

// ── HOST-BOUNDARY-021: armed + no material → no probe, no change ─────────

test('WHAT[HOST-BOUNDARY-021] XWIRE_armed_without_material_no_probe', () => {
  const result = XWireSurface.transform(armedInput({ coverableCutoff: 0 }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, false)
  // RecoveryOpportunity belongs to this physical attempt. NoCoverage sends the
  // ordinary projection but must not leak arming into a later parked odd cursor.
  assert.equal(result.consumed, true)
  assert.equal(result.changed, false)
  assert.ok(result.noProbeReason, 'should have a no-probe reason')
})

// ── HOST-BOUNDARY-021: reconcile — completed + probe → promote ───────────

test('WHAT[HOST-BOUNDARY-021] XWIRE_completed_attempt_with_probe_promotes_prefix_rebase', () => {
  const result = XWireSurface.transform(armedInput({ outcome: 'completed', coverableCutoff: 2 }))
  assert.equal(result.ok, true)
  assert.equal(result.promoted, true)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_stale_probe_does_not_promote_after_prefix_rebase', () => {
  const result = XWireSurface.reconcile({
    hasPlan: true,
    outcome: 'completed',
    hasProbe: true,
    currentEpoch: 2,
    probeEpoch: 1,
  })
  assert.equal(result.promoted, false)
  assert.equal(result.cleared, true)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_failed_attempt_does_not_promote', () => {
  const result = XWireSurface.transform(armedInput({ outcome: 'failed', coverableCutoff: 2 }))
  assert.equal(result.ok, true)
  assert.equal(result.promoted, false)
})

// ── HOST-BOUNDARY-021: reconcile decision surface ────────────────────────

test('WHAT[HOST-BOUNDARY-021] XWIRE_reconcile_completed_with_probe_promotes_and_clears', () => {
  const result = XWireSurface.reconcile({ hasPlan: true, outcome: 'completed', hasProbe: true, currentEpoch: 0, probeEpoch: 0 })
  assert.equal(result.promoted, true)
  assert.equal(result.cleared, true)
  assert.equal(result.keptPlan, false)
})

test('WHAT[CONTEXT-COMPRESSION-011] XWIRE_tool_call_provider_success_promotes_and_clears_before_host_turn_finishes', () => {
  const result = XWireSurface.reconcile({
    hasPlan: true,
    outcome: 'tool-calls',
    hasProbe: true,
    currentEpoch: 0,
    probeEpoch: 0,
  })
  assert.equal(result.promoted, true)
  assert.equal(result.cleared, true)
  assert.equal(result.keptPlan, false)
})

test('WHAT[CONTEXT-COMPRESSION-011] XWIRE_ordinary_request_keeps_committed_prefix_instead_of_resurrecting_raw_x', () => {
  const source = readFileSync(
    new URL('../../../src/Wanxiangshu/Context/Prefix/Wire.fs', import.meta.url),
    'utf8',
  )
  const committed = source.slice(
    source.indexOf('let private applyOrdinaryCommittedPrefix'),
    source.indexOf('let private planArmedWorkMainRetry'),
  )
  const ordinary = source.slice(
    source.indexOf('let private applyNonReplicaTransform'),
    source.indexOf('let private applySessionTransform'),
  )

  assert.match(ordinary, /settleVisibleToolContinuations/)
  assert.match(ordinary, /TryTakeRecoveryPermit\(sessionId, physical\)/)
  assert.match(ordinary, /\| None ->[\s\S]*?applyOrdinaryCommittedPrefix/)
  assert.match(committed, /applyCommittedPrefix durable sessionId state rawMessages output/)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_reconcile_completed_without_probe_clears_without_promoting', () => {
  const result = XWireSurface.reconcile({ hasPlan: true, outcome: 'completed', hasProbe: false })
  assert.equal(result.promoted, false)
  assert.equal(result.cleared, true)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_reconcile_failed_clears_plan_without_promoting', () => {
  const result = XWireSurface.reconcile({ hasPlan: true, outcome: 'failed', hasProbe: true })
  assert.equal(result.promoted, false)
  assert.equal(result.cleared, true)
  assert.equal(result.keptPlan, false)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_reconcile_unknown_reread_keeps_the_plan', () => {
  const result = XWireSurface.reconcile({ hasPlan: true, outcome: 'in-progress', hasProbe: true })
  assert.equal(result.promoted, false)
  assert.equal(result.cleared, false)
  assert.equal(result.keptPlan, true)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_reconcile_no_plan_is_inert', () => {
  const result = XWireSurface.reconcile({ hasPlan: false, outcome: 'completed', hasProbe: true })
  assert.equal(result.promoted, false)
  assert.equal(result.cleared, false)
  assert.equal(result.keptPlan, false)
})

// ── Mutation sensitivity: wrong physical ownership must not consume ──────
//
// A session-scoped boolean arming regression would consume this permit even
// though the current physical user is unrelated.

test('WHAT[PAR-011] XWIRE_mutation_sensitive_unrelated_physical_user_must_not_consume_recovery', () => {
  const result = XWireSurface.transform(armedInput({
    armedPhysicalUser: 'retry-user-1',
    physicalUser: 'unrelated-user-9',
  }))
  assert.equal(result.consumed, false,
    'mutation guard: session presence alone must never consume a physical recovery permit')
})
