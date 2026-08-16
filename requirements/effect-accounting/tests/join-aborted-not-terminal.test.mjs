/**
 * P0-RECOVERY-JOIN-001 Case A: Aborted alone is never durable finality / joinable.
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  childRecovery,
  handleId,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'

const AGENT = 'fast-coder'
const HANDLE = handleId.agent('h-abort')
const CHILD = sessionId('ses_child_abort')

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_aborted_alone_is_not_terminal', () => {
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotMissing(),
    [childRecovery.abortedObserved('host abort')],
  )
  assert.equal(resolution.name, 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_aborted_observed_never_joinable', () => {
  const resolution = childRecovery.resolveChild(
    childRecovery.durableUnknown(),
    childRecovery.snapshotActive(),
    [childRecovery.abortedObserved('signal only')],
  )
  assert.notEqual(resolution.name, 'RecoveredTerminal')
  assert.equal(resolution.name, 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_aborted_with_session_active_is_recovered_active', () => {
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotActive(),
    [childRecovery.abortedObserved('stale abort'), childRecovery.sessionActive()],
  )
  assert.equal(resolution.name, 'RecoveredActive')
})

// Mid-turn: readable non-terminal snapshot + SessionActive → RecoveredActive (permit-eligible).
test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_mid_turn_snapshot_active_with_session_active_is_recovered_active', () => {
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotActive(),
    [childRecovery.sessionActive()],
  )
  assert.equal(resolution.name, 'RecoveredActive')
})

// True unreadable → RecoveryIncomplete (wait); never RecoveryBlocked solely from unreadable.
test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_true_unreadable_is_recovery_incomplete', () => {
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotUnreadable('GetMessages network error'),
    [childRecovery.sessionActive()],
  )
  assert.equal(resolution.name, 'RecoveryIncomplete')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_tryFromProvenTerminal_rejects_empty_body', () => {
  const empty = childRecovery.evidenceCompleted(AGENT, HANDLE, CHILD, '')
  const result = childRecovery.tryFromProvenTerminal(empty)
  assert.equal(result.ok, false)
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_tryFromDurableCompleted_rejects_cancelled', () => {
  // Clean-break: tryFromDurableCompleted deleted; any kind+body is permanent Error.
  const result = childRecovery.tryFromDurableCompleted(
    AGENT,
    HANDLE,
    CHILD,
    'Cancelled',
    'ignored',
  )
  assert.equal(result.ok, false)
  assert.match(String(result.error), /deleted|not joinable|Cancelled|fromDecoded/i)
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_joinable_completion_has_no_fromAborted_export', () => {
  const names = childRecovery.joinableCompletionExports()
  assert.ok(
    names.every((n) => !/fromAborted|FromAborted/i.test(n)),
    `unexpected fromAborted export: ${names.join(', ')}`,
  )
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_proven_terminal_then_joinable', () => {
  const evidence = childRecovery.evidenceCompleted(AGENT, HANDLE, CHILD, '{"status":"ok"}')
  const proof = childRecovery.tryFromProvenTerminal(evidence)
  assert.equal(proof.ok, true)
  const resolution = childRecovery.resolveChild(
    childRecovery.durableActive(),
    childRecovery.snapshotTerminal(evidence),
    [childRecovery.abortedObserved('prior abort ignored once terminal proven')],
  )
  assert.equal(resolution.name, 'RecoveredTerminal')
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_001_durable_completed_awaiting_join_is_joinable', () => {
  const evidence = childRecovery.evidenceCompleted(AGENT, HANDLE, CHILD, 'body-ok')
  const proof = childRecovery.tryFromProvenTerminal(evidence)
  assert.equal(proof.ok, true)
  const resolution = childRecovery.resolveChild(
    childRecovery.durableCompletedAwaitingJoin(proof.value),
    childRecovery.snapshotMissing(),
    [childRecovery.abortedObserved('noise')],
  )
  assert.equal(resolution.name, 'RecoveredTerminal')
})
