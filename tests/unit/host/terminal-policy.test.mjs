// TPOL: TerminalPolicy — pure terminal admission rules over the journal projection.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  agentJournal, agentFact, sessionId, logicalRunId, authorityRoot,
  handleId, handleOwnership, stream, caseOf, roles, completionKind,
  managerJobId, worktreeIdentity, worktreePath, targetRef,
} from '../support/domain.mjs'

const { Role } = await import('../../../dist/Kernel/Roles.js')
const { AgentJournalModule_appendAgent } = await import('../../../dist/Journal/AgentJournal.js')
const {
  sessionDead, roleName, tryLinkedChild, isLinkedChild, isTopLevelManager,
  mainSealedForBlogger, outstandingBackground,
} = await import('../../../dist/Infrastructure/OpenCode/Host/TerminalPolicy.js')

const MAIN = sessionId('ses_main')
const CHILD = sessionId('ses_child')
const OTHER = sessionId('ses_other')

const linkFact = (parent = MAIN, child = CHILD, target = 'fast-coder') =>
  agentFact('HandleLinked', {
    ParentSessionId: parent,
    ChildSessionId: child,
    Handle: handleId.agent('h1'),
    TargetAgent: target,
    CanonicalRole: roles.of('Coder'),
    Ownership: handleOwnership.durableParentHandle(),
  })

const rootFact = (sid, canonicalRole, kind = 'HumanRoot') =>
  agentFact('AuthorityRootAccepted', {
    SessionId: sid,
    LogicalRunId: logicalRunId(`run-${sid}`),
    AuthorityRootUserMessageId: authorityRoot(`root-${sid}`),
    AuthorityKind: kind,
    SelectedAgent: `fast-${canonicalRole}`,
    PeerAgent: `deep-${canonicalRole}`,
    CanonicalRole: canonicalRole,
    SelectedTier: 'fast',
  })

const withJournal = async (facts, fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-tpol-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const journal = opened.journal
  try {
    for (const [sid, inner] of facts) {
      const res = await AgentJournalModule_appendAgent(stream.session(sid), undefined, inner, journal)
      assert.equal(caseOf(res), 'Ok', 'fact must fold')
    }
    await fn(journal)
  } finally {
    try { opened.dispose() } catch {}
    rmSync(dir, { recursive: true, force: true })
  }
}

test('TPOL_sessionDead_false_without_journal_or_on_fresh_journal', async () => {
  assert.equal(sessionDead(null, MAIN), false)
  assert.equal(sessionDead(undefined, MAIN), false)
  await withJournal([], async (journal) => {
    assert.equal(sessionDead(journal, MAIN), false)
  })
})

test('TPOL_roleName_lowercases_roles_and_handles_none', () => {
  assert.equal(roleName(Role.Manager), 'manager')
  assert.equal(roleName(Role.Coder), 'coder')
  assert.equal(roleName(Role.Orchestrator), 'orchestrator')
  assert.equal(roleName(null), undefined)
  assert.equal(roleName(undefined), undefined)
})

test('TPOL_tryLinkedChild_finds_child_handle_and_keeps_target_agent', async () => {
  await withJournal([[MAIN, linkFact()]], async (journal) => {
    const record = tryLinkedChild(journal, 'ses_child')
    assert.ok(record, 'linked child must be findable')
    assert.equal(record.TargetAgent, 'fast-coder')
    assert.equal(record.ChildSessionId, CHILD)
    assert.equal(isLinkedChild(journal, 'ses_child'), true)
    assert.equal(tryLinkedChild(journal, 'ses_unknown'), undefined)
    assert.equal(isLinkedChild(journal, 'ses_unknown'), false)
  })
})

test('TPOL_tryLinkedChild_without_journal_returns_none', () => {
  assert.equal(tryLinkedChild(null, 'ses_child'), undefined)
  assert.equal(isLinkedChild(undefined, 'ses_child'), false)
})

test('TPOL_isTopLevelManager_without_journal_uses_parent_map_only', () => {
  assert.equal(isTopLevelManager(new Map(), null, 'ses_x'), true)
  assert.equal(isTopLevelManager(new Map([['ses_x', 'ses_p']]), null, 'ses_x'), false)
})

test('TPOL_isTopLevelManager_linked_child_without_authority_is_not_top_level', async () => {
  await withJournal([[MAIN, linkFact()]], async (journal) => {
    assert.equal(isTopLevelManager(new Map(), journal, 'ses_child'), false, 'linked child is not top level')
    assert.equal(isTopLevelManager(new Map(), journal, 'ses_other'), true, 'unknown session without parent is top level')
    assert.equal(isTopLevelManager(new Map([['ses_other', 'ses_main']]), journal, 'ses_other'), false)
  })
})

