// Split from tests/unit/host/terminal-policy.test.mjs (cutover Wave 2a);
// owner: managed-session-lifecycle. MANAGED-SESSION-015 + 反向覆盖：tryLinkedChild
// （durable handle 链接）、mainSealedForBlogger（retired handle 封 main）、
// outstandingBackground（listable handles / durable+live PTY / orchestrator 全局
// active jobs）、sessionDead（durable 终态 admission）。
// isTopLevelManager 断言归 interaction-authority（AuthorityRoot 事实）；
// roleName 断言归 session-ontology（SESSION-ONTOLOGY-013 canonical role label）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  agentJournal, agentFact, sessionId, handleId, handleOwnership, stream,
  caseOf, roles, completionKind, managerJobId, worktreeIdentity, worktreePath, targetRef,
} from '../../verification-system/tests/support/domain.mjs'

const { Role } = await import('../../../dist/Foundation/Roles.js')
const { AgentJournalModule_appendAgent } = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const {
  sessionDead, tryLinkedChild, isLinkedChild, mainSealedForBlogger, outstandingBackground,
} = await import('../../../dist/OpenCode/Host/TerminalPolicy.js')

const MAIN = sessionId('ses_main')
const CHILD = sessionId('ses_child')
const OTHER = sessionId('ses_other')

const linkFact = (parent = MAIN, child = CHILD, target = 'fast-coder') =>
  agentFact('HandleLinked', {
    ParentSessionId: parent,
    ChildSessionId: child,
    Handle: handleId.agent('h1'),
    TargetAgent: target,
    Byname: 'tpol-child',
    CanonicalRole: roles.of('Coder'),
    Ownership: handleOwnership.durableParentHandle(),
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

test('WHAT[MANAGED-SESSION-006] TPOL_sessionDead_false_without_journal_or_on_fresh_journal', async () => {
  assert.equal(sessionDead(null, MAIN), false)
  assert.equal(sessionDead(undefined, MAIN), false)
  await withJournal([], async (journal) => {
    assert.equal(sessionDead(journal, MAIN), false)
  })
})

test('WHAT[MANAGED-SESSION-015] TPOL_tryLinkedChild_finds_child_handle_and_keeps_target_agent', async () => {
  await withJournal([[MAIN, linkFact()]], async (journal) => {
    const record = tryLinkedChild(journal, 'ses_child')
    assert.ok(record, 'linked child must be findable')
    assert.equal(record.TargetAgent, 'fast-coder')
    assert.deepEqual(record.ChildSessionId, CHILD)
    assert.equal(isLinkedChild(journal, 'ses_child'), true)
    assert.equal(tryLinkedChild(journal, 'ses_unknown'), undefined)
    assert.equal(isLinkedChild(journal, 'ses_unknown'), false)
  })
})

test('WHAT[MANAGED-SESSION-015] TPOL_tryLinkedChild_without_journal_returns_none', () => {
  assert.equal(tryLinkedChild(null, 'ses_child'), undefined)
  assert.equal(isLinkedChild(undefined, 'ses_child'), false)
})

test('WHAT[MANAGED-SESSION-006] TPOL_mainSealedForBlogger_false_without_journal_or_unlinked_main', async () => {
  assert.equal(mainSealedForBlogger(null, MAIN), false)
  await withJournal([], async (journal) => {
    assert.equal(mainSealedForBlogger(journal, MAIN), false)
  })
})

test('WHAT[MANAGED-SESSION-006] TPOL_mainSealedForBlogger_retired_handle_seals_main', async () => {
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

test('WHAT[MANAGED-SESSION-006] TPOL_outstandingBackground_manager_has_listable_handles', async () => {
  await withJournal([[MAIN, linkFact()]], async (journal) => {
    assert.equal(outstandingBackground(journal, () => false, Role.Manager, MAIN), true)
    assert.equal(outstandingBackground(journal, () => false, Role.Manager, OTHER), false)
  })
  assert.equal(outstandingBackground(null, () => false, Role.Manager, MAIN), false)
})

test('WHAT[MANAGED-SESSION-006] TPOL_outstandingBackground_devops_checks_durable_then_live_pty', async () => {
  await withJournal([[MAIN, linkFact()]], async (journal) => {
    assert.equal(outstandingBackground(journal, () => false, Role.DevOps, MAIN), true, 'durable handle counts')
  })
  await withJournal([], async (journal) => {
    assert.equal(outstandingBackground(journal, () => false, Role.DevOps, MAIN), false)
    assert.equal(outstandingBackground(journal, () => true, Role.DevOps, MAIN), true, 'live pty probe counts')
  })
})

test('WHAT[MANAGED-SESSION-006] TPOL_outstandingBackground_orchestrator_active_jobs', async () => {
  const created = agentFact('ManagerJobCreated', {
    ManagerJobId: managerJobId('job_1'),
    ManagerSessionId: CHILD,
    ManagerAgent: 'fast-manager',
    Byname: 'tpol-manager',
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

test('WHAT[MANAGED-SESSION-006] TPOL_outstandingBackground_other_roles_never_outstanding', async () => {
  await withJournal([[MAIN, linkFact()]], async (journal) => {
    assert.equal(outstandingBackground(journal, () => true, Role.Coder, MAIN), false)
    assert.equal(outstandingBackground(journal, () => true, null, MAIN), false)
    assert.equal(outstandingBackground(null, () => true, null, MAIN), false)
  })
})
