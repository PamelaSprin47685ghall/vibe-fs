import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as index from '../../../dist/Repository/Knowledge/Casebook/IndexSurface.js'
import { sandbox, parse } from './support/casebook.mjs'

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })

const oldShelfmarks = JSON.parse(readFileSync(new URL('./fixtures/host-shelfmarks.json', import.meta.url), 'utf8'))

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

test('WHAT[knowledge-reuse-012] CASEBOOK_preserves_160_old_hash_vectors_and_fetches_an_old_locator', async () => {
  assert.equal(oldShelfmarks.length, 160)
  for (const [identity, question, shelfmark] of oldShelfmarks) {
    assert.equal(index.shelfmarkFor(identity, question), shelfmark, JSON.stringify([identity, question]))
  }
  const local = sandbox()
  try {
    const baseline = await casebook.freezeCompletionState(local.store, local.dir, [])
    const result = await casebook.finalizeEngineerCase(local.store, 'ses_1', 'legacy-locator-trace', 'Question?', 'Legacy answer', [], baseline)
    assert.equal(result.kind, 'finalized')
    assert.equal(parse(await local.fetch('Question? · aa739210')).answer, 'Legacy answer')
  } finally {
    local.close()
  }
})

test('WHAT[knowledge-reuse-012] CASEBOOK_public_unicode_locator_survives_UTF8_and_fetches_the_same_case', async () => {
  const local = sandbox()
  try {
    const question = 'a'.repeat(70) + '😀 tail of a long question'
    const baseline = await casebook.freezeCompletionState(local.store, local.dir, [])
    const result = await casebook.finalizeEngineerCase(local.store, 'unicode-locator-case', 'unicode-locator-trace', question, 'Unicode answer', [], baseline)
    assert.equal(result.kind, 'finalized')
    const snapshot = await index.refresh(local.store, 256)
    const shelfmark = snapshot.cases.find(entry => entry.question === question).shelfmark
    const transported = Buffer.from(shelfmark, 'utf8').toString('utf8')
    assert.equal(parse(await local.fetch(transported)).answer, 'Unicode answer')
    assert.equal(transported, shelfmark)
    assert.equal(shelfmark.isWellFormed(), true)
  } finally {
    local.close()
  }
})
