// Recovery closure and permit laws through the recovery owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'

const root = 'ses_root'
const work = (session) => ({ kind: 'work', session })
const child = (parent, session, handle) => ({ kind: 'child', parent, child: session, handle })
const companion = (main, session) => ({ kind: 'companion', main, companion: session })
const blogger = (main, session) => ({ kind: 'blogger', main, blogger: session })
const managerJob = (job, manager) => ({ kind: 'managerJob', job, manager })

test('WHAT[CRASH-014] CRASH_CLOSURE_validate_accepts_unique_sessions_and_keeps_order', () => {
  const result = recovery.validateClosure(root, [child('p', 'c', 'h1'), companion('m', 'c2'), work('w1')])
  assert.equal(result.ok, true)
  assert.deepEqual([...result.members].sort(), ['A:p>c:h1', 'C:m>c2', 'W:w1'])
})

test('WHAT[CRASH-014] CRASH_CLOSURE_duplicate_session_is_a_cycle_block', () => {
  const result = recovery.validateClosure(root, [work('w1'), work('w1')])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'RecoveryCycle')
})

test('WHAT[CRASH-014] CRASH_CLOSURE_member_tokens_are_stable_identities', () => {
  assert.equal(recovery.token(work('w')), 'W:w')
  assert.equal(recovery.token(child('p', 'c', 'h1')), 'A:p>c:h1')
  assert.equal(recovery.token(companion('m', 'c2')), 'C:m>c2')
  assert.equal(recovery.token(blogger('m', 'b3')), 'B:m>b3')
  assert.equal(recovery.token(managerJob('j1', 'm4')), 'M:j1:m4')
})

test('WHAT[CRASH-011] CRASH_CLOSURE_permit_refuses_loss_and_admits_growth', () => {
  const permit = ['W:w1', 'A:p>c:h1']
  assert.deepEqual(recovery.missingMembers(permit, ['W:w1']), ['A:p>c:h1'])
  assert.deepEqual(recovery.missingMembers(permit, ['W:w1', 'A:p>c:h1', 'C:m>c2']), [])
})

test('WHAT[CRASH-014] CRASH_CLOSURE_members_set_matches_tokens', () => {
  const result = recovery.validateClosure(root, [work('w1'), child('p', 'c', 'h1')])
  assert.deepEqual([...result.members].sort(), ['A:p>c:h1', 'W:w1'])
})
