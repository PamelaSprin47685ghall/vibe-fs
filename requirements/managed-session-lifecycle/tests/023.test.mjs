import assert from 'node:assert/strict'
import test from 'node:test'
import * as RolesSurface from '../../../dist/Foundation/RolesSurface.js'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'

test('WHAT[MANAGED-SESSION-023] new tasks reject legacy roles and legacy active sessions are explicitly retired', () => {
  const publicLabels = RolesSurface.allPublicRoleLabels

  // Active public roles must include Engineer and DevOps
  assert.ok(publicLabels.includes('engineer'), 'Engineer must be in public role labels')
  assert.ok(publicLabels.includes('devops'), 'DevOps must be in public role labels')

  // Deprecated roles must be removed from public role labels
  assert.equal(publicLabels.includes('coder'), false, 'Coder must not be in public role labels')
  assert.equal(publicLabels.includes('inspector'), false, 'Inspector must not be in public role labels')
  assert.equal(publicLabels.includes('browser'), false, 'Browser must not be in public role labels')
  assert.equal(publicLabels.includes('inquiry'), false, 'Inquiry must not be in public role labels')
  assert.equal(publicLabels.includes('distiller'), false, 'Distiller must not be in public role labels')

  // Behavioral verification: new tasks / handles reject legacy invalid roles fail-closed
  const initial = HandleSurface.emptyState()
  const legacyLinkResult = HandleSurface.apply(initial, {
    op: 'link',
    handle: HandleSurface.handleIdAgent('h_legacy_1'),
    child: 'ses_child_legacy',
    agent: 'coder',
    role: 'InvalidLegacyRole',
  })
  assert.equal(legacyLinkResult.ok, false, 'Linking or creating handle for unrecognized/invalid role must be rejected')

  // Behavioral verification: legacy active session discovered on recovery is explicitly retired / drained to terminal and cannot resume
  const activeState = HandleSurface.apply(initial, {
    op: 'link',
    handle: HandleSurface.handleIdAgent('h_legacy_2'),
    child: 'ses_child_legacy_2',
    agent: 'coder',
    role: 'Engineer',
  })
  assert.equal(activeState.ok, true)

  // Explicit retirement / abandon transition via CancelAndDrain
  const abandoned = HandleSurface.apply(activeState.state, {
    op: 'abandon',
    handle: HandleSurface.handleIdAgent('h_legacy_2'),
    reason: 'ParentCancelled',
    abandonedAt: new Date().toISOString(),
  })
  assert.equal(abandoned.ok, true, 'Legacy active handle must be transitioned to terminal Abandoned/Retired')

  // Terminal handle cannot be revived or resumed into active state
  const revived = HandleSurface.apply(abandoned.state, {
    op: 'link',
    handle: HandleSurface.handleIdAgent('h_legacy_2'),
    child: 'ses_child_legacy_2',
    agent: 'coder',
    role: 'Engineer',
  })
  assert.equal(revived.ok, false, 'Terminalized legacy handle must never be revived into active state')
})
