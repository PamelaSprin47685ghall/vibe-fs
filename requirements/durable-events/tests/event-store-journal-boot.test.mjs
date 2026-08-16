// Boot replay is owned by CanonicalIntegrator over local writer files.

import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import { agentFact, caseOf, fold, idValue, payloadOf, runtimeId, sessionId, stream, utcOffset } from '../../verification-system/tests/support/domain.mjs'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const EsWriter = await import('../../../dist/Persistence/Journal/EventStoreJournalWriter.js')
const AgentJournal = await import('../../../dist/Persistence/Journal/AgentJournal.js')
const createFn = Object.entries(EsWriter).find(([name]) => name.startsWith('EventStoreJournalWriter_create'))?.[1]
const resumeFn = Object.entries(EsWriter).find(([name]) => name.startsWith('EventStoreJournalWriter_resumeOrCreate'))?.[1]
const SESSION = sessionId('ses_es_boot')
const CLOSED = agentFact('CompanionBloggerClosed', { SessionId: SESSION })
const mustOk = (r) => { assert.equal(caseOf(r), 'Ok'); return payloadOf(r) }

test('WHAT[DURABLE-EVENTS-013] restart_replays_prior_writer_files_then_fresh_runtime_starts_LocalSeq_at_1', async () => {
  const first = createLocalEventStore({ writerId: 'boot-writer-a' })
  try {
    const [writer, init] = await createFn(runtimeId('rt_before'), 4242, utcOffset('2026-04-01T00:00:00Z'), first.store)
    assert.equal(Number(idValue.localSeq(init.LocalSeq)), 1)
    const journal = mustOk(AgentJournal.AgentJournalModule_createFromEventStore(writer, init))
    assert.equal(caseOf(await AgentJournal.AgentJournalModule_appendAgent(stream.session(SESSION), undefined, CLOSED, journal)), 'Ok')
    journal.Dispose?.()

    const restarted = createLocalEventStore({ commonDir: first.commonDir, writerId: 'boot-writer-b' })
    const resumed = mustOk(await resumeFn(runtimeId('rt_after'), 5252, utcOffset('2026-04-02T00:00:00Z'), restarted.store))
    const [nextWriter, nextInit, projection] = resumed
    assert.equal(Number(idValue.localSeq(nextInit.LocalSeq)), 1, 'fresh RuntimeId owns a fresh LocalSeq domain')
    assert.ok(fold.session(projection, 'ses_es_boot'), 'prior journal fact is rebuilt only through Integrator boot replay')
    const nextJournal = mustOk(AgentJournal.AgentJournalModule_createFromProjection(nextWriter, projection))
    assert.ok(fold.session(AgentJournal.AgentJournalModule_snapshot(nextJournal), 'ses_es_boot'))
    nextJournal.Dispose?.()
  } finally {
    first.close()
  }
})

test('WHAT[DURABLE-EVENTS-020] empty_boot_is_read_only_and_keeps_RuntimeStarted_in_memory_until_activation', async () => {
  const local = createLocalEventStore({ writerId: 'boot-empty' })
  try {
    const resumed = mustOk(await resumeFn(runtimeId('rt_empty'), 6001, utcOffset('2026-05-01T00:00:00Z'), local.store))
    assert.equal(Number(idValue.localSeq(resumed[1].LocalSeq)), 1)
    assert.equal(caseOf(resumed[1].Fact), 'Runtime')
    assert.equal(existsSync(join(local.commonDir, 'wanxiang', 'events', 'boot-empty.ndjson')), false)
    resumed[0].Release?.()
  } finally {
    local.close()
  }
})

test('WHAT[DURABLE-EVENTS-013] boot_and_live_use_one_CanonicalIntegrator_program', async () => {
  const { readFile } = await import('node:fs/promises')
  const integrator = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/CanonicalIntegrator.fs', import.meta.url), 'utf8')
  const writer = await readFile(new URL('../../../src/Wanxiangshu/Persistence/Journal/EventStoreJournalWriter.fs', import.meta.url), 'utf8')
  assert.match(integrator, /EventKWayMerge\.merge/)
  assert.match(integrator, /integrateOne/)
  assert.match(integrator, /prepareLive/)
  assert.doesNotMatch(writer, /readStreams|loadEvent|Fold\.apply|OpenSnapshot/)
})
