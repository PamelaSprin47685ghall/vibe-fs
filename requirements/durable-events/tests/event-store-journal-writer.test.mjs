// tests/unit/journal/event-store-journal-writer.test.mjs
// W1 writer: EventStore-backed journal append + store blobs (no NDJSON / no blobs/ dir).

import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, readdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  agentFact,
  caseOf,
  errorResult,
  fact,
  idValue,
  isSome,
  listItems,
  payloadOf,
  runtimeId,
  sessionId,
  stream,
  utcOffset,
} from '../../verification-system/tests/support/domain.mjs'

const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')
const EsWriter = await import('../../../dist/Journal/EventStoreJournalWriter.js')
const AgentJournalMod = await import('../../../dist/Journal/AgentJournal.js')

const SESSION = sessionId('ses_es_writer')
const CLOSED_AGENT = agentFact('CompanionBloggerClosed', { SessionId: SESSION })
const CLOSED_FACT = fact('CompanionBloggerClosed', { SessionId: SESSION })

const oidValue = (rootOid) => Persist.GitObjectIdModule_value(Persist.RootOidModule_value(rootOid))
const snapshotOid = (snapshot) => oidValue(snapshot.RootOid)

const mustOk = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Ok', `${label} should be Ok, got ${caseOf(result)}: ${payloadOf(result)}`)
  return payloadOf(result)
}

const appendWriter = async (writer, streamId, envelopeFact, run) => {
  const result = await EsWriter.EventStoreJournalWriter__Append(writer, streamId, run, envelopeFact)
  return caseOf(result) === 'Committed'
    ? { committed: true, envelope: payloadOf(result) }
    : {
        committed: false,
        eventId: idValue.event(result.fields[0]),
        failure: caseOf(result.fields[1]),
        reason: result.fields[1]?.fields?.[0],
      }
}

const createPair = async (store, raw) => {
  const create =
    EsWriter.EventStoreJournalWriter_create_Z10F3E7A9 ??
    Object.entries(EsWriter)
      .find(([name]) => name.startsWith('EventStoreJournalWriter_create'))
      ?.[1]
  assert.equal(typeof create, 'function', 'EventStoreJournalWriter.create missing from dist')
  const pair = await create(runtimeId('rt_es'), 4242, utcOffset('2026-04-01T00:00:00Z'), store, raw ?? undefined)
  return { writer: pair[0], init: pair[1] }
}

const collectNdjson = (root) => {
  const hits = []
  const walk = (dir) => {
    if (!existsSync(dir)) return
    for (const name of readdirSync(dir, { withFileTypes: true })) {
      const path = join(dir, name.name)
      if (name.isDirectory()) walk(path)
      else if (name.name.endsWith('.ndjson')) hits.push(path)
    }
  }
  walk(root)
  return hits
}

test('create_publishes_RuntimeStarted_under_refs_wanxiang_store', async () => {
  const raw = GitRaw.GitRawStore_createInMemory()
  const store = Store.EventStore_create(raw)
  const { writer, init } = await createPair(store, raw)

  assert.equal(Number(idValue.localSeq(init.LocalSeq)), 1)
  assert.equal(caseOf(init.Stream), 'Workspace')

  const published = await raw.ReadRef(Persist.StoreRef_canonical)
  assert.equal(isSome(published), true)
  assert.equal(Persist.GitObjectIdModule_value(published), snapshotOid(await store.OpenSnapshot()))
  assert.equal(EsWriter.EventStoreJournalWriter__get_IsPoisoned(writer), false)

  writer.Dispose()
})

