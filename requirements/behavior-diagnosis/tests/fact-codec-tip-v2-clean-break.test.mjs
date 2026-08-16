// Split from tests/unit/journal/fact-codec.test.mjs (cutover Wave 2a); owner: behavior-diagnosis
//
// ENFORCER-072 tip-v2 clean break at the fact codec: ScoreVectorRef-era
// observation commits and entries without a TipRuleId are refused, modern tip-v2
// entries pass the marker check, and the legacy observation tags
// (BlogEntryCommitted / BlogSquashCommitted) encode/dual-decode to the new
// observation names (BD-012: BlogObservationCommitted is the only atomic fact).

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  agentFactCaseOf,
  blobDigest,
  blobRef,
  bloggerRequestId,
  fact,
  frameEpochId,
  journal,
  payloadOf,
  prefixEpochId,
  providerRun,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'

test('WHAT[BD-008] ENFORCER_072_score_vector_entry_refuses_with_tip_v2_message', () => {
  for (const tag of ['BlogEntryCommitted', 'BlogObservationCommitted']) {
    const legacy = `{"${tag}":{"ScoreVectorRef":"sv-1","TipRuleId":"rule"}}`
    assert.equal(journal.containsLegacyScoreVectorEntry(legacy), true, tag)

    const decoded = journal.deserializeFact(legacy)
    assert.equal(decoded.ok, false, tag)
    assert.equal(decoded.error, journal.tipV2CleanBreakMessage)
  }
})

test('WHAT[BD-008] ENFORCER_072_entry_without_tip_rule_id_is_legacy', () => {
  for (const tag of ['BlogEntryCommitted', 'BlogObservationCommitted']) {
    const noTipRule = `{"${tag}":{"Entry":"e"}}`
    assert.equal(journal.containsLegacyScoreVectorEntry(noTipRule), true, tag)
  }
})

test('WHAT[BD-008] ENFORCER_072_modern_tip_v2_entry_passes_the_marker_check', () => {
  for (const tag of ['BlogEntryCommitted', 'BlogObservationCommitted']) {
    const modern = `{"${tag}":{"TipRuleId":"rule-x","Entry":"e"}}`
    assert.equal(journal.containsLegacyScoreVectorEntry(modern), false, tag)
  }

  // The marker check only fires on observation-commit lines (new or legacy tag).
  assert.equal(journal.containsLegacyScoreVectorEntry('{"Other":{"TipRuleId":null}}'), false)
})

const observationCommitted = () =>
  fact('BlogObservationCommitted', {
    SessionId: sessionId('ses_obs'),
    BloggerSessionId: sessionId('ses_blogger'),
    RequestId: bloggerRequestId('req-obs'),
    FrameEpochId: frameEpochId(0),
    PreviousIngestedThroughSequence: 0n,
    NextIngestedThroughSequence: 1n,
    PreviousCoverableTurnCutoffExclusive: 0,
    NextCoverableTurnCutoffExclusive: 1,
    NextCoveredPrefixDigest: 'd-1',
    TextRef: blobRef('blob-obs'),
    TextDigest: blobDigest('sha-obs'),
    ProviderRun: providerRun('run-obs'),
    ToolCallIds: [],
    TipRuleId: 'rule-obs',
    FieldNameAtCommit: 'field-obs',
    EvidenceRef: undefined,
    ObservedPrefixEpochId: prefixEpochId(0),
  })

const observationsSquashed = () =>
  fact('BlogObservationsSquashed', {
    SessionId: sessionId('ses_obs'),
    BloggerSessionId: sessionId('ses_blogger'),
    RequestId: bloggerRequestId('req-squash'),
    PreviousFrameEpochId: frameEpochId(0),
    NextFrameEpochId: frameEpochId(1),
    CoveredFrameCount: 1,
    TextRef: blobRef('blob-squash'),
    TextDigest: blobDigest('sha-squash'),
    ProviderRun: providerRun('run-squash'),
  })

test('WHAT[BD-012] PERSIST_005_observation_encode_writes_new_tags_only', () => {
  const committed = journal.serializeFact(observationCommitted())
  assert.equal(committed.includes('"BlogObservationCommitted"'), true)
  assert.equal(committed.includes('"BlogEntryCommitted"'), false)

  const squashed = journal.serializeFact(observationsSquashed())
  assert.equal(squashed.includes('"BlogObservationsSquashed"'), true)
  assert.equal(squashed.includes('"BlogSquashCommitted"'), false)
})

test('WHAT[BD-012] PERSIST_005_legacy_observation_tags_dual_decode_to_new_names', () => {
  const committed = journal.serializeFact(observationCommitted())
  const legacyCommitted = committed.replaceAll('"BlogObservationCommitted"', '"BlogEntryCommitted"')
  assert.equal(legacyCommitted.includes('"BlogEntryCommitted"'), true)

  const decodedCommitted = journal.deserializeFact(legacyCommitted)
  assert.equal(decodedCommitted.ok, true, decodedCommitted.ok ? '' : decodedCommitted.error)
  assert.equal(agentFactCaseOf(payloadOf(decodedCommitted.value)), 'BlogObservationCommitted')
  assert.equal(journal.serializeFact(decodedCommitted.value), committed)

  const squashed = journal.serializeFact(observationsSquashed())
  const legacySquashed = squashed.replaceAll('"BlogObservationsSquashed"', '"BlogSquashCommitted"')
  const decodedSquashed = journal.deserializeFact(legacySquashed)
  assert.equal(decodedSquashed.ok, true, decodedSquashed.ok ? '' : decodedSquashed.error)
  assert.equal(agentFactCaseOf(payloadOf(decodedSquashed.value)), 'BlogObservationsSquashed')
  assert.equal(journal.serializeFact(decodedSquashed.value), squashed)
})
