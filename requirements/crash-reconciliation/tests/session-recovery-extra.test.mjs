// Recovery family mapping and authorization through the recovery owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'

test('WHAT[CRASH-010] MISC_recovery_of_handle_family_all_branches', () => {
  assert.deepEqual(recovery.handleFamily('none'), { state: 'NoRecoveryRequired', restoredHandles: [], reason: '' })
  assert.deepEqual(recovery.handleFamily('recovered'), { state: 'Recovered', restoredHandles: ['h1'], reason: '' })
  assert.equal(recovery.handleFamily('waiting').state, 'Waiting')
  assert.match(recovery.handleFamily('waiting').reason, /handle h2 waiting: still running/)
  assert.equal(recovery.handleFamily('blocked').state, 'Blocked')
  assert.match(recovery.handleFamily('blocked').reason, /handle h3 blocked: linkage conflict/)
})

test('WHAT[CRASH-010] MISC_recovery_of_job_family_all_branches', () => {
  assert.equal(recovery.jobFamily('none').state, 'NoRecoveryRequired')
  assert.equal(recovery.jobFamily('recovered').state, 'Recovered')
  assert.equal(recovery.jobFamily('waiting').state, 'Waiting')
  assert.equal(recovery.jobFamily('blocked').state, 'Blocked')
})

test('WHAT[CRASH-014] MISC_recovery_validate_closure_pure', () => {
  const valid = recovery.validateClosure('a1', [
    { kind: 'work', session: 'a1' },
    { kind: 'child', parent: 'a1', child: 'a2', handle: 'h1' },
  ])
  assert.equal(valid.ok, true)
  const cycle = recovery.validateClosure('a1', [
    { kind: 'work', session: 'a1' },
    { kind: 'child', parent: 'a1', child: 'a2', handle: 'h1' },
    { kind: 'blogger', main: 'a2', blogger: 'a2' },
  ])
  assert.equal(cycle.ok, false)
  assert.equal(cycle.error, 'RecoveryCycle')
})

test('WHAT[CRASH-013] MISC_recovery_authorize_aggregates_blocks_waits_ready', () => {
  assert.equal(recovery.authorize('root1', 9, [{ session: 'child1', state: 'Blocked' }]).state, 'FamilyBlocked')
  assert.equal(
    recovery.authorize('root1', 9, [
      { session: 'child1', state: 'Waiting' },
      { session: 'other', state: 'NoRecoveryRequired' },
    ]).state,
    'FamilyWaiting',
  )
  const ready = recovery.authorize('root1', 9, [{ session: 'child1', state: 'Recovered' }])
  assert.equal(ready.state, 'FamilyReady')
  assert.equal(ready.root, 'root1')
  assert.equal(ready.sequence, 9)
  assert.deepEqual(ready.members, [])
})

test('WHAT[CRASH-002] MISC_recovery_receipt_accessors_and_nonempty_helpers', () => {
  assert.deepEqual(recovery.receiptView('s1', 42), {
    session: 's1',
    sequence: 42,
    snapshotDigest: null,
    resolvedClaims: [],
    restoredHandles: [],
  })
})
