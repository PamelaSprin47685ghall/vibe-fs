import assert from 'node:assert/strict'
import test from 'node:test'
import * as index from '../../../dist/Knowledge/Casebook/IndexSurface.js'

test('WHAT[KNOWLEDGE-REUSE-012] CASEBOOK_index_exposes_shelfmark_and_canonical_question_only', async () => {
  const snapshot = index.createIndexSnapshot([{ id: 'c1', question: 'how to test?', answer: 'hidden', extra: 123 }])
  assert.deepEqual(snapshot, [{ shelfmark: 'c1', question: 'how to test?' }])
})

test('WHAT[KNOWLEDGE-REUSE-012] CASEBOOK_shelfmark_is_stable_and_not_the_session_identity', () => {
  const mark = index.shelfmarkFor('ses-123', 'question text')
  assert.notEqual(mark, 'ses-123')
  assert.equal(mark, index.shelfmarkFor('ses-456', 'question text'))
})

test('WHAT[KNOWLEDGE-REUSE-012] CASEBOOK_invalidate_then_refresh_advances_epoch', async () => {
  let idx = index.empty
  idx = index.advanceEpoch(idx)
  assert.equal(idx.epoch, 1)
})

test('WHAT[KNOWLEDGE-REUSE-012] CASEBOOK_visible_set_change_advances_epoch', async () => {
  let idx = index.empty
  idx = index.updateVisibleSet(['c1'], idx)
  assert.equal(idx.epoch, 1)
})