test('TPOL_isTopLevelManager_manager_run_is_top_level_unless_orchestrator_parented', async () => {
  await withJournal([[MAIN, rootFact(MAIN, 'manager')]], async (journal) => {
    assert.equal(isTopLevelManager(new Map(), journal, 'ses_main'), true)
    assert.equal(
      isTopLevelManager(new Map([['ses_main', 'ses_par']]), journal, 'ses_main'),
      true,
      'parent linkage alone must not suppress the guard (no orchestrator session)',
    )
  })

  // Orchestrator parent (its own canonical role is Orchestrator) suppresses.
  await withJournal([
    [MAIN, rootFact(MAIN, 'manager')],
    [sessionId('ses_par'), rootFact(sessionId('ses_par'), 'orchestrator', 'AgentOwnerRoot')],
  ], async (journal) => {
    assert.equal(
      isTopLevelManager(new Map([['ses_main', 'ses_par']]), journal, 'ses_main'),
      false,
      'manager forked by an Orchestrator is a job worker, not top level',
    )
  })
})

test('TPOL_isTopLevelManager_non_manager_run_is_never_top_level', async () => {
  await withJournal([[MAIN, rootFact(MAIN, 'coder')]], async (journal) => {
    assert.equal(isTopLevelManager(new Map(), journal, 'ses_main'), false)
  })
})

test('TPOL_mainSealedForBlogger_false_without_journal_or_unlinked_main', async () => {
  assert.equal(mainSealedForBlogger(null, MAIN), false)
  await withJournal([], async (journal) => {
    assert.equal(mainSealedForBlogger(journal, MAIN), false)
  })
})

test('TPOL_mainSealedForBlogger_retired_handle_seals_main', async () => {
  const completed = agentFact('HandleCompleted', {
    ParentSessionId: MAIN,
    Handle: handleId.agent('h1'),
    Kind: completionKind.of('Terminal'),
    CompletionRef: undefined,
    CompletionDigest: undefined,
  })
  const retired = agentFact('HandleRetired', { ParentSessionId: MAIN, Handle: handleId.agent('h1') })
  await withJournal([
    [MAIN, linkFact()],
    [MAIN, completed],
    [MAIN, retired],
  ], async (journal) => {
    assert.equal(mainSealedForBlogger(journal, CHILD), true, 'retired handle must seal the child main for Blogger')
    assert.equal(mainSealedForBlogger(journal, OTHER), false)
    assert.equal(mainSealedForBlogger(journal, MAIN), false, 'parent session itself is not the main')
  })
})

test('TPOL_outstandingBackground_manager_has_listable_handles', async () => {
  await withJournal([[MAIN, linkFact()]], async (journal) => {
    assert.equal(outstandingBackground(journal, () => false, Role.Manager, MAIN), true)
    assert.equal(outstandingBackground(journal, () => false, Role.Manager, OTHER), false)
  })
  assert.equal(outstandingBackground(null, () => false, Role.Manager, MAIN), false)
})

test('TPOL_outstandingBackground_devops_checks_durable_then_live_pty', async () => {
  await withJournal([[MAIN, linkFact()]], async (journal) => {
    assert.equal(outstandingBackground(journal, () => false, Role.DevOps, MAIN), true, 'durable handle counts')
  })
  await withJournal([], async (journal) => {
    assert.equal(outstandingBackground(journal, () => false, Role.DevOps, MAIN), false)
    assert.equal(outstandingBackground(journal, () => true, Role.DevOps, MAIN), true, 'live pty probe counts')
  })
})

test('TPOL_outstandingBackground_orchestrator_active_jobs', async () => {
  const created = agentFact('ManagerJobCreated', {
    ManagerJobId: managerJobId('job_1'),
    ManagerSessionId: CHILD,
    ManagerAgent: 'fast-manager',
    WorktreeIdentity: worktreeIdentity('wt_1'),
    WorktreePath: worktreePath('/tmp/wt1'),
    TargetRef: targetRef('refs/heads/main'),
    TargetBranchFrozen: 'refs/heads/main',
  })
  // Orchestrator projection is session-agnostic: any active job answers true
  // for any session key.
  await withJournal([[sessionId('ses_orch'), created]], async (journal) => {
    assert.equal(outstandingBackground(journal, () => false, Role.Orchestrator, sessionId('ses_orch')), true)
    assert.equal(outstandingBackground(journal, () => false, Role.Orchestrator, OTHER), true, 'active jobs are global')
  })
  assert.equal(outstandingBackground(null, () => false, Role.Orchestrator, MAIN), false)
})

test('TPOL_outstandingBackground_other_roles_never_outstanding', async () => {
  await withJournal([[MAIN, linkFact()]], async (journal) => {
    assert.equal(outstandingBackground(journal, () => true, Role.Coder, MAIN), false)
    assert.equal(outstandingBackground(journal, () => true, null, MAIN), false)
    assert.equal(outstandingBackground(null, () => true, null, MAIN), false)
  })
})
