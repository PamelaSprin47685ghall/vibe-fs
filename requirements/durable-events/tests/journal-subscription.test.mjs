// Journal revision subscription (DURABLE-EVENTS-013): awaiters wake only
// after a successful durable append and fold.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as revisions from '../../../dist/Persistence/Journal/RevisionSurface.js'

const lifeOpened = (session) => ({
  case: 'LifeOpened',
  payload: {
    SessionId: session,
    LifeId: `life-${session}`,
    OpeningUserMessageId: `msg-${session}`,
    OpeningTextRef: 'blobs/placeholder',
    OpeningTextDigest: 'sha-placeholder',
    OpeningCursorSequence: 1,
  },
})

const withJournal = async (writerId, fn) => {
  const repo = mkdtempSync(join(tmpdir(), `wxs-jrev-${writerId}-`))
  execFileSync('git', ['init', '--quiet', repo])
  const commonDir = join(repo, '.git')
  const opened = await journal.JournalSurface_bootWithWriterId(commonDir, writerId, `rt-${writerId}`, 4242, '2026-04-01T00:00:00Z')
  assert.equal(opened.ok, true, JSON.stringify(opened.error))
  try {
    await fn(opened.journal)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(repo, { recursive: true, force: true })
  }
}

const appendLife = (handle, session) =>
  journal.JournalSurface_appendManagerLifecycle(handle, { kind: 'Session', session }, lifeOpened(session))
const revisionOf = (handle) => Number(revisions.revision(handle))

test('WHAT[DURABLE-EVENTS-013] EXEC_journal_revision_advances_only_on_successful_fold', async () => {
  await withJournal('revision-advance', async (handle) => {
    const before = revisionOf(handle)
    assert.equal(before, 0, 'pure load writes no RuntimeStarted and starts at revision 0')

    const linked = await appendLife(handle, 'ses_p')
    assert.equal(linked.ok, true, JSON.stringify(linked.error))

    const after = revisionOf(handle)
    assert.equal(after, 2, 'first business append lazily writes RuntimeStarted#1 then publishes business#2')
  })
})

test('WHAT[DURABLE-EVENTS-013] EXEC_AwaitChangeFrom_after_append_returns_promptly', async () => {
  await withJournal('revision-prompt', async (handle) => {
    const from = revisionOf(handle)
    const linked = await appendLife(handle, 'ses_prompt')
    assert.equal(linked.ok, true, JSON.stringify(linked.error))

    const started = Date.now()
    const change = await revisions.awaitChangeFrom(from, handle)
    const elapsed = Date.now() - started

    assert.ok(elapsed < 500, `must not wait full budget; elapsed=${elapsed}`)
    assert.ok(change.revision > from)
    assert.equal(change.revision, revisionOf(handle))
  })
})

test('WHAT[DURABLE-EVENTS-013] EXEC_AwaitChangeFrom_before_append_waits_then_completes', async () => {
  await withJournal('revision-wait', async (handle) => {
    const from = revisionOf(handle)
    const pending = revisions.awaitChangeFrom(from, handle)

    setTimeout(() => {
      void appendLife(handle, 'ses_wait').then((linked) => {
        assert.equal(linked.ok, true, JSON.stringify(linked.error))
      })
    }, 30)

    const change = await pending
    assert.ok(change.revision > from)
    assert.equal(typeof change.envelope, 'string')
    assert.ok(change.envelope.length > 0)
  })
})
