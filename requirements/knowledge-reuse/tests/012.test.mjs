import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as index from '../../../dist/Repository/Knowledge/Casebook/IndexSurface.js'

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })

const caseRec = (sessionId, q, a, observations = []) => ({
  sessionId,
  q,
  a,
  observations,
  lastAccessOrder: 0,
})

const openStore = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-casebook-index-'))
  const handle = eventStore.create(dir, 'casebook-index')
  return { dir, handle, close: () => { eventStore.dispose(handle); rmSync(dir, { recursive: true, force: true }) } }
}

test('WHAT[knowledge-reuse-012] CASEBOOK_index_exposes_shelfmark_and_canonical_question_only', async () => {
  const local = openStore()
  try {
    const question = 'Persistence after restart'
    const archived = await casebook.archive(local.handle, caseRec('idx-private-1', question, 'A', [fileRead('a.txt', 'h1')]))
    assert.equal(archived.ok, true)

    const snap = await index.refresh(local.handle, 10)
    const entries = snap.cases
    assert.equal(entries.length, 1)
    assert.equal(entries[0].question, question)
    assert.deepEqual(Object.keys(entries[0]).sort(), ['question', 'shelfmark'])
    assert.match(entries[0].shelfmark, /^Persistence after restart · [0-9a-f]{8}$/)
    assert.equal(entries[0].shelfmark.includes('idx-private-1'), false)
    assert.equal('sessionId' in entries[0], false)

    const cached = index.tryGet()
    assert.equal(cached.epoch, snap.epoch)
    const found = await index.resolve(local.handle, 10, entries[0].shelfmark)
    assert.equal(found.ok, true)
    assert.equal(found.value.sessionId, 'idx-private-1')
  } finally {
    local.close()
  }
})

test('WHAT[knowledge-reuse-012] CASEBOOK_shelfmark_is_stable_and_not_the_session_identity', () => {
  const first = index.shelfmarkFor('private-session-a', '## Restart behavior\nfull canonical question')
  const again = index.shelfmarkFor('private-session-a', '## Restart behavior\nfull canonical question')
  const other = index.shelfmarkFor('private-session-b', '## Restart behavior\nfull canonical question')
  assert.equal(first, again)
  assert.notEqual(first, other)
  assert.match(first, /^Restart behavior · [0-9a-f]{8}$/)
  assert.equal(first.includes('private-session-a'), false)
})

test('WHAT[knowledge-reuse-012] CASEBOOK_invalidate_then_refresh_advances_epoch', async () => {
  const local = openStore()
  try {
    assert.equal((await casebook.archive(local.handle, caseRec('idx-s2', 'Q', 'A'))).ok, true)
    const first = await index.refresh(local.handle, 10)
    const stable = await index.refresh(local.handle, 10)
    assert.equal(stable.epoch, first.epoch)
    index.invalidate()
    const advanced = await index.refresh(local.handle, 10)
    assert.equal(advanced.epoch, first.epoch + 1n)
  } finally {
    local.close()
  }
})

test('WHAT[knowledge-reuse-012] CASEBOOK_visible_set_change_advances_epoch', async () => {
  const local = openStore()
  try {
    const empty = await index.refresh(local.handle, 10)
    assert.equal((await casebook.archive(local.handle, caseRec('idx-s3', 'A new canonical question', 'A'))).ok, true)
    const after = await index.refresh(local.handle, 10)
    assert.equal(after.epoch > empty.epoch, true)
    assert.equal(after.cases.some((entry) => entry.question === 'A new canonical question'), true)
  } finally {
    local.close()
  }
})

test('WHAT[knowledge-reuse-012] CASEBOOK_shelfmark_of_a_long_title_survives_utf8_and_still_resolves', async () => {
  // The title is cut to 71 UTF-16 code units. When that cut falls inside a surrogate pair the shelfmark
  // carries a lone surrogate; any UTF-8 transport (the provider wire) turns it into U+FFFD, so the
  // shelfmark the model sends back no longer addresses the Case it was shown.
  const local = openStore()
  try {
    const question = 'a'.repeat(70) + '😀 why does the cache forget after restart?'
    assert.equal((await casebook.archive(local.handle, caseRec('idx-long-title', question, 'A'))).ok, true)
    const snap = await index.refresh(local.handle, 10)
    const shown = snap.cases.find(entry => entry.question === question).shelfmark
    assert.equal(shown.isWellFormed(), true, 'a public locator must be a well-formed string')
    const echoed = Buffer.from(shown, 'utf8').toString('utf8')
    const found = await index.resolve(local.handle, 10, echoed)
    assert.equal(found.ok, true)
    assert.equal(found.value?.sessionId, 'idx-long-title', 'the shelfmark echoed over UTF-8 must address the shown Case')
  } finally {
    local.close()
  }
})
