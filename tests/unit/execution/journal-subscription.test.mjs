// Journal revision subscription (P0-A): AwaitChangeFrom wakes on successful fold.

import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentFact,
  agentJournal,
  handleId,
  journalRevision,
  roles,
  sessionId,
  stream,
} from '../support/domain.mjs'

const withJournal = async (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jrev-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    await fn(created.journal)
  } finally {
    created.dispose()
  }
}

const appendHandleLinked = (journal, parent = 'ses_p', child = 'ses_c', agent = 'h1') => {
  const fact = agentFact('HandleLinked', {
    ParentSessionId: sessionId(parent),
    ChildSessionId: sessionId(child),
    Handle: handleId.agent(agent),
    TargetAgent: 'fast-coder',
    CanonicalRole: roles.of('Coder'),
  })
  return agentJournal.appendAgent(stream.session(sessionId(parent)), undefined, fact, journal)
}

test('EXEC_journal_revision_advances_only_on_successful_fold', async () => {
  await withJournal(async (journal) => {
    const before = journalRevision.value(agentJournal.revision(journal))
    assert.ok(before >= 1, 'create folds RuntimeStarted → revision ≥ 1')

    const linked = appendHandleLinked(journal)
    assert.equal(linked.ok, true, JSON.stringify(linked.error))

    const after = journalRevision.value(agentJournal.revision(journal))
    assert.equal(after, before + 1, 'one successful append advances revision by LocalSeq step')
  })
})

test('EXEC_AwaitChangeFrom_after_append_returns_promptly', async () => {
  await withJournal(async (journal) => {
    const from = agentJournal.revision(journal)
    const linked = appendHandleLinked(journal)
    assert.equal(linked.ok, true, JSON.stringify(linked.error))

    const started = Date.now()
    const change = await agentJournal.awaitChangeFrom(from, journal)
    const elapsed = Date.now() - started

    assert.ok(elapsed < 500, `must not wait full budget; elapsed=${elapsed}`)
    assert.ok(journalRevision.isAfter(change.Revision, from))
    assert.equal(journalRevision.value(change.Revision), journalRevision.value(agentJournal.revision(journal)))
  })
})

test('EXEC_AwaitChangeFrom_before_append_waits_then_completes', async () => {
  await withJournal(async (journal) => {
    const from = agentJournal.revision(journal)
    const pending = agentJournal.awaitChangeFrom(from, journal)

    // Append after waiter is registered (next macrotask).
    setTimeout(() => {
      const linked = appendHandleLinked(journal, 'ses_p2', 'ses_c2', 'h2')
      assert.equal(linked.ok, true, JSON.stringify(linked.error))
    }, 30)

    const change = await pending
    assert.ok(journalRevision.isAfter(change.Revision, from))
    assert.ok(change.Envelope != null)
  })
})
