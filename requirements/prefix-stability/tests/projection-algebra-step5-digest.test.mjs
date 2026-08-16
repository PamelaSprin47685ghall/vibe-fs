// Split from tests/unit/context/projection-algebra.test.mjs (cutover Wave 2a); owner: prefix-stability.
//
// CTX_011 step-5 digest proofs: the cutoff digest truncates exactly at the
// cutoff (cutoff 0 = the EMPTY prefix), and the proof reads the SNAPSHOT not a
// stale closure (COMPANION-011).

import assert from 'node:assert/strict'
import test from 'node:test'
import { listItems, projectionAlgebra, providerProjection, toList } from '../../verification-system/tests/support/domain.mjs'

const semanticView = (raw) => providerProjection.toSemantic(providerProjection.decodeMessageView(toList(raw)))
const sha256 = (input) => `«${input}»`

const stage2Snapshot = (raw, committed = undefined) => ({
  CurrentProjection: semanticView(raw),
  CommittedPrefix: committed,
})

test('WHAT[PREFIX-STABILITY-009] CTX_011_step5_cutoff_digest_truncates_exactly_at_the_cutoff', () => {
  const snapshot = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] },
  ])

  const full = providerProjection.renderSemantic(snapshot.CurrentProjection)

  // Truncate semantics: cutoff 2 keeps the first two messages, the rest are cut.
  const truncated = {
    ...snapshot.CurrentProjection,
    Messages: toList(listItems(snapshot.CurrentProjection.Messages).slice(0, 2)),
  }
  assert.equal(projectionAlgebra.cutoffDigest(sha256, snapshot, 2), sha256(providerProjection.renderSemantic(truncated)))
  assert.notEqual(projectionAlgebra.cutoffDigest(sha256, snapshot, 2), sha256(full), 'a real cutoff changes the digest')

  // cutoff 0 proves the EMPTY prefix — the load-bearing CTX-011 step-5 shape.
  const empty = { ...snapshot.CurrentProjection, Messages: toList([]) }
  assert.equal(projectionAlgebra.cutoffDigest(sha256, snapshot, 0), sha256(providerProjection.renderSemantic(empty)))

  // An out-of-range cutoff never throws: List.truncate keeps everything (the
  // selector's min() means such a cutoff cannot reach the proof in production).
  assert.equal(projectionAlgebra.cutoffDigest(sha256, snapshot, 99), sha256(full))
})

test('WHAT[PREFIX-STABILITY-009] CTX_011_step5_the_proof_reads_the_SNAPSHOT_not_a_stale_closure', () => {
  // The digest must be recomputed from X's CURRENT projection each attempt —
  // a closure captured once would re-prove yesterday's numbering (COMPANION-011).
  const before = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'old' }] },
  ])
  const after = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'old' }] },
    { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'new' }] },
  ])

  // Same cutoff (2) over a 1-message and a 2-message projection: the grown one
  // keeps its second message, so the proof cannot be the same (COMPANION-011).
  // A cutoff of 1 would truncate both to the same single message — the selector's
  // min() means such a cutoff would only be asked when the numbering is identical.
  assert.notEqual(
    projectionAlgebra.cutoffDigest(sha256, before, 2),
    projectionAlgebra.cutoffDigest(sha256, after, 2),
    'the same cutoff over a grown projection must not produce the same proof',
  )
})
