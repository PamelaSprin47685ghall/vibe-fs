// Journal is a semantic adapter over local process NDJSON.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as journal from '../../../dist/Persistence/Journal/Surface.js'

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

test('WHAT[DURABLE-EVENTS-020] create_is_read_only_until_the_first_business_append', async () => {
  await withRepo('journal-writer-proof', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_boot(commonDir, 'rt_es', 4242, '2026-04-01T00:00:00Z'), 'boot')
    const file = join(commonDir, 'wanxiang', 'events', 'journal-writer-proof.ndjson')
    assert.equal(existsSync(file), false)
    journal.JournalSurface_dispose(booted.journal)
  })
})

test('WHAT[DURABLE-EVENTS-006] append_adds_one_local_line_and_Current_is_already_integrated', async () => {
  await withRepo('journal-append-proof', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_bootWithWriterId(commonDir, 'journal-append-proof', 'rt_es_append', 4242, '2026-04-01T00:00:00Z'), 'boot')
    const file = join(commonDir, 'wanxiang', 'events', 'journal-append-proof.ndjson')

    assert.equal(existsSync(file), false)
    const appended = mustOk(
      await journal.JournalSurface_appendAgent(
        booted.journal,
        { kind: 'Session', session: 'ses_es_writer' },
        null,
        CLOSED_AGENT,
      ),
      'append',
    )

    const after = readFileSync(file, 'utf8')
    assert.equal(after.trim().split('\n').length, 2, 'first business append writes RuntimeStarted then the business fact')
    assert.ok(appended.projection)
    journal.JournalSurface_dispose(booted.journal)
  })
})

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

test('WHAT[DURABLE-EVENTS-012] journal_writer_source_has_no_snapshot_CAS_or_Git_raw_store', async () => {
  const { readFile } = await import('node:fs/promises')
  const source = await readFile(new URL('../../../src/Wanxiangshu/Persistence/Journal/EventStoreJournalWriter.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /OpenSnapshot|CompareAndSwapRef|IGitRawStore|RootOid|StoreSnapshot/)
  assert.match(source, /store\.Append/)
})

test('WHAT[DURABLE-EVENTS-013] journal_surface_does_not_mint_terminal_proof_from_forged_strings', () => {
  assert.equal(Object.hasOwn(journal, 'JournalSurface_recordTerminalCompletion'), false)
})
