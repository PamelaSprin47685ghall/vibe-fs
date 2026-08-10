// tests/unit/journal/event-store-journal-boot.test.mjs
// W1-boot: resumeOrCreate + createFromProjection (no NDJSON / no blobs/ dir).

import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, readdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  agentFact,
  caseOf,
  eventId,
  fact,
  fold,
  idValue,
  isSome,
  listItems,
  payloadOf,
  runtimeId,
  sessionId,
  stream,
  toList,
  utcOffset,
} from '../support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')
const EsWriter = await import('../../../dist/Journal/EventStoreJournalWriter.js')
const AgentJournalMod = await import('../../../dist/Journal/AgentJournal.js')

const SESSION = sessionId('ses_es_boot')
const CLOSED_AGENT = agentFact('CompanionBloggerClosed', { SessionId: SESSION })
const CLOSED_FACT = fact('CompanionBloggerClosed', { SessionId: SESSION })

const oidValue = (rootOid) => Persist.GitObjectIdModule_value(Persist.RootOidModule_value(rootOid))
const snapshotOid = (snapshot) => oidValue(snapshot.RootOid)

const mustOk = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Ok', `${label} should be Ok, got ${caseOf(result)}: ${payloadOf(result)}`)
  return payloadOf(result)
}

const resolveExport = (mod, prefixes) => {
  for (const prefix of prefixes) {
    const hit = Object.entries(mod).find(([name]) => name.startsWith(prefix))
    if (hit) return hit[1]
  }
  return undefined
}

const createFn = () => {
  const create =
    EsWriter.EventStoreJournalWriter_create_Z10F3E7A9 ??
    resolveExport(EsWriter, ['EventStoreJournalWriter_create'])
  assert.equal(typeof create, 'function', 'EventStoreJournalWriter.create missing from dist')
  return create
}

const resumeOrCreateFn = () => {
  const resume =
    EsWriter.EventStoreJournalWriter_resumeOrCreate_Z10F3E7A9 ??
    resolveExport(EsWriter, ['EventStoreJournalWriter_resumeOrCreate'])
  assert.equal(typeof resume, 'function', 'EventStoreJournalWriter.resumeOrCreate missing from dist')
  return resume
}

const createPair = (store, raw) => {
  const pair = createFn()(runtimeId('rt_es_boot'), 4242, utcOffset('2026-04-01T00:00:00Z'), store, raw ?? undefined)
  return { writer: pair[0], init: pair[1] }
}

const resumeTriple = (store, raw, overrides = {}) => {
  const result = resumeOrCreateFn()(
    runtimeId(overrides.runtime ?? 'rt_es_boot_resume'),
    overrides.pid ?? 5252,
    utcOffset(overrides.startedAt ?? '2026-04-02T00:00:00Z'),
    store,
    raw,
  )
  const triple = mustOk(result, 'resumeOrCreate')
  return { writer: triple[0], init: triple[1], projection: triple[2], result }
}

