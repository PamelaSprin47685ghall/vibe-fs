// Split from tests/unit/journal/blog-entry-committed.test.mjs (cutover Wave 2a); owner: behavior-diagnosis
//
// ENFORCER-045 atomic fold: one BlogObservationCommitted fact updates Blog AND
// Enforcement atomically. No separate EnforcementCycleCommitted exists; the
// fold refuses its name as a pre-0.5.0 legacy line (PERSIST-005). This half
// pins the enforcement-projection side (BD-012/BD-013): the atomic first
// application, the ByProviderRun receipt, the duplicate-run / stale-cursor
// rejections, and the retired EnforcementCycleCommitted fact name.
// The Blog coverage-law half (strict advance / zero advance / squash) lives in
// context-compression/tests/blog-entry-committed.test.mjs.

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

// ── ENFORCER-045: one fact, two projections ─────────────────────────────────

test('ENFORCER_045_cycle_commit_appends_frame_and_advances_coverage', () => {
  const s = foldOk([
    entryWithEnforcement({
      from: 0,
      to: 1,
      cutoffFrom: 0,
      cutoffTo: 1,
      toolCalls: ['call-1'],
      tipRuleId: 'enforcement-a01',
      fieldNameAtCommit: 'primitive-obsession',
      evidenceRef: 'blob-evidence',
    }),
  ])

  assert.equal(blog.frameCount(s.Blog), 1, 'frame count +1')
  assert.equal(s.Blog.Coverage.IngestedThroughSequence, 1n, 'coverage advanced')
  assert.equal(s.Blog.Coverage.CoverableTurnCutoffExclusive, 1)
})

test('ENFORCER_045_enforcement_half_queryable_by_provider_run', () => {
  const s = foldOk([
    entryWithEnforcement({
      from: 0,
      to: 1,
      cutoffFrom: 0,
      cutoffTo: 1,
      run: 'msg_run1',
      toolCalls: ['call-a', 'call-b'],
      tipRuleId: 'enforcement-a01',
      fieldNameAtCommit: 'primitive-obsession',
      evidenceRef: 'blob-ev1',
    }),
  ])

  assert.ok(s.Enforcement, 'enforcement projection exists')
  assert.equal(s.Enforcement.ByProviderRun.size, 1, 'one enforcement record')
  const [record] = [...s.Enforcement.ByProviderRun.values()]
  assert.ok(record.ToolCallIds, 'tool call ids present')
  assert.equal(record.TipRuleId, 'enforcement-a01')
  assert.equal(record.FieldNameAtCommit, 'primitive-obsession')
  assert.ok(record.CycleEvidenceRef, 'evidence ref present')
  assert.equal(record.CycleScoreRef, undefined, 'ScoreVectorRef path deleted')
})

test('ENFORCER_045_duplicate_provider_run_rejected_by_fold', () => {
  const error = foldErr([
    entryWithEnforcement({ from: 0, to: 1, cutoffFrom: 0, cutoffTo: 1, run: 'msg_dup' }),
    entryWithEnforcement({ from: 1, to: 2, cutoffFrom: 1, cutoffTo: 2, run: 'msg_dup' }),
  ])
  assert.ok(error, 'duplicate ProviderRun must be rejected')
})

test('ENFORCER_045_stale_previous_ingest_cursor_rejected', () => {
  const error = foldErr([
    entryWithEnforcement({ from: 0, to: 2, cutoffFrom: 0, cutoffTo: 1, n: 1, run: 'msg_a' }),
    // Previous=0 but projection is at 2 — a correct writer cannot produce this.
    entryWithEnforcement({ from: 0, to: 3, cutoffFrom: 0, cutoffTo: 2, n: 2, run: 'msg_b' }),
  ])
  assert.ok(error, 'stale previous ingest cursor must be rejected')
})

test('ENFORCER_045_no_enforcement_cycle_committed_fact_exists', () => {
  // The AgentFact union no longer contains the case; building it must fail.
  assert.throws(
    () =>
      fact('EnforcementCycleCommitted', {
        MainSessionId: session,
        BloggerSessionId: blogger,
        ProviderRun: providerRun('msg_old'),
        ToolCallIds: [],
        TextRef: blobRef('blob-old'),
        TextDigest: blobDigest('sha-old'),
        TipRuleId: 'enforcement-old',
        FieldNameAtCommit: 'primitive-obsession',
        EvidenceRef: undefined,
        ObservedPrefixEpochId: prefixEpochId(0),
      }),
    /no AgentFact family has case 'EnforcementCycleCommitted'/,
    'EnforcementCycleCommitted is not a valid AgentFact case',
  )
})
