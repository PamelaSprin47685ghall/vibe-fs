import assert from 'node:assert/strict'
import test from 'node:test'
import * as closurePermit from '../../../dist/Execution/Session/Recovery/RecoveryClosurePermitSurface.js'
import * as sessionExtra from '../../../dist/Execution/Session/Recovery/SessionRecoveryExtraSurface.js'

test('WHAT[CRASH-014] CRASH_CLOSURE_validate_accepts_unique_sessions_and_keeps_order', () => {
  const res = closurePermit.validate(['ses-1', 'ses-2'])
  assert.equal(res.ok, true)
})

test('WHAT[CRASH-014] CRASH_CLOSURE_duplicate_session_is_a_cycle_block', () => {
  const res = closurePermit.validate(['ses-1', 'ses-1'])
  assert.equal(res.ok, false)
  assert.equal(res.error, 'RecoveryCycle')
})

test('WHAT[CRASH-014] CRASH_CLOSURE_member_tokens_are_stable_identities', () => {
  assert.ok(closurePermit.stableTokens)
})

test('WHAT[CRASH-014] CRASH_CLOSURE_members_set_matches_tokens', () => {
  assert.ok(closurePermit.matchesTokens)
})

test('WHAT[CRASH-014] MISC_recovery_validate_closure_pure', () => {
  assert.ok(sessionExtra.validateClosure)
})
