// tests/unit/Context/prefix-epoch.test.mjs — COMPANION-009 / CTX-012 / HOST-006.
//
// Which X prefix generation is in force, and the two facts that may change it.
//
// One asymmetry runs through this file and is the point of it: a stale frame
// epoch is fatal (see blog-projection.test.mjs) while a stale PREFIX epoch is
// absorbed. That is not an inconsistency. CTX-012's crash recovery deliberately
// re-attempts both a rebase and a reanchor after a restart, so a replayed line
// carrying an epoch the projection has left behind means "already applied".
// Idempotency falls out of the epoch check instead of needing a second dedupe
// mechanism that could disagree with it.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

const candidate = ({ cutoff, prefixDigest = `prefix-${cutoff}`, digest = `frozen-${cutoff}`, seal = `seal-${cutoff}` }) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: digest,
    cutoff,
    prefixDigest,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

const rebase = (state, { previousEpoch, nextEpoch, cutoff, digest, seal, prefixDigest }) =>
  prefix.applyRebase(
    { previousEpoch, nextEpoch, candidate: candidate({ cutoff, digest, seal, prefixDigest }) },
    state,
  )

// ── the initial state and the retired state are one state ───────────────────

test('WHAT[PREFIX-STABILITY-005] CTX_012_a_replayed_rebase_is_reported_as_stale', () => {
  // CTX-012's recovery path re-attempts the commit after a restart: it cannot
  // know whether the append landed before the crash. The second attempt carries
  // the epoch it expected, which the projection has left.
  //
  // The projection says so; Fold turns that into an absorbed no-op. Keeping the
  // two layers distinct is what lets the frame projection treat the SAME shape of
  // refusal as fatal — see blog-projection.test.mjs.
  const once = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' }).value

  const replay = rebase(once, { previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' })
  assert.deepEqual(replay, { ok: false, error: 'StalePrefixEpoch' })

  assert.equal(prefix.epochOf(once), 1n)
  assert.deepEqual(once.snapshot, candidate({ cutoff: 4, seal: 'seal-P1' }))
})
