// requirements/finality/tests/finality-background-obligation.test.mjs
//
// FINALITY-027: background child or PTY present → fixed join prompt; join is a
// Finality prerequisite (FINALITY-003 runtime face, GLORY-038 / EXEC-016).
// The pure decision primitive is TerminalPolicy.outstandingBackground: for the
// Manager role it is exactly "has listable handles in the durable journal" —
// the blessed Life is no exception, and an empty journal is never outstanding.

import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentFact,
  agentJournal,
  handleId,
  handleOwnership,
  roles,
  sessionId,
  stream,
  terminalPolicy,
} from '../../verification-system/tests/support/domain.mjs'

const outstandingBackground = terminalPolicy.outstandingBackground

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-finality-bg-'))
  const created = await agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    await fn(created.journal)
  } finally {
    created.dispose()
  }
}

const appendHandleLinked = async (journal, parent = 'ses_mgr', child = 'ses_child', agent = 'h1') => {
  const fact = agentFact('HandleLinked', {
    ParentSessionId: sessionId(parent),
    ChildSessionId: sessionId(child),
    Handle: handleId.agent(agent),
    TargetAgent: 'fast-coder',
    Byname: 'finality-background-child',
    CanonicalRole: roles.of('Coder'),
    Ownership: handleOwnership.durableParentHandle(),
  })
  return agentJournal.appendAgent(stream.session(sessionId(parent)), undefined, fact, journal)
}

test('WHAT[FINALITY-027] Manager without journal or handles is never outstanding', () => {
  // No journal: no join obligation for the Manager (GLORY-038/EXEC-016).
  assert.equal(outstandingBackground(undefined, () => true, roles.of('Manager'), sessionId('ses_mgr')), false)
})

test('WHAT[FINALITY-027] Manager with a listable child handle has a join obligation', async () => {
  await withJournal(async (journal) => {
    const linked = await appendHandleLinked(journal)
    assert.equal(linked.ok, true, JSON.stringify(linked.error))
    assert.equal(
      outstandingBackground(journal, () => true, roles.of('Manager'), sessionId('ses_mgr')),
      true,
      'a durable listable child handle is an outstanding background resource',
    )
  })
})
