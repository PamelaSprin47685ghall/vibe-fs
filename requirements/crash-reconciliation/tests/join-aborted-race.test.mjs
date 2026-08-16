// Join-recovery ordering through the child-recovery owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as child from '../../../dist/Execution/Delegation/Fork/ChildRecoverySurface.js'

const event = (kind, extra = {}) => ({ kind, ...extra })

test('WHAT[CRASH-001] CRASH_JOIN_abort_observation_never_becomes_completion', () => {
  const result = child.resolve('active', 'missing', ['aborted:transport'], '')
  assert.equal(result.result, 'RecoveryIncomplete')
  assert.notEqual(result.result, 'RecoveredTerminal')
  assert.notEqual(result.result, 'RecoveredAbandoned')
})

test('WHAT[CRASH-001] CRASH_JOIN_durable_abandoned_is_terminal_abandonment', () => {
  const result = child.resolve('abandoned', 'missing', [], '')
  assert.equal(result.result, 'RecoveredAbandoned')
})

test('WHAT[CRASH-001] CRASH_JOIN_parent_cancelled_abandons_missing_child', () => {
  assert.equal(child.resolve('active', 'missing', ['parent-cancelled'], '').result, 'RecoveredAbandoned')
})

test('WHAT[CRASH-001] CRASH_JOIN_active_child_is_recovered_active_not_incomplete', () => {
  assert.equal(child.resolve('active', 'active', ['active'], '').result, 'RecoveredActive')
})

test('WHAT[CRASH-001] CRASH_JOIN_restore_in_flight_remains_incomplete_without_permit', () => {
  assert.equal(child.resolve('active', 'missing', ['restore'], '').result, 'RecoveryIncomplete')
})

test('WHAT[CRASH-001] CRASH_JOIN_unreadable_snapshot_remains_incomplete', () => {
  assert.equal(child.resolve('active', 'unreadable', [], '').result, 'RecoveryIncomplete')
})

test('WHAT[CRASH-001] CRASH_JOIN_terminal_proof_is_joinable_only_with_body', () => {
  assert.deepEqual(child.provenTerminal('body'), { ok: true, finality: 'Succeeded', body: 'body' })
  assert.equal(child.provenTerminal('').ok, false)
})

test('WHAT[CRASH-001] CRASH_JOIN_return_requires_proof_before_commit', () => {
  assert.equal(child.trace([
    event('TerminalProofIssued', { agent: 'a1' }),
    event('HandleCompletionCommitted', { agent: 'a1' }),
    event('JoinReturned', { agent: 'a1' }),
  ]), true)
  assert.equal(child.trace([
    event('HandleCompletionCommitted', { agent: 'a1' }),
    event('JoinReturned', { agent: 'a1' }),
  ]), false)
  assert.equal(child.trace([
    event('RawAbortObserved', { session: 's1' }),
    event('HandleCompletionCommitted', { agent: 'a1' }),
  ]), false)
})
