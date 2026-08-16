// requirements/crash-reconciliation/tests/recovery-closure-permit.test.mjs
//
// Recovery closure algebra (Domain/SessionRecovery.fs): the closure is
// validated before any recovery work, its member tokens are the stable identity
// a permit must still find, and the permit's admission is monotone — recovered
// members may not vanish, but members created after recovery closed are legal.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf, listItems, setItems, stringSet, toList } from '../../verification-system/tests/support/domain.mjs'
import { SessionIdModule_create as sid } from '../../../dist/Foundation/Identity.js'
import { AgentHandleIdModule_create as hid } from '../../../dist/Foundation/Identity.js'
import { ManagerJobIdModule_create as jobId } from '../../../dist/Foundation/Identity.js'
import {
  FamilyRecoveryPermit,
  FamilyRecoveryPermitModule_missingFrom,
  RecoveryClosure,
  RecoveryClosureModule_members,
  RecoveryNode,
  RecoveryNodeModule_token,
  ValidatedClosureModule_value,
  validateClosurePure,
} from '../../../dist/Execution/Session/Recovery/Model.js'

const root = sid('ses_root')

const work = (s) => new RecoveryNode(0, [sid(s)])
const child = (p, c, h) => new RecoveryNode(1, [sid(p), sid(c), hid(h)])
const companion = (m, c) => new RecoveryNode(2, [sid(m), sid(c)])
const blogger = (m, b) => new RecoveryNode(3, [sid(m), sid(b)])
const managerJob = (j, m) => new RecoveryNode(4, [jobId(j), sid(m)])
const reviewer = (j, r) => new RecoveryNode(5, [jobId(j), sid(r)])

const closureOf = (nodes, sequence = 3n) =>
  new RecoveryClosure(root, toList(nodes), 'digest', sequence)

test('WHAT[CRASH-014] CRASH_CLOSURE_validate_accepts_unique_sessions_and_keeps_order', () => {
  const nodes = [child('p', 'c', 'h1'), companion('m', 'c2'), work('w1')]
  const result = validateClosurePure(closureOf(nodes))
  assert.equal(result.tag, 0, 'unique sessions must validate')
  const validated = ValidatedClosureModule_value(result.fields[0])
  assert.deepEqual([...setItems(RecoveryClosureModule_members(validated))].sort(), [
    'A:p>c:h1',
    'C:m>c2',
    'W:w1',
  ])
})

test('WHAT[CRASH-014] CRASH_CLOSURE_duplicate_session_is_a_cycle_block', () => {
  const duplicated = closureOf([work('w1'), work('w1')])
  const result = validateClosurePure(duplicated)
  assert.equal(result.tag, 1, 'a session listed twice must fail closed')
  assert.equal(caseOf(result.fields[0].Head), 'RecoveryCycle')
})

test('WHAT[CRASH-014] CRASH_CLOSURE_member_tokens_are_stable_identities', () => {
  const nodes = [work('w'), child('p', 'c', 'h1'), companion('m', 'c2'), blogger('m', 'b3'), managerJob('j1', 'm4'), reviewer('j1', 'r5')]
  assert.equal(RecoveryNodeModule_token(nodes[0]), 'W:w')
  assert.equal(RecoveryNodeModule_token(nodes[1]), 'A:p>c:h1')
  assert.equal(RecoveryNodeModule_token(nodes[2]), 'C:m>c2')
  assert.equal(RecoveryNodeModule_token(nodes[3]), 'B:m>b3')
  assert.equal(RecoveryNodeModule_token(nodes[4]), 'M:j1:m4')
  assert.equal(RecoveryNodeModule_token(nodes[5]), 'R:j1:r5')
})

test('WHAT[CRASH-011] CRASH_CLOSURE_permit_refuses_loss_and_admits_growth', () => {
  const permit = new FamilyRecoveryPermit(root, 3n, stringSet(['W:w1', 'A:p>c:h1']))

  // A member the family no longer has invalidates the permit: recovery closed
  // over something that has since vanished (EXEC-023 closure membership).
  const lost = FamilyRecoveryPermitModule_missingFrom(stringSet(['W:w1']), permit)
  assert.deepEqual(listItems(lost), ['A:p>c:h1'])

  // Members created after recovery closed were never in need of recovery:
  // growth is legal and must not revoke the permit.
  const grown = FamilyRecoveryPermitModule_missingFrom(
    stringSet(['W:w1', 'A:p>c:h1', 'C:m>c2']),
    permit,
  )
  assert.deepEqual(listItems(grown), [])
})

test('WHAT[CRASH-014] CRASH_CLOSURE_members_set_matches_tokens', () => {
  const nodes = [work('w1'), child('p', 'c', 'h1')]
  const members = setItems(RecoveryClosureModule_members(closureOf(nodes)))
  assert.deepEqual([...members].sort(), ['A:p>c:h1', 'W:w1'])
})
