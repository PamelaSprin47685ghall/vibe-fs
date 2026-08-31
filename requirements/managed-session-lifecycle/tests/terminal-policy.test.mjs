import assert from 'node:assert/strict'
import test from 'node:test'
import * as TerminalPolicySurface from '../../../dist/OpenCode/Host/TerminalPolicySurface.js'

const childJournal = {
  children: { 'ses_child': { handle: 'agent:h1', targetAgent: 'fast-coder' } },
  listable: ['ses_main'],
  sealed: true,
  activeJobs: true,
}

test('WHAT[MANAGED-SESSION-006] TPOL_sessionDead_false_without_journal_or_on_fresh_journal', () => {
  assert.equal(TerminalPolicySurface.sessionDeadWithoutJournal('ses_main'), false)
})

test('WHAT[MANAGED-SESSION-015] TPOL_tryLinkedChild_finds_child_handle_and_keeps_target_agent', () => {
  const record = terminalPolicy.tryLinkedChild(childJournal, 'ses_child')
  assert.equal(record.handle, 'agent:h1')
  assert.equal(record.targetAgent, 'fast-coder')
})

test('WHAT[MANAGED-SESSION-015] TPOL_tryLinkedChild_without_journal_returns_none', () => {
  assert.equal(terminalPolicy.tryLinkedChild(null, 'ses_child'), undefined)
  assert.equal(terminalPolicy.isLinkedChild(undefined, 'ses_child'), false)
})

test('WHAT[MANAGED-SESSION-006] TPOL_mainSealedForBlogger_false_without_journal_or_unlinked_main', () => {
  assert.equal(terminalPolicy.mainSealedForBlogger(null), false)
  assert.equal(terminalPolicy.mainSealedForBlogger({}), false)
})

test('WHAT[MANAGED-SESSION-006] TPOL_mainSealedForBlogger_retired_handle_seals_main', () => {
  assert.equal(terminalPolicy.mainSealedForBlogger(childJournal), true)
})

test('WHAT[MANAGED-SESSION-006] TPOL_outstandingBackground_manager_has_listable_handles', () => {
  assert.equal(terminalPolicy.outstandingBackground(childJournal, () => false, 'Manager', 'ses_main'), true)
  assert.equal(terminalPolicy.outstandingBackground(childJournal, () => false, 'Manager', 'ses_other'), false)
})

test('WHAT[MANAGED-SESSION-006] TPOL_outstandingBackground_devops_checks_durable_then_live_pty', () => {
  assert.equal(terminalPolicy.outstandingBackground(childJournal, () => false, 'DevOps', 'ses_main'), true)
  assert.equal(terminalPolicy.outstandingBackground({}, () => true, 'DevOps', 'ses_pty'), true)
})

test('WHAT[MANAGED-SESSION-006] TPOL_outstandingBackground_orchestrator_active_jobs', () => {
  assert.equal(terminalPolicy.outstandingBackground(childJournal, () => false, 'Orchestrator', 'ses_main'), true)
  assert.equal(terminalPolicy.outstandingBackground({}, () => false, 'Orchestrator', 'ses_main'), false)
})

test('WHAT[MANAGED-SESSION-006] TPOL_outstandingBackground_other_roles_never_outstanding', () => {
  assert.equal(terminalPolicy.outstandingBackground(childJournal, () => true, 'Coder', 'ses_main'), false)
  assert.equal(terminalPolicy.outstandingBackground(childJournal, () => true, undefined, 'ses_main'), false)
})
