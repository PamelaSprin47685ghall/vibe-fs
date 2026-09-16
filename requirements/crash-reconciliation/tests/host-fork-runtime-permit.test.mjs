// Join permit admission crosses the delegation-owned JoinSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/Host/JoinSurface.js'

const valid = (over = {}) => ({
  permitRoot: 'ses_hfrt',
  permitSequence: 0,
  currentRoot: 'ses_hfrt',
  currentSequence: 0,
  permitMembers: [],
  currentMembers: [],
  ...over,
})

test('WHAT[CRASH-011] HFRT_join_with_permit_root_mismatch_is_not_found', () => {
  const result = join.validatePermit('ses_other', 0, 'ses_hfrt', 0, [], [])
  assert.equal(result.ok, false)
  assert.match(result.error, /root mismatch: permit=ses_other runtime=ses_hfrt/)
})

test('WHAT[CRASH-011] HFRT_join_with_permit_stale_journal_sequence_is_not_found', () => {
  const result = join.validatePermit('ses_hfrt', 1000, 'ses_hfrt', 0, [], [])
  assert.equal(result.ok, false)
  assert.match(result.error, /journalSequence stale: permit=1000/)
})

test('WHAT[CRASH-011] EXEC_023_permit_whose_recovered_member_is_gone_is_not_found', () => {
  const result = join.validatePermit('ses_hfrt', 0, 'ses_hfrt', 0, ['W:ses_vanished'], [])
  assert.equal(result.ok, false)
  assert.match(result.error, /closure lost members: missing=W:ses_vanished/)
})

test('WHAT[CRASH-011] EXEC_023_permit_survives_family_growth_after_recovery_closed', () => {
  const result = join.validatePermit('ses_hfrt', 0, 'ses_hfrt', 0, ['W:ses_hfrt'], ['W:ses_hfrt', 'C:ses_child>ses_grandchild'])
  assert.equal(result.ok, true)
  assert.equal(result.error, 'NothingToJoin')
})

test('WHAT[CRASH-011] HFRT_join_with_valid_permit_passes_validation', () => {
  assert.deepEqual(join.validatePermit('ses_hfrt', 0, 'ses_hfrt', 0, [], []), { ok: true, error: 'NothingToJoin' })
})

test('WHAT[CRASH-011] HFRT_await_agent_with_permit_validation_error_maps_to_not_found', () => {
  const result = join.validatePermit('ses_other', 0, 'ses_hfrt', 0, [], [])
  assert.equal(result.ok, false)
  assert.match(result.error, /NotFound|root mismatch/)
})