const appendWriter = (writer, streamId, envelopeFact, run) => {
  const result = EsWriter.EventStoreJournalWriter__Append(writer, streamId, run, envelopeFact)
  return caseOf(result) === 'Committed'
    ? { committed: true, envelope: payloadOf(result) }
    : {
        committed: false,
        eventId: idValue.event(result.fields[0]),
        failure: caseOf(result.fields[1]),
        reason: result.fields[1]?.fields?.[0],
      }
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

const streamId = (v) => Domain.EventStreamIdModule_create(v)

test('resumeOrCreate_continues_LocalSeq_and_preserves_prior_projection', () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-es-journal-boot-'))
  try {
    const raw = GitRaw.GitRawStore_createInMemory()
    const store = Store.EventStore_create(raw)
    const { writer, init } = createPair(store, raw)
    assert.equal(Number(idValue.localSeq(init.LocalSeq)), 1)

    const appended = appendWriter(writer, stream.session(SESSION), CLOSED_FACT)
    assert.equal(appended.committed, true, appended.reason)
    assert.equal(Number(idValue.localSeq(appended.envelope.LocalSeq)), 2)
    assert.equal(Number(EsWriter.EventStoreJournalWriter__get_LastCommittedLocalSeq(writer)), 2)
    writer.Dispose()

    const resumed = resumeTriple(store, raw)
    assert.equal(Number(idValue.localSeq(resumed.init.LocalSeq)), 3)
    assert.equal(Number(EsWriter.EventStoreJournalWriter__get_LastCommittedLocalSeq(resumed.writer)), 3)
    assert.equal(caseOf(resumed.init.Stream), 'Workspace')
    assert.equal(caseOf(resumed.init.Fact), 'Runtime')

    // Prior CompanionBloggerClosed remains visible after boot fold + RuntimeStarted.
    assert.ok(fold.session(resumed.projection, 'ses_es_boot'), 'projection should contain prior session fact')

    const journal = mustOk(
      AgentJournalMod.AgentJournalModule_createFromProjection(resumed.writer, resumed.projection),
      'createFromProjection',
    )
    const snap = AgentJournalMod.AgentJournalModule_snapshot(journal)
    assert.ok(fold.session(snap, 'ses_es_boot'), 'journal snapshot should retain prior fact')

    const next = AgentJournalMod.AgentJournalModule_appendAgent(
      stream.session(SESSION),
      undefined,
      CLOSED_AGENT,
      journal,
    )
    assert.equal(caseOf(next), 'Ok', payloadOf(next))
    assert.equal(Number(EsWriter.EventStoreJournalWriter__get_LastCommittedLocalSeq(resumed.writer)), 4)

    assert.deepEqual(collectNdjson(workspace), [])
    assert.equal(existsSync(join(workspace, 'blobs')), false)

    journal.Dispose()
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})

test('resumeOrCreate_empty_store_matches_create_plus_createFromProjection', () => {
  const workspace = mkdtempSync(join(tmpdir(), 'wxs-es-journal-boot-empty-'))
  try {
    const raw = GitRaw.GitRawStore_createInMemory()
    const store = Store.EventStore_create(raw)

    const resumed = resumeTriple(store, raw, {
      runtime: 'rt_es_boot_empty',
      pid: 6001,
      startedAt: '2026-05-01T00:00:00Z',
    })
    assert.equal(Number(idValue.localSeq(resumed.init.LocalSeq)), 1)
    assert.equal(Number(EsWriter.EventStoreJournalWriter__get_LastCommittedLocalSeq(resumed.writer)), 1)
    assert.equal(caseOf(resumed.init.Fact), 'Runtime')

    const journal = mustOk(
      AgentJournalMod.AgentJournalModule_createFromProjection(resumed.writer, resumed.projection),
      'createFromProjection empty',
    )
    assert.equal(idValue.runtime(AgentJournalMod.AgentJournalModule_runtimeId(journal)), 'rt_es_boot_empty')

    const published = raw.ReadRef(Persist.StoreRef_canonical)
    assert.equal(isSome(published), true)
    assert.equal(Persist.GitObjectIdModule_value(published), snapshotOid(store.OpenSnapshot()))

    const blobs = mustOk(GitRaw.GitRawStore_listEventBlobs(raw, store.OpenSnapshot().RootOid))
    assert.equal(listItems(blobs).length, 1, 'empty resume should publish only RuntimeStarted')

    assert.deepEqual(collectNdjson(workspace), [])
    assert.equal(existsSync(join(workspace, 'blobs')), false)

    journal.Dispose()
  } finally {
    rmSync(workspace, { recursive: true, force: true })
  }
})

test('resumeOrCreate_malformed_journal_envelope_returns_Boot_FoldRejection', () => {
  const raw = GitRaw.GitRawStore_createInMemory()
  const store = Store.EventStore_create(raw)

  const bad = new Domain.EventEnvelope(
    eventId('dddddddddddddddddddddddddddddddddddddddd'),
    streamId('journal/workspace'),
    'JournalEnvelope',
    toList([]),
    { not: 'a-journal-envelope' },
    toList([]),
  )
  mustOk(store.Append(store.OpenSnapshot(), toList([bad])), 'seed malformed JournalEnvelope')

  const result = resumeOrCreateFn()(
    runtimeId('rt_es_boot_bad'),
    7001,
    utcOffset('2026-06-01T00:00:00Z'),
    store,
    raw,
  )
  assert.equal(caseOf(result), 'Error', 'malformed journal payload must fail closed')
  const rejection = payloadOf(result)
  assert.equal(rejection.Fact, 'Boot')
  assert.equal(typeof rejection.Reason, 'string')
  assert.ok(rejection.Reason.length > 0)
})

test('resumeOrCreate_skips_non_journal_Job_events', () => {
  const raw = GitRaw.GitRawStore_createInMemory()
  const store = Store.EventStore_create(raw)

  const job = new Domain.EventEnvelope(
    eventId('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'),
    streamId('job/main'),
    'JobRequested',
    toList([]),
    { status: 'open' },
    toList([]),
  )
  mustOk(store.Append(store.OpenSnapshot(), toList([job])), 'seed JobRequested')

  const resumed = resumeTriple(store, raw, { runtime: 'rt_es_boot_job', pid: 8001 })
  assert.equal(Number(idValue.localSeq(resumed.init.LocalSeq)), 1)
  assert.equal(Number(EsWriter.EventStoreJournalWriter__get_LastCommittedLocalSeq(resumed.writer)), 1)

  const blobs = mustOk(GitRaw.GitRawStore_listEventBlobs(raw, store.OpenSnapshot().RootOid))
  assert.equal(listItems(blobs).length, 2, 'JobRequested + RuntimeStarted')

  resumed.writer.Dispose()
})