test('append_advances_StoreSnapshot_and_writes_no_ndjson_or_blobs_dir', async () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-es-journal-'))
  try {
    const raw = GitRaw.GitRawStore_createInMemory()
    const store = Store.EventStore_create(raw)
    const { writer, init } = await createPair(store, raw)
    const before = snapshotOid(EsWriter.EventStoreJournalWriter__get_StoreSnapshot(writer))

    const appended = await appendWriter(writer, stream.session(SESSION), CLOSED_FACT)
    assert.equal(appended.committed, true, appended.reason)
    assert.equal(Number(idValue.localSeq(appended.envelope.LocalSeq)), 2)

    const after = snapshotOid(EsWriter.EventStoreJournalWriter__get_StoreSnapshot(writer))
    assert.notEqual(after, before)
    assert.equal(Persist.GitObjectIdModule_value(await raw.ReadRef(Persist.StoreRef_canonical)), after)

    const blobs = mustOk(await GitRaw.GitRawStore_listEventBlobs(raw, (await store.OpenSnapshot()).RootOid))
    assert.ok(listItems(blobs).length >= 2, 'RuntimeStarted + append should both be in store')

    // Success path must not create NDJSON or a workspace blobs/ directory.
    assert.deepEqual(collectNdjson(workspace), [])
    assert.equal(existsSync(join(workspace, 'blobs')), false)
    assert.equal(EsWriter.EventStoreJournalWriter__get_FilePath(writer), '')

    // BlobWriter uses Git ODB — still no blobs/ mkdir.
    const receipt = mustOk(await writer.BlobWriter.Write('large-body\n'), 'blob write')
    assert.match(idValue.blobRef(receipt.BlobRef), /^blobs\/[0-9a-f]{40}$/)
    assert.equal(existsSync(join(workspace, 'blobs')), false)
    assert.equal(mustOk(await writer.BlobWriter.Read(receipt.BlobRef), 'blob read'), 'large-body\n')

    assert.equal(Number(idValue.localSeq(init.LocalSeq)), 1)
    writer.Dispose()
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})

test('append_failure_poisons_and_returns_CommitUnknown', async () => {
  const raw = GitRaw.GitRawStore_createInMemory()
  const inner = Store.EventStore_create(raw)
  let appendCalls = 0
  const flaky = {
    OpenSnapshot: () => inner.OpenSnapshot(),
    Refresh: () => inner.Refresh(),
    Merge: (snapshots) => inner.Merge(snapshots),
    Publish: (candidate) => inner.Publish(candidate),
    Converge: (remote) => inner.Converge(remote),
    Append: async (baseSnapshot, events) => {
      appendCalls += 1
      if (appendCalls === 1) return inner.Append(baseSnapshot, events)
      return errorResult(Persist.AppendError.AppendRetryExhausted)
    },
  }

  const { writer } = await createPair(flaky, raw)
  assert.equal(appendCalls, 1)

  const result = await EsWriter.EventStoreJournalWriter__Append(writer, stream.session(SESSION), undefined, CLOSED_FACT)
  assert.equal(caseOf(result), 'CommitUnknown')
  assert.equal(caseOf(result.fields[1]), 'WriteFailed')
  assert.match(String(result.fields[1].fields[0]), /retry exhausted/i)
  assert.equal(EsWriter.EventStoreJournalWriter__get_IsPoisoned(writer), true)

  const again = await EsWriter.EventStoreJournalWriter__Append(writer, stream.session(SESSION), undefined, CLOSED_FACT)
  assert.equal(caseOf(again), 'CommitUnknown')
  assert.match(String(again.fields[1].fields[0]), /poisoned or disposed/i)

  writer.Dispose()
})

test('createFromEventStore_folds_init_and_appendAgent_without_ndjson', async () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-es-agent-journal-'))
  try {
    const raw = GitRaw.GitRawStore_createInMemory()
    const store = Store.EventStore_create(raw)
    const { writer, init } = await createPair(store, raw)

    const journal = mustOk(
      AgentJournalMod.AgentJournalModule_createFromEventStore(writer, init),
      'createFromEventStore',
    )

    const before = Persist.GitObjectIdModule_value(await raw.ReadRef(Persist.StoreRef_canonical))
    const append = await AgentJournalMod.AgentJournalModule_appendAgent(
      stream.session(SESSION),
      undefined,
      CLOSED_AGENT,
      journal,
    )
    assert.equal(caseOf(append), 'Ok', payloadOf(append))

    const after = Persist.GitObjectIdModule_value(await raw.ReadRef(Persist.StoreRef_canonical))
    assert.notEqual(after, before)
    assert.deepEqual(collectNdjson(workspace), [])
    assert.equal(existsSync(join(workspace, 'blobs')), false)

    journal.Dispose()
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})
