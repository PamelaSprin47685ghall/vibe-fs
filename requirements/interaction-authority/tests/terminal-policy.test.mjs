// Split from tests/unit/host/terminal-policy.test.mjs (cutover Wave 2a);
// owner: interaction-authority. isTopLevelManager（AuthorityRoot 事实）——
// 无 journal 时只看 parent map；linked child 无 authority 非 top-level；
// manager run 除非被 Orchestrator parent 否则 top-level；非 manager 永不
// top-level。tryLinkedChild/mainSealedForBlogger/outstandingBackground 断言
// 归 managed-session-lifecycle；roleName 归 session-ontology。

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  agentJournal, agentFact, sessionId, logicalRunId, authorityRoot,
  handleId, handleOwnership, stream, caseOf, roles,
} from '../../verification-system/tests/support/domain.mjs'

const { AgentJournalModule_appendAgent } = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const { isTopLevelManager } = await import('../../../dist/Infrastructure/OpenCode/Host/TerminalPolicy.js')

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
