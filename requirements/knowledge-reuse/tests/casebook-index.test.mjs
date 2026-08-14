// Provider-safe CasebookIndexSnapshot: shelfmark + canonical question only.

import assert from 'node:assert/strict'
import test from 'node:test'

import { tryGet, refresh, invalidate, resolve, shelfmarkFor } from '../../../dist/Infrastructure/CasebookIndex.js'
import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { listItems, resultOf, toList } from '../../../tests/unit/support/domain.mjs'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, hash) => new Observation(obsIndex('FileRead'), [path, hash])

const caseRec = (sessionId, q, a, observations = []) => ({
  SessionId: sessionId,
  Q: q,
  A: a,
  Observations: toList(observations),
  LastAccessOrder: 0,
})

test('CASEBOOK_index_exposes_shelfmark_and_canonical_question_only', async () => {
  const raw = createRaw()
  const store = createStore(raw)
  const question = 'Persistence after restart'

  const archived = resultOf(await archive(store, raw, caseRec('idx-private-1', question, 'A', [fileRead('a.txt', 'h1')])))
  assert.equal(archived.ok, true, `archive ok, got ${JSON.stringify(archived.error)}`)

  const snap = await refresh(store, raw, 10)
  assert.equal(typeof snap.Epoch, 'bigint')
  const entries = listItems(snap.Cases)
  assert.equal(entries.length, 1)
  assert.equal(entries[0].Question, question)
  assert.match(entries[0].Shelfmark, /^Persistence after restart · [0-9a-f]{8}$/)
  assert.equal(entries[0].Shelfmark.includes('idx-private-1'), false)
  assert.equal('SessionId' in entries[0], false)
  assert.equal('Status' in entries[0], false)
  assert.equal('Freshness' in entries[0], false)

  const cached = tryGet()
  assert.equal(cached !== undefined && cached !== null, true)
  assert.equal(cached.Epoch, snap.Epoch)
  assert.deepEqual(listItems(cached.Cases), entries)

  const found = resultOf(await resolve(store, raw, 10, entries[0].Shelfmark))
  assert.equal(found.ok, true)
  assert.equal(found.value.SessionId, 'idx-private-1', 'internal lookup keeps durable identity behind the shelfmark')
})

test('CASEBOOK_shelfmark_is_stable_and_not_the_session_identity', () => {
  const first = shelfmarkFor('private-session-a', '## Restart behavior\nfull canonical question')
  const again = shelfmarkFor('private-session-a', '## Restart behavior\nfull canonical question')
  const other = shelfmarkFor('private-session-b', '## Restart behavior\nfull canonical question')

  assert.equal(first, again)
  assert.notEqual(first, other)
  assert.match(first, /^Restart behavior · [0-9a-f]{8}$/)
  assert.equal(first.includes('private-session-a'), false)
})

test('CASEBOOK_invalidate_then_refresh_advances_epoch', async () => {
  const raw = createRaw()
  const store = createStore(raw)
  resultOf(await archive(store, raw, caseRec('idx-s2', 'Q', 'A', [])))

  const first = await refresh(store, raw, 10)
  const stable = await refresh(store, raw, 10)
  assert.equal(stable.Epoch, first.Epoch, 'same visible index without invalidate keeps epoch')
  assert.deepEqual(listItems(stable.Cases), listItems(first.Cases))

  invalidate()
  const advanced = await refresh(store, raw, 10)
  assert.equal(advanced.Epoch, first.Epoch + 1n, 'invalidate forces epoch bump on next refresh')
  assert.equal(listItems(advanced.Cases).length, 1)
})

test('CASEBOOK_visible_set_change_advances_epoch', async () => {
  const raw = createRaw()
  const store = createStore(raw)

  const empty = await refresh(store, raw, 10)
  resultOf(await archive(store, raw, caseRec('idx-s3', 'A new canonical question', 'A', [])))
  const after = await refresh(store, raw, 10)

  assert.equal(after.Epoch > empty.Epoch, true, 'provider-visible index change must advance epoch')
  assert.equal(listItems(after.Cases).some((entry) => entry.Question === 'A new canonical question'), true)
})
