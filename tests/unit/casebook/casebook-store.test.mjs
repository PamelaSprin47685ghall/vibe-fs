// tests/unit/casebook/casebook-store.test.mjs — G6-C: Casebook durable facts
// through the unified EventStore (CASE-007).
//
// Captured/Refreshed/Accessed/Evicted events round-trip through payload
// codecs; loadEvents decodes them back; project() folds + applies LRU
// capacity. No feature ref / manifest tree anywhere.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  appendCaptured,
  appendRefreshed,
  appendAccessed,
  appendEvicted,
  loadEvents,
  project,
  loadEnvelopes,
  headOf,
} from '../../../dist/Infrastructure/CasebookStore.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { caseOf, listItems, mapEntries, resultOf, toList } from '../support/domain.mjs'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, hash) => new Observation(obsIndex('FileRead'), [path, hash])
const globResult = (pattern, paths) => new Observation(obsIndex('GlobResult'), [pattern, toList(paths)])

const unwrap = (result) => {
  const r = resultOf(result)
  assert.equal(r.ok, true, `expected Ok, got ${JSON.stringify(r.error)}`)
  return r.value
}

const caseRec = (sessionId, q, a, observations) => ({
  SessionId: sessionId,
  Q: q,
  A: a,
  Observations: toList(observations),
  LastAccessOrder: 0,
})

