// JournalPayloadClosure — Journal facts lift real local content addresses into
// universal EventStore payload_refs. Placeholder text is not a dependency.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const withJournal = async (writerId, fn) => {
  const repo = mkdtempSync(join(tmpdir(), `wxs-journal-closure-${writerId}-`))
  execFileSync('git', ['init', '--quiet', repo])
  const commonDir = join(repo, '.git')
  const booted = await journal.JournalSurface_bootWithWriterId(commonDir, writerId, `rt-${writerId}`, 4242, '2026-04-01T00:00:00Z')
  assert.equal(booted.ok, true, JSON.stringify(booted.error))
  try {
    await fn(commonDir, booted.journal)
  } finally {
    journal.JournalSurface_dispose(booted.journal)
    rmSync(repo, { recursive: true, force: true })
  }
}

const lifeOpened = (session, ref, digest) => ({
  case: 'LifeOpened',
  payload: {
    SessionId: session,
    LifeId: `life-${session}`,
    OpeningUserMessageId: `msg-${session}`,
    OpeningTextRef: ref,
    OpeningTextDigest: digest,
    OpeningCursorSequence: 1,
  },
})

const payloadRefsFromFile = (commonDir, writerId) => {
  const file = join(commonDir, 'wanxiang', 'events', `${writerId}.ndjson`)
  return readFileSync(file, 'utf8')
    .trim()
    .split('\n')
    .map(JSON.parse)
    .filter((event) => event.event_type === 'JournalEnvelope')
    .map((event) => event.payload_refs)
}

test('WHAT[DURABLE-EVENTS-012] closure_lifts_a_content_addressed_digest_into_payload_refs', async () => {
  await withJournal('closure-real', async (commonDir, handle) => {
    const receipt = await journal.JournalSurface_writePayload(handle, 'durable body\n')
    assert.equal(receipt.ok, true, JSON.stringify(receipt.error))

    const appended = await journal.JournalSurface_appendManagerLifecycle(
      handle,
      { kind: 'Session', session: 'ses_real' },
      lifeOpened('ses_real', receipt.blobRef, receipt.blobDigest),
    )
    assert.equal(appended.ok, true, JSON.stringify(appended.error))

    const refs = payloadRefsFromFile(commonDir, 'closure-real')
    assert.deepEqual(refs.at(-1), [receipt.blobRef.slice('blobs/'.length)])
  })
})

test('WHAT[DURABLE-EVENTS-012] closure_dedupes_a_matching_blob_ref_and_digest_pair', async () => {
  await withJournal('closure-dedupe', async (commonDir, handle) => {
    const receipt = await journal.JournalSurface_writePayload(handle, 'dedupe body\n')
    assert.equal(receipt.ok, true, JSON.stringify(receipt.error))
    // The writer derives the digest from bytes; use a real content address and
    // assert the closure contains one ref even though ref + digest are both present.
    const appended = await journal.JournalSurface_appendManagerLifecycle(
      handle,
      { kind: 'Session', session: 'ses_dedupe' },
      lifeOpened('ses_dedupe', receipt.blobRef, receipt.blobDigest),
    )
    assert.equal(appended.ok, true, JSON.stringify(appended.error))

    const refs = payloadRefsFromFile(commonDir, 'closure-dedupe')
    assert.equal(refs.at(-1).length, 1)
    assert.equal(refs.at(-1)[0], receipt.blobRef.slice('blobs/'.length))
  })
})

test('WHAT[DURABLE-EVENTS-012] closure_ignores_non_content_addressed_placeholder_handles', async () => {
  await withJournal('closure-placeholder', async (commonDir, handle) => {
    const appended = await journal.JournalSurface_appendManagerLifecycle(
      handle,
      { kind: 'Session', session: 'ses_placeholder' },
      lifeOpened('ses_placeholder', 'blobs/placeholder', 'sha-placeholder'),
    )
    assert.equal(appended.ok, true, JSON.stringify(appended.error))
    assert.deepEqual(payloadRefsFromFile(commonDir, 'closure-placeholder').at(-1), [])
  })
})

test('WHAT[DURABLE-EVENTS-012] closure_is_empty_for_a_fact_without_blob_fields', async () => {
  await withJournal('closure-empty', async (commonDir, handle) => {
    const appended = await journal.JournalSurface_appendAgent(
      handle,
      { kind: 'Session', session: 'ses_empty' },
      null,
      { family: 'Companion', case: 'CompanionBloggerClosed', payload: { SessionId: 'ses_empty' } },
    )
    assert.equal(appended.ok, true, JSON.stringify(appended.error))
    assert.deepEqual(payloadRefsFromFile(commonDir, 'closure-empty').at(-1), [])
  })
})
