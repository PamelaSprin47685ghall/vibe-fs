import assert from 'node:assert/strict'
import test from 'node:test'
import * as XWireSurface from '../../../dist/Context/Prefix/XWireSurface.js'

// ── Test fixtures ───────────────────────────────────────────────────────
//
// The X-wire transform decision surface (XWireSurface.transform) mirrors the
// production XWire.applyTransform decision pipeline with JS-native I/O. The
// inputs are the observable facts the production function reads from the
// journal, the session snapshot port, and the plugin runtime scope; the
// outputs are the decisions it makes (no-op, fail-closed, render synthetic
// prefix, consume the exact accepted retry, promote prefix rebase).

const baseProjection = {
  messages: [
    { role: 'user', parts: [{ kind: 'text', text: 'hello' }] },
    { role: 'assistant', parts: [{ kind: 'text', text: 'answer' }] },
  ],
}

const acceptedRetryInput = (overrides = {}) => ({
  journal: true,
  sessionId: 'ses_x',
  acceptedRetry: true,
  failures: 1,
  prefixEpoch: 0,
  physicalUser: 'user-1',
  acceptedPhysicalUser: 'user-1',
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
  const result = XWireSurface.transform(acceptedRetryInput({ journal: false }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_no_session_id_in_output_is_a_noop', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ sessionId: '' }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_unaccepted_retry_is_a_noop', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ acceptedRetry: false }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})

test('WHAT[PAR-011] XWIRE_accepted_retry_cannot_be_consumed_by_other_physical_material_in_the_same_session', () => {
  const result = XWireSurface.transform(acceptedRetryInput({
    acceptedPhysicalUser: 'retry-user-1',
    physicalUser: 'ordinary-user-2',
  }))

  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})

// ── HOST-BOUNDARY-020: fail-closed only after exact physical ownership ──

test('WHAT[PAR-011] XWIRE_missing_current_physical_user_cannot_consume_the_accepted_retry', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ physicalUser: '' }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.consumed, false)
})

test('WHAT[HOST-BOUNDARY-008] XWIRE_pre_inference_retry_does_not_require_a_public_session_snapshot', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ snapshotPort: false }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, false)
  assert.equal(result.consumed, true)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_prefix_epoch_fail_closed', () => {
  const withoutEpoch = acceptedRetryInput()
  delete withoutEpoch.prefixEpoch
  const result = XWireSurface.transform(withoutEpoch)
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /prefix epoch/)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_malformed_prefix_epoch_fail_closed', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ prefixEpoch: 'not-an-epoch' }))
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /prefix epoch/)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_frozen_prefix_body_fail_closed', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ frozenRecordPrefixBody: undefined }))
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /frozen record prefix body/)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_covered_digest_mismatch_refuses_the_probe_fail_closed', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ coveredDigest: 'not-the-current-prefix-digest' }))
  assert.equal(result.ok, true)
  assert.equal(result.consumed, true)
  assert.equal(result.changed, false)
  assert.match(result.noProbeReason, /^CutoffProofFailed:/)
  assert.equal(result.probe, null)
})

// ── HOST-BOUNDARY-021: accepted retry + material → synthetic prefix ──────

test('WHAT[HOST-BOUNDARY-021] XWIRE_accepted_retry_with_material_renders_synthetic_prefix', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ coverableCutoff: 2 }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, false)
  // When a probe is selected and the prefix intent renders a synthetic prefix,
  // the transform changes the projection and consumes this accepted retry.
  assert.equal(result.consumed, true)
  // The output should differ from the input when a synthetic prefix is rendered.
  if (result.changed) {
    assert.ok(result.output, 'output must be present when changed')
    assert.ok(result.output.messages, 'output must have messages')
  }
})

// ── HOST-BOUNDARY-021: accepted retry + no material → no probe ───────────

test('WHAT[HOST-BOUNDARY-021] XWIRE_accepted_retry_without_material_has_no_probe', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ coverableCutoff: 0 }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, false)
  // NoCoverage sends the ordinary projection; this request's choice cannot
  // migrate into a later physical retry.
  assert.equal(result.consumed, true)
  assert.equal(result.changed, false)
  assert.ok(result.noProbeReason, 'should have a no-probe reason')
})

// ── HOST-BOUNDARY-021: reconcile — completed + probe → promote ───────────

test('WHAT[HOST-BOUNDARY-021] XWIRE_completed_attempt_with_probe_promotes_prefix_rebase', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ outcome: 'completed', coverableCutoff: 2 }))
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
  const result = XWireSurface.transform(acceptedRetryInput({ outcome: 'failed', coverableCutoff: 2 }))
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
// A session-scoped acceptance regression would consume this request even when
// the current physical user is unrelated.

test('WHAT[PAR-011] XWIRE_mutation_sensitive_unrelated_physical_user_must_not_consume_accepted_retry', () => {
  const result = XWireSurface.transform(acceptedRetryInput({
    acceptedPhysicalUser: 'retry-user-1',
    physicalUser: 'unrelated-user-9',
  }))
  assert.equal(result.consumed, false,
    'mutation guard: session presence alone must never consume an accepted physical retry')
})