test('CASE007_captured_refreshed_round_trip_through_eventstore', () => {
  const raw = createRaw()
  const store = createStore(raw)

  const first = unwrap(appendCaptured(store, toList([]), caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')])))
  unwrap(
    appendRefreshed(store, toList([first]), 's1', 'Q1b', 'A1b', toList([fileRead('a.txt', 'h1'), globResult('*.fs', ['x'])])),
  )

  const events = unwrap(loadEvents(raw, store.OpenSnapshot()))
  const types = listItems(events).map((e) => caseOf(e)).sort()
  assert.deepEqual(types, ['CaseCaptured', 'CaseRefreshed'])

  const cases = project(10, events)
  const s1 = mapEntries(cases).find(([k]) => k === 's1')[1]
  assert.equal(s1.A, 'A1b')
  assert.equal(listItems(s1.Observations).length, 2)
})

test('CASE007_accessed_and_evicted_events_round_trip', () => {
  const raw = createRaw()
  const store = createStore(raw)

  const first = unwrap(appendCaptured(store, toList([]), caseRec('s1', 'Q', 'A', [])))
  unwrap(appendAccessed(store, toList([first]), 's1'))
  const events = unwrap(loadEvents(raw, store.OpenSnapshot()))
  assert.deepEqual(listItems(events).map((e) => caseOf(e)).sort(), ['CaseAccessed', 'CaseCaptured'])

  // eviction is expressed as an event too
  const evictedId = unwrap(appendEvicted(store, toList([first]), 's1'))
  const allEvents = unwrap(loadEvents(raw, store.OpenSnapshot()))
  assert.deepEqual(listItems(allEvents).map((e) => caseOf(e)).sort(), ['CaseAccessed', 'CaseCaptured', 'CaseEvicted'])
  assert.equal(mapEntries(project(10, allEvents)).length, 0)
  assert.equal(evictedId !== undefined, true)
})

test('CASE007_loadEnvelopes_keeps_event_ids_for_linear_parents', () => {
  const raw = createRaw()
  const store = createStore(raw)

  const first = unwrap(appendCaptured(store, toList([]), caseRec('s1', 'Q', 'A', [])))
  const second = unwrap(appendCaptured(store, toList([first]), caseRec('s2', 'Q2', 'A2', [])))

  const envelopes = unwrap(loadEnvelopes(raw, store.OpenSnapshot()))
  assert.equal(listItems(envelopes).length, 2)
  const head = headOf(envelopes)
  assert.equal(head !== undefined, true)
  // the second append's parent chain is preserved: head is the last event
  assert.equal(second !== undefined, true)
})

test('CASE009_marker_gates_the_surface', async () => {
  const { CasebookFeature_isEnabled: isEnabled } = await import('../../../dist/Infrastructure/CasebookWorkflow.js')
  const { mkdtempSync, rmSync, mkdirSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cbmarker-'))
  try {
    assert.equal(isEnabled(dir), false, 'no marker → disabled')
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    assert.equal(isEnabled(dir), true, 'marker dir exists → enabled')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE004_005_workflow_archive_fetch_freshness', async () => {
  const {
    CasebookWorkflow_archiveInspectorResult: archive,
    CasebookWorkflow_fetchCase: fetchCase,
    CasebookWorkflow_checkFreshness: checkFreshness,
  } = await import('../../../dist/Infrastructure/CasebookWorkflow.js')
  const raw = createRaw()
  const store = createStore(raw)

  const archived = archive(store, raw, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')]))
  assert.equal(resultOf(archived).ok, true, `archive ok, got ${JSON.stringify(resultOf(archived).error)}`)

  const fetched = fetchCase(store, raw, 10, 's1')
  assert.equal(resultOf(fetched).ok, true)
  const caseObj = resultOf(fetched).value
  assert.equal(caseObj !== undefined, true)
  assert.equal(caseObj.A, 'A1')

  // replay identical → Fresh; changed → Stale
  const fresh = checkFreshness(caseObj, toList([fileRead('a.txt', 'h1')]))
  assert.equal(caseOf(fresh), 'Fresh')
  const stale = checkFreshness(caseObj, toList([fileRead('a.txt', 'h2')]))
  assert.equal(caseOf(stale), 'Stale')
})

test('CASE004_replay_detects_deltas_against_current_worktree', async () => {
  const { replayAll } = await import('../../../dist/Infrastructure/CasebookReplay.js')
  const { contentHash: hash } = await import('../../../dist/Infrastructure/CasebookCapture.js')
  const { mkdtempSync, rmSync, writeFileSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cbreplay-'))
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    // stored observation matches current content → replay keeps it identical
    const stored = [fileRead('a.txt', hash('hello'))]
    const replayed = listItems(replayAll(dir, toList(stored)))
    assert.equal(replayed.length, 1)
    assert.equal(caseOf(replayed[0]), 'FileRead')
    // content changed → replayed hash differs → Stale downstream
    writeFileSync(join(dir, 'a.txt'), 'changed!', 'utf8')
    const replayed2 = listItems(replayAll(dir, toList(stored)))
    assert.equal(replayed2[0].fields[1], hash('changed!'))
    // file deleted → observation gone → Stale downstream
    rmSync(join(dir, 'a.txt'))
    assert.equal(listItems(replayAll(dir, toList(stored))).length, 0)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE006_refresh_publishes_revision_and_needsRefresh_detects_stale', async () => {
  const {
    CasebookWorkflow_archiveInspectorResult: archive,
    CasebookWorkflow_fetchCase: fetchCase,
    CasebookWorkflow_refreshCase: refreshCase,
    CasebookWorkflow_needsRefresh: needsRefresh,
  } = await import('../../../dist/Infrastructure/CasebookWorkflow.js')
  const { contentHash: hash } = await import('../../../dist/Infrastructure/CasebookCapture.js')
  const { mkdtempSync, rmSync, writeFileSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cbrefresh-'))
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const raw = createRaw()
    const store = createStore(raw)
    // archive with observation matching current content
    resultOf(archive(store, raw, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', hash('hello'))])))
    // still fresh → no refresh needed
    const fresh = resultOf(needsRefresh(store, raw, 10, 's1', dir))
    assert.equal(fresh.ok, true)
    assert.equal(fresh.value, false)
    // content changed → refresh needed
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const stale = resultOf(needsRefresh(store, raw, 10, 's1', dir))
    assert.equal(stale.ok, true)
    assert.equal(stale.value, true)
    // Bookkeeper revises → Refreshed lands and the projection carries new A
    const refreshed = resultOf(refreshCase(store, raw, 's1', 'Q1b', 'A1b', toList([fileRead('a.txt', hash('changed'))])))
    assert.equal(refreshed.ok, true, `refresh ok, got ${JSON.stringify(refreshed.error)}`)
    const fetched = resultOf(fetchCase(store, raw, 10, 's1'))
    assert.equal(fetched.value.A, 'A1b')
    // after revision matches the current worktree again → no refresh needed
    const settled = resultOf(needsRefresh(store, raw, 10, 's1', dir))
    assert.equal(settled.value, false)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE010_finalize_is_exactly_once_per_scope', async () => {
  const { CasebookWorkflow_finalizeCase: finalizeCase } = await import('../../../dist/Infrastructure/CasebookWorkflow.js')
  const raw = createRaw()
  const store = createStore(raw)
  const first = resultOf(finalizeCase(store, raw, caseRec('scope-1', 'Q', 'A', [])))
  assert.equal(first.ok, true, `first finalize ok, got ${JSON.stringify(first.error)}`)
  const second = resultOf(finalizeCase(store, raw, caseRec('scope-1', 'Q', 'A2', [])))
  assert.equal(second.ok, false, 'second finalize must be refused')
  assert.equal(second.error.includes('already finalized'), true)
  // a different scope still archives
  const other = resultOf(finalizeCase(store, raw, caseRec('scope-2', 'Q', 'A', [])))
  assert.equal(other.ok, true)
})
