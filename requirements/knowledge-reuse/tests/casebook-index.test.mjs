// FROZEN — 2026-08-14. Casebook index derives only from canonical Integrator Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'

import { tryGet, refresh, invalidate, resolve, shelfmarkFor } from '../../../dist/Repository/Knowledge/Casebook/Index.js'
import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Repository/Knowledge/Casebook/Workflow.js'
import { Observation } from '../../../dist/Repository/Knowledge/Casebook/Model.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { listItems, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, hash) => new Observation(obsIndex('FileRead'), [path, hash])
const caseRec = (sessionId, q, a, observations = []) => ({ SessionId: sessionId, Q: q, A: a, Observations: toList(observations), LastAccessOrder: 0 })

test('CASEBOOK_index_exposes_shelfmark_and_canonical_question_only', async () => {
  const local = createLocalEventStore()
  try {
    const question = 'Persistence after restart'
    const archived = resultOf(await archive(local.store, caseRec('idx-private-1', question, 'A', [fileRead('a.txt', 'h1')])))
    assert.equal(archived.ok, true)

    const snap = await refresh(local.store, 10)
    const entries = listItems(snap.Cases)
    assert.equal(entries.length, 1)
    assert.equal(entries[0].Question, question)
    assert.match(entries[0].Shelfmark, /^Persistence after restart · [0-9a-f]{8}$/)
    assert.equal(entries[0].Shelfmark.includes('idx-private-1'), false)
    assert.equal('SessionId' in entries[0], false)

    const cached = tryGet()
    assert.equal(cached.Epoch, snap.Epoch)
    const found = resultOf(await resolve(local.store, 10, entries[0].Shelfmark))
    assert.equal(found.ok, true)
    assert.equal(found.value.SessionId, 'idx-private-1')
  } finally {
    local.close()
  }
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
  const local = createLocalEventStore()
  try {
    resultOf(await archive(local.store, caseRec('idx-s2', 'Q', 'A')))
    const first = await refresh(local.store, 10)
    const stable = await refresh(local.store, 10)
    assert.equal(stable.Epoch, first.Epoch)
    invalidate()
    const advanced = await refresh(local.store, 10)
    assert.equal(advanced.Epoch, first.Epoch + 1n)
  } finally {
    local.close()
  }
})

test('CASEBOOK_visible_set_change_advances_epoch', async () => {
  const local = createLocalEventStore()
  try {
    const empty = await refresh(local.store, 10)
    resultOf(await archive(local.store, caseRec('idx-s3', 'A new canonical question', 'A')))
    const after = await refresh(local.store, 10)
    assert.equal(after.Epoch > empty.Epoch, true)
    assert.equal(listItems(after.Cases).some((entry) => entry.Question === 'A new canonical question'), true)
  } finally {
    local.close()
  }
})
