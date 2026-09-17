import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { existsSync, mkdtempSync, readFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const CLOSED_AGENT = {
  family: 'Companion',
  case: 'CompanionBloggerClosed',
  payload: { SessionId: 'ses_es_writer' },
}
const mustOk = (result, label) => {
  assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
  return result
}
const withRepo = (writerId, fn) => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-journal-'))
  execFileSync('git', ['init', '--quiet', repo])
  const commonDir = join(repo, '.git')
  return fn(commonDir)
    .finally(() => rmSync(repo, { recursive: true, force: true }))
}

test('WHAT[DURABLE-EVENTS-012] BlobWriter_uses_local_content_addressed_payloads_not_workspace_blobs_or_Git_ODB', async () => {
  await withRepo('journal-blob-proof', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_boot(commonDir, 'rt_es_blob', 4242, '2026-04-01T00:00:00Z'), 'boot')

    const receipt = mustOk(await journal.JournalSurface_writePayload(booted.journal, 'large-body\n'), 'write')
    assert.match(receipt.blobRef, /^blobs\/[0-9a-f]{64}$/)

    const handle = receipt.blobRef.slice('blobs/'.length)
    assert.equal(existsSync(join(commonDir, 'wanxiang', 'payloads', handle)), true)

    const read = mustOk(await journal.JournalSurface_readPayload(booted.journal, receipt.blobRef), 'read')
    assert.equal(read.content, 'large-body\n')
    journal.JournalSurface_dispose(booted.journal)
  })
})
test('WHAT[DURABLE-EVENTS-012] appended_fact_lifts_real_blob_digest_into_persisted_payload_refs', async () => {
  await withRepo('journal-closure-proof', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_bootWithWriterId(commonDir, 'journal-closure-proof', 'rt_es_closure', 4242, '2026-04-01T00:00:00Z'), 'boot')

    const receipt = mustOk(await journal.JournalSurface_writePayload(booted.journal, 'part-body\n'), 'write')
    const handle = receipt.blobRef.slice('blobs/'.length)

    const fact = {
      case: 'LifeOpened',
      payload: {
        SessionId: 'ses_closure',
        LifeId: 'life_closure',
        OpeningUserMessageId: 'msg_1',
        OpeningTextRef: receipt.blobRef,
        OpeningTextDigest: receipt.blobDigest,
        OpeningCursorSequence: 1,
      },
    }

    const appended = mustOk(
      await journal.JournalSurface_appendManagerLifecycle(booted.journal, { kind: 'Session', session: 'ses_closure' }, fact),
      'append',
    )
    assert.ok(appended.projection)

    const ndjson = readFileSync(join(commonDir, 'wanxiang', 'events', 'journal-closure-proof.ndjson'), 'utf8')
    assert.match(ndjson, new RegExp(`"payload_refs":\\["${handle}"\\]`))
    journal.JournalSurface_dispose(booted.journal)
  })
})
test('WHAT[DURABLE-EVENTS-012] closure_fails_closed_when_a_real_content_address_is_missing', async () => {
  await withRepo('journal-closure-missing', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_boot(commonDir, 'rt_es_missing', 4242, '2026-04-01T00:00:00Z'), 'boot')
    const missingDigest = 'f'.repeat(64)

    const fact = {
      case: 'LifeOpened',
      payload: {
        SessionId: 'ses_missing',
        LifeId: 'life_missing',
        OpeningUserMessageId: 'msg_missing',
        OpeningTextRef: `blobs/${missingDigest}`,
        OpeningTextDigest: missingDigest,
        OpeningCursorSequence: 1,
      },
    }

    const result = await journal.JournalSurface_appendManagerLifecycle(
      booted.journal,
      { kind: 'Session', session: 'ses_missing' },
      fact,
    )
    assert.equal(result.ok, false)
    assert.match(JSON.stringify(result.error), /MissingPayload/i)
    journal.JournalSurface_dispose(booted.journal)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, readFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

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
}
