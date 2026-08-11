// tests/unit/casebook/casebook-index.test.mjs — G6-D: process-local
// CasebookIndexSnapshot (PrefixEpoch-stable freeze of archived session ids).
//
// refresh projects the unified EventStore; tryGet returns the last freeze;
// invalidate forces the next refresh to advance epoch. No feature ref.

import assert from 'node:assert/strict'
import test from 'node:test'

import { tryGet, refresh, invalidate } from '../../../dist/Infrastructure/CasebookIndex.js'
import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { listItems, resultOf, toList } from '../support/domain.mjs'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, hash) => new Observation(obsIndex('FileRead'), [path, hash])

const caseRec = (sessionId, q, a, observations = []) => ({
  SessionId: sessionId,
  Q: q,
  A: a,
  Observations: toList(observations),
  LastAccessOrder: 0,
})

test('G6D_index_refresh_sees_archived_session_id', () => {
  const raw = createRaw()
  const store = createStore(raw)

  const archived = resultOf(archive(store, raw, caseRec('idx-s1', 'Q', 'A', [fileRead('a.txt', 'h1')])))
  assert.equal(archived.ok, true, `archive ok, got ${JSON.stringify(archived.error)}`)

  const snap = refresh(store, raw, 10)
  assert.equal(typeof snap.Epoch, 'bigint')
  const ids = listItems(snap.SessionIds)
  assert.equal(ids.includes('idx-s1'), true, `expected idx-s1 in ${JSON.stringify(ids)}`)

  const cached = tryGet()
  assert.equal(cached !== undefined && cached !== null, true)
  assert.equal(cached.Epoch, snap.Epoch)
  assert.deepEqual(listItems(cached.SessionIds), ids)
})

test('G6D_invalidate_then_refresh_advances_epoch', () => {
  const raw = createRaw()
  const store = createStore(raw)
  resultOf(archive(store, raw, caseRec('idx-s2', 'Q', 'A', [])))

  const first = refresh(store, raw, 10)
  const stable = refresh(store, raw, 10)
  assert.equal(stable.Epoch, first.Epoch, 'same content without invalidate keeps epoch')
  assert.deepEqual(listItems(stable.SessionIds), listItems(first.SessionIds))

  invalidate()
  const advanced = refresh(store, raw, 10)
  assert.equal(advanced.Epoch, first.Epoch + 1n, 'invalidate forces epoch bump on next refresh')
  assert.equal(listItems(advanced.SessionIds).includes('idx-s2'), true)
})

test('G6D_set_change_advances_epoch', () => {
  const raw = createRaw()
  const store = createStore(raw)

  const empty = refresh(store, raw, 10)
  resultOf(archive(store, raw, caseRec('idx-s3', 'Q', 'A', [])))
  const after = refresh(store, raw, 10)

  assert.equal(after.Epoch > empty.Epoch, true, 'new session id set must advance epoch')
  assert.equal(listItems(after.SessionIds).includes('idx-s3'), true)
})
