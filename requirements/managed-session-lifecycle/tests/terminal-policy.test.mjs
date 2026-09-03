import assert from 'node:assert/strict'
import test from 'node:test'
import * as HandleSurface from '../../../dist/Execution/Delegation/Handle/Surface.js'
import * as TerminalPolicySurface from '../../../dist/OpenCode/Host/TerminalPolicySurface.js'

test('WHAT[MANAGED-SESSION-006] TPOL_sessionDead_false_without_journal', () => {
  assert.equal(TerminalPolicySurface.sessionDeadWithoutJournal('ses_main'), false)
})

test('WHAT[MANAGED-SESSION-006] TPOL_outstanding_without_durable_work_is_role_closed', () => {
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('Manager', false, 'ses_main'), false)
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('DevOps', true, 'ses_devops'), true)
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('Orchestrator', false, 'ses_orchestrator'), false)
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('Coder', true, 'ses_coder'), false)
  assert.equal(TerminalPolicySurface.outstandingWithoutJournal('unknown', true, 'ses_unknown'), false)
})

test('WHAT[MANAGED-SESSION-015] TPOL_linked_child_keeps_exact_handle_and_target', () => {
  const linked = HandleSurface.apply(HandleSurface.empty(), {
    op: 'link', handle: 'agent:h1', child: 'ses_child', agent: 'coder', role: 'Coder',
  })
  assert.equal(linked.ok, true)
  assert.deepEqual(HandleSurface.tryFindByChildSession(linked.state, 'ses_child'), {
    handle: 'agent:h1',
    child: 'ses_child',
    targetAgent: 'coder',
    role: 'Coder',
    lifecycle: 'Active',
    creationOrder: 0,
    completion: undefined,
    completionRef: undefined,
    completionDigest: undefined,
    abandonReason: undefined,
  })
  assert.equal(HandleSurface.tryFindByChildSession(linked.state, 'ses_missing'), null)
})
