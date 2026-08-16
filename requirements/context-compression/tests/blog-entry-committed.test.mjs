// Split from tests/unit/journal/blog-entry-committed.test.mjs (cutover Wave 2a); owner: context-compression
//
// Blog coverage-law half of the ENFORCER-045 atomic fold: coverage strictly
// advances across commits, a zero-advance entry is refused (CTX-011), and a
// squash stays coverage-neutral (CTX-012, CONTEXT-COMPRESSION-011/014).
// The enforcement-projection half lives in
// behavior-diagnosis/tests/blog-entry-committed.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
  blogProjection as blog,
  envelope,
  fact,
  bloggerRequestId,
  fold,
  frameEpochId,
  prefixEpochId,
  providerRun,
  sessionId,
  stream,
  toolCallId,
} from '../../verification-system/tests/support/domain.mjs'

const session = sessionId('ses-main')
const blogger = sessionId('ses-blogger')

let seq = 0
const next = (factValue, run) =>
  envelope({ seq: (seq += 1), stream: stream.session(session), run, fact: factValue })

/** BlogObservationCommitted with full enforcement half (tip v2: TipRuleId, no ScoreVectorRef). */
const entryWithEnforcement = ({
  epoch = 0,
  from,
  to,
  cutoffFrom,
  cutoffTo,
  digest = `d-${cutoffTo}`,
  n = 1,
  run = `msg_e${n}`,
  toolCalls = [],
  tipRuleId = `enforcement-tip-${n}`,
  fieldNameAtCommit = `field-tip-${n}`,
  evidenceRef,
  prefixEpoch = 0,
}) =>
  next(
    fact('BlogObservationCommitted', {
      SessionId: session,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId(`req-e${n}`),
      FrameEpochId: frameEpochId(epoch),
      PreviousIngestedThroughSequence: BigInt(from),
      NextIngestedThroughSequence: BigInt(to),
      PreviousCoverableTurnCutoffExclusive: cutoffFrom,
      NextCoverableTurnCutoffExclusive: cutoffTo,
      NextCoveredPrefixDigest: digest,
      TextRef: blobRef(`blob-e${n}`),
      TextDigest: blobDigest(`sha-e${n}`),
      ProviderRun: providerRun(run),
      ToolCallIds: toolCalls.map((id) => toolCallId(id)),
      TipRuleId: tipRuleId,
      FieldNameAtCommit: fieldNameAtCommit,
      EvidenceRef: evidenceRef ? blobRef(evidenceRef) : undefined,
      ObservedPrefixEpochId: prefixEpochId(prefixEpoch),
    }),
    run,
  )

const foldOk = (envelopes) => {
  const result = fold.replay(envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return fold.session(result.value, 'ses-main')
}

const foldErr = (envelopes) => {
  const result = fold.replay(envelopes)
  assert.equal(result.ok, false, 'expected fold rejection')
  return result.error
}

// ── ENFORCER-045: coverage laws ─────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-015] ENFORCER_045_coverage_strictly_advances_across_commits', () => {
  const s = foldOk([
    entryWithEnforcement({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1, run: 'msg_r1' }),
    entryWithEnforcement({ from: 1, to: 3, cutoffFrom: 1, cutoffTo: 2, n: 2, run: 'msg_r2' }),
  ])

  assert.equal(blog.frameCount(s.Blog), 2)
  assert.equal(s.Blog.Coverage.IngestedThroughSequence, 3n)
  assert.equal(s.Blog.Coverage.CoverableTurnCutoffExclusive, 2)
})

test('WHAT[CONTEXT-COMPRESSION-015] ENFORCER_045_zero_advance_rejected', () => {
  const error = foldErr([
    entryWithEnforcement({ from: 1, to: 1, cutoffFrom: 0, cutoffTo: 0, n: 1, run: 'msg_zero' }),
  ])
  assert.ok(error, 'zero advance must be rejected (CTX-011)')
})

// ── squash stays coverage-neutral ───────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-011] CTX_012_squash_does_not_advance_coverage', () => {
  const squash = next(
    fact('BlogObservationsSquashed', {
      SessionId: session,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId('req-s1'),
      PreviousFrameEpochId: frameEpochId(0),
      NextFrameEpochId: frameEpochId(1),
      CoveredFrameCount: 1,
      TextRef: blobRef('blob-s1'),
      TextDigest: blobDigest('sha-s1'),
      ProviderRun: providerRun('msg_s1'),
    }),
    'msg_s1',
  )

  const s = foldOk([
    entryWithEnforcement({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, n: 1, run: 'msg_e1' }),
    squash,
  ])

  assert.equal(s.Blog.Coverage.IngestedThroughSequence, 1n, 'coverage unchanged by squash')
  assert.equal(s.Blog.Coverage.CoverableTurnCutoffExclusive, 1)
  assert.equal(blog.frameCount(s.Blog), 1, 'squash replaced frame, not appended')
  assert.deepEqual(blog.frameKinds(s.Blog), ['Squash'])
})
