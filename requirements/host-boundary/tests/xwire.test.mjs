import assert from 'node:assert/strict'
import test from 'node:test'
import { wireProjection } from './support/host-surface.mjs'

const base = { messages: [{ info: { id: 'asst-1' }, parts: [{ type: 'text', text: 'answer' }] }] }
const run = (input = {}) => wireProjection.transform({ journal: {}, sessionId: 'ses_x', physicalUser: 'user-1', snapshot: base, ...input })

test('WHAT[HOST-BOUNDARY-021] XWIRE_no_journal_is_a_noop', () => {
  const result = wireProjection.transform({ sessionId: 'ses_x', physicalUser: 'user-1', snapshot: base })
  assert.equal(result.ok, false)
  assert.match(result.error, /journal/)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_no_session_id_in_output_is_a_noop', () => {
  const result = wireProjection.transform({ journal: {}, physicalUser: 'user-1', snapshot: base })
  assert.equal(result.ok, false)
  assert.match(result.error, /session id/)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_unarmed_session_is_a_noop', () => {
  const result = run()
  assert.equal(result.ok, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_physical_user_message_throws', () => {
  const result = run({ physicalUser: undefined, armed: true })
  assert.equal(result.ok, false)
  assert.match(result.error, /physical user/)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_snapshot_port_throws', () => {
  const result = run({ snapshot: undefined, armed: true })
  assert.equal(result.ok, false)
  assert.match(result.error, /snapshot/)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_snapshot_error_throws', () => {
  const result = run({ snapshot: undefined })
  assert.equal(result.ok, false)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_unbindable_run_throws', () => {
  const result = run({ physicalUser: undefined })
  assert.equal(result.ok, false)
})

test('WHAT[HOST-BOUNDARY-020] XWIRE_missing_projections_throws', () => {
  const result = wireProjection.transform({ journal: {}, sessionId: 'ses_x', physicalUser: 'user-1', snapshot: {}, armed: true })
  assert.equal(result.ok, true)
  assert.equal(result.promoted, true)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_probe_plan_renders_synthetic_prefix_and_consumes_arming', () => {
  const result = run({ armed: true, cutoff: 1 })
  assert.equal(result.changed, true)
  assert.equal(result.consumed, true)
  assert.equal(result.promoted, true)
  assert.equal(result.output.messages.length, 1)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_no_material_spends_slot_without_probe', () => {
  const result = run({ armed: false, cutoff: 1 })
  assert.equal(result.consumed, false)
  assert.equal(result.changed, false)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_probe_reconcile_promotes_prefix_rebase_fact', () => {
  const result = run({ armed: true, cutoff: 1 })
  assert.equal(result.promoted, true)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_failed_attempt_clears_plan_without_promoting', () => {
  const result = run({ armed: true, snapshot: undefined })
  assert.equal(result.ok, false)
})

test('WHAT[HOST-BOUNDARY-021] XWIRE_unknown_reread_keeps_the_plan', () => {
  const result = run({ armed: true, cutoff: 0 })
  assert.equal(result.consumed, true)
})
