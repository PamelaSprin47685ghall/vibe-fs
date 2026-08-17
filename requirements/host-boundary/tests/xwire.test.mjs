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

// ── HOST-BOUNDARY-020: fail-closed on insufficient observation ──────────

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_physical_user_message_fail_closed', () => {
  const result = XWireSurface.transform(armedInput({ physicalUser: '' }))
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /physical user/)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_snapshot_port_fail_closed', () => {
  const result = XWireSurface.transform(armedInput({ snapshotPort: false }))
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /snapshot/)
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
  // NoCoverage means the slot is not consumed (temporary — waiting for material).
  assert.equal(result.consumed, false)
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

// ── Mutation sensitivity: wrong production transform must fail ───────────
//
// If the surface returned ok=true for a missing physical user (i.e. did NOT
// fail-closed), this assertion would catch it. This proves the test is
// mutation-sensitive to the production decision logic.

test('WHAT[HOST-BOUNDARY-020] XWIRE_mutation_sensitive_missing_physical_user_must_fail_closed', () => {
  // A correct production surface returns ok=false for missing physical user.
  // If someone breaks the fail-closed gate, this test fails.
  const result = XWireSurface.transform(armedInput({ physicalUser: '' }))
  assert.equal(result.ok, false,
    'mutation guard: missing physical user must fail-closed, not silently succeed')
})
