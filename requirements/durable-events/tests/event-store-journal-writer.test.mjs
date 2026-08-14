// FROZEN — 2026-08-14. Journal is a semantic adapter over local process NDJSON.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { agentFact, caseOf, idValue, payloadOf, runtimeId, sessionId, stream, utcOffset } from '../../verification-system/tests/support/domain.mjs'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const EsWriter = await import('../../../dist/Persistence/Journal/EventStoreJournalWriter.js')
const AgentJournal = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const CLOSED_AGENT = agentFact('CompanionBloggerClosed', { SessionId: sessionId('ses_es_writer') })

const createFn = Object.entries(EsWriter).find(([name]) => name.startsWith('EventStoreJournalWriter_create'))?.[1]
const mustOk = (result) => {
  assert.equal(caseOf(result), 'Ok', `expected Ok, got ${caseOf(result)}`)
  return payloadOf(result)
}

test('create_appends_RuntimeStarted_to_the_process_writer_file_not_a_Git_ref', async () => {
  const local = createLocalEventStore({ writerId: 'journal-writer-proof' })
  try {
    const [writer, init] = await createFn(runtimeId('rt_es'), 4242, utcOffset('2026-04-01T00:00:00Z'), local.store)
    assert.equal(Number(idValue.localSeq(init.LocalSeq)), 1)
    assert.equal(caseOf(init.Stream), 'Workspace')
    const file = join(local.commonDir, 'wanxiang', 'events', 'journal-writer-proof.ndjson')
    assert.equal(existsSync(file), true)
    assert.equal(readFileSync(file, 'utf8').trim().split('\n').length, 1)
    assert.equal(EsWriter.EventStoreJournalWriter__get_IsPoisoned(writer), false)
    writer.Release?.()
  } finally {
    local.close()
  }
})

test('append_adds_one_local_line_and_Current_is_already_integrated', async () => {
  const local = createLocalEventStore({ writerId: 'journal-append-proof' })
  try {
    const [writer, init] = await createFn(runtimeId('rt_es_append'), 4242, utcOffset('2026-04-01T00:00:00Z'), local.store)
    const journal = mustOk(AgentJournal.AgentJournalModule_createFromEventStore(writer, init))
    const before = readFileSync(join(local.commonDir, 'wanxiang', 'events', 'journal-append-proof.ndjson'), 'utf8')
    const appended = await AgentJournal.AgentJournalModule_appendAgent(stream.session(sessionId('ses_es_writer')), undefined, CLOSED_AGENT, journal)
    assert.equal(caseOf(appended), 'Ok')
    const after = readFileSync(join(local.commonDir, 'wanxiang', 'events', 'journal-append-proof.ndjson'), 'utf8')
    assert.equal(after.startsWith(before), true)
    assert.equal(after.trim().split('\n').length, 2)
    assert.ok(local.store.TryCurrent('Journal'))
    journal.Dispose?.()
  } finally {
    local.close()
  }
})

test('BlobWriter_uses_local_content_addressed_payloads_not_workspace_blobs_or_Git_ODB', async () => {
  const local = createLocalEventStore({ writerId: 'journal-blob-proof' })
  try {
    const [writer] = await createFn(runtimeId('rt_es_blob'), 4242, utcOffset('2026-04-01T00:00:00Z'), local.store)
    const receipt = mustOk(await writer.BlobWriter.Write('large-body\n'))
    assert.match(idValue.blobRef(receipt.BlobRef), /^blobs\/[0-9a-f]{64}$/)
    const handle = idValue.blobRef(receipt.BlobRef).slice('blobs/'.length)
    assert.equal(existsSync(join(local.commonDir, 'wanxiang', 'payloads', handle)), true)
    assert.equal(mustOk(await writer.BlobWriter.Read(receipt.BlobRef)), 'large-body\n')
    writer.Release?.()
  } finally {
    local.close()
  }
})

test('journal_writer_source_has_no_snapshot_CAS_or_Git_raw_store', async () => {
  const { readFile } = await import('node:fs/promises')
  const source = await readFile(new URL('../../../src/Wanxiangshu/Persistence/Journal/EventStoreJournalWriter.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /OpenSnapshot|CompareAndSwapRef|IGitRawStore|RootOid|StoreSnapshot/)
  assert.match(source, /store\.Append/)
})
