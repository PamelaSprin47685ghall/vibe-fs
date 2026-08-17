// Tip-v2 clean break at the Blogger observation fact codec.
//
// The test speaks only the Blog owner surface. Fact payload identities are
// semantic strings at this boundary; FactCodec owns their typed conversion.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const observation = (overrides = {}) => ({
  case: 'BlogObservationCommitted',
  sessionId: 'ses_obs',
  bloggerSessionId: 'ses_blogger',
  requestId: 'req-obs',
  frameEpoch: 0,
  previousIngestedThroughSequence: 0,
  nextIngestedThroughSequence: 1,
  previousCoverableTurnCutoffExclusive: 0,
  nextCoverableTurnCutoffExclusive: 1,
  nextCoveredPrefixDigest: 'd-1',
  textRef: 'blobs/blob-obs',
  textDigest: 'sha-obs',
  run: 'run-obs',
  toolCallIds: [],
  tipRuleId: 'rule-obs',
  fieldNameAtCommit: 'field-obs',
  evidenceRef: undefined,
  observedPrefixEpoch: 0,
  ...overrides,
})

const squashed = () => ({
  case: 'BlogObservationsSquashed',
  sessionId: 'ses_obs',
  bloggerSessionId: 'ses_blogger',
  requestId: 'req-squash',
  previousFrameEpoch: 0,
  nextFrameEpoch: 1,
  coveredFrameCount: 1,
  textRef: 'blobs/blob-squash',
  textDigest: 'sha-squash',
  run: 'run-squash',
})

test('WHAT[BD-008] ENFORCER_072_score_vector_entry_refuses_with_tip_v2_message', () => {
  for (const tag of ['BlogEntryCommitted', 'BlogObservationCommitted']) {
    const legacy = `{"${tag}":{"ScoreVectorRef":"sv-1","TipRuleId":"rule"}}`
    assert.equal(blog.containsLegacyScoreVectorEntry(legacy), true, tag)

    const decoded = blog.deserializeFact(legacy)
    assert.equal(decoded.ok, false, tag)
    assert.equal(decoded.error, blog.tipV2CleanBreakMessage)
  }
})

test('WHAT[BD-008] ENFORCER_072_entry_without_tip_rule_id_is_legacy', () => {
  for (const tag of ['BlogEntryCommitted', 'BlogObservationCommitted']) {
    const noTipRule = `{"${tag}":{"Entry":"e"}}`
    assert.equal(blog.containsLegacyScoreVectorEntry(noTipRule), true, tag)
  }
})

test('WHAT[BD-008] ENFORCER_072_modern_tip_v2_entry_passes_the_marker_check', () => {
  for (const tag of ['BlogEntryCommitted', 'BlogObservationCommitted']) {
    const modern = `{"${tag}":{"TipRuleId":"rule-x","Entry":"e"}}`
    assert.equal(blog.containsLegacyScoreVectorEntry(modern), false, tag)
  }

  assert.equal(blog.containsLegacyScoreVectorEntry('{"Other":{"TipRuleId":null}}'), false)
})

test('WHAT[BD-012] PERSIST_005_observation_encode_writes_new_tags_only', () => {
  const committed = blog.serializeFact(observation())
  assert.equal(committed.includes('BlogObservationCommitted'), true)
  assert.equal(committed.includes('BlogEntryCommitted'), false)

  const encodedSquash = blog.serializeFact(squashed())
  assert.equal(encodedSquash.includes('BlogObservationsSquashed'), true)
  assert.equal(encodedSquash.includes('BlogSquashCommitted'), false)
})

test('WHAT[BD-012] PERSIST_005_fact_codec_rejects_pre_cutover_observation_tags', () => {
  const committed = blog.serializeFact(observation())
  const preCutoverCommitted = committed.replaceAll('BlogObservationCommitted', 'BlogEntryCommitted')
  assert.equal(preCutoverCommitted.includes('BlogEntryCommitted'), true)
  assert.equal(blog.deserializeFact(preCutoverCommitted).ok, false)

  const encodedSquash = blog.serializeFact(squashed())
  const preCutoverSquash = encodedSquash.replaceAll('BlogObservationsSquashed', 'BlogSquashCommitted')
  assert.equal(blog.deserializeFact(preCutoverSquash).ok, false)
})
