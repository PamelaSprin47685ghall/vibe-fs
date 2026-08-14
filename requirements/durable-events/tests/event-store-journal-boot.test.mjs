// FROZEN — 2026-08-14. Boot replay is owned by CanonicalIntegrator over local writer files.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { agentFact, caseOf, fold, idValue, payloadOf, runtimeId, sessionId, stream, utcOffset } from '../../verification-system/tests/support/domain.mjs'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const EsWriter = await import('../../../dist/Journal/EventStoreJournalWriter.js')
const AgentJournal = await import('../../../dist/Journal/AgentJournal.js')
const createFn = Object.entries(EsWriter).find(([name]) => name.startsWith('EventStoreJournalWriter_create'))?.[1]
const resumeFn = Object.entries(EsWriter).find(([name]) => name.startsWith('EventStoreJournalWriter_resumeOrCreate'))?.[1]
const SESSION = sessionId('ses_es_boot')
const CLOSED = agentFact('CompanionBloggerClosed', { SessionId: SESSION })
const mustOk = (r) => { assert.equal(caseOf(r), 'Ok'); return payloadOf(r) }

test('restart_replays_prior_writer_files_then_fresh_runtime_starts_LocalSeq_at_1', async () => {
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

test('empty_boot_publishes_only_RuntimeStarted_into_local_writer_truth', async () => {
  const local = createLocalEventStore({ writerId: 'boot-empty' })
  try {
    const resumed = mustOk(await resumeFn(runtimeId('rt_empty'), 6001, utcOffset('2026-05-01T00:00:00Z'), local.store))
    assert.equal(Number(idValue.localSeq(resumed[1].LocalSeq)), 1)
    assert.equal(caseOf(resumed[1].Fact), 'Runtime')
    assert.ok(local.store.TryCurrent('Journal'))
    resumed[0].Release?.()
  } finally {
    local.close()
  }
})

test('boot_and_live_use_one_CanonicalIntegrator_program', async () => {
  const { readFile } = await import('node:fs/promises')
  const integrator = await readFile(new URL('../../../src/Wanxiangshu/Infrastructure/Persist/CanonicalIntegrator.fs', import.meta.url), 'utf8')
  const writer = await readFile(new URL('../../../src/Wanxiangshu/Journal/EventStoreJournalWriter.fs', import.meta.url), 'utf8')
  assert.match(integrator, /EventKWayMerge\.merge/)
  assert.match(integrator, /integrateOne/)
  assert.match(integrator, /integrateLive/)
  assert.doesNotMatch(writer, /readStreams|loadEvent|Fold\.apply|OpenSnapshot/)
})
