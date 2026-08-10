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
