// Split from tests/unit/journal/envelope.test.mjs (cutover Wave 2a); owner: behavior-diagnosis
//
// ENFORCER-072 tip-v2 clean break at envelope decode: observation-commit
// envelopes from the ScoreVectorRef era (or without a TipRuleId) are refused
// when boot reads the NDJSON line, and the legacy BlogEntryCommitted tag
// dual-decodes to BlogObservationCommitted. The codec-level (serializeFact /
// deserializeFact) half lives in fact-codec-tip-v2-clean-break.test.mjs.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentFactCaseOf,
  blobDigest,
  blobRef,
  bloggerRequestId,
  envelope,
  fact,
  frameEpochId,
  journal,
  payloadOf,
  prefixEpochId,
  providerRun,
  sessionId,
  stream,
} from '../../verification-system/tests/support/domain.mjs'

const SESSION = sessionId('ses_a')

test('WHAT[BD-008] ENFORCER_072_observation_commit_without_TipRuleId_is_refused_at_envelope_decode', () => {
  // Boot reads envelopes, not FactCodec alone. Without this check Thoth fails
  // opaquely, Boot truncates mid-stream, and fold invents "already has open request".
  for (const tag of ['BlogEntryCommitted', 'BlogObservationCommitted']) {
    const legacy = JSON.stringify({
      RuntimeId: ['RuntimeId', 'rt1'],
      LocalSeq: ['LocalSeq', '1'],
      ObservedAt: '2026-01-01T00:00:00.000+00:00',
      EventId: ['EventId', 'e1'],
      Stream: ['Session', ['SessionId', 'ses_a']],
      Fact: [
        'Agent',
        [
          tag,
          {
            SessionId: ['SessionId', 'ses_a'],
            BloggerSessionId: ['SessionId', 'ses_b'],
            // no TipRuleId, optional ScoreVectorRef-era shape
            TextDigest: ['BlobDigest', 'sha'],
            TextRef: ['BlobRef', 'blobs/sha'],
          },
        ],
      ],
    })
    assert.equal(journal.containsLegacyScoreVectorEntry(legacy), true, tag)
    const decoded = journal.deserialize(legacy)
    assert.equal(decoded.ok, false, tag)
    assert.equal(decoded.error, journal.tipV2CleanBreakMessage)
  }
})

test('WHAT[BD-008] ENFORCER_072_ScoreVectorRef_era_entry_is_refused_at_envelope_decode', () => {
  for (const tag of ['BlogEntryCommitted', 'BlogObservationCommitted']) {
    const legacy = JSON.stringify({
      RuntimeId: ['RuntimeId', 'rt1'],
      LocalSeq: ['LocalSeq', '1'],
      ObservedAt: '2026-01-01T00:00:00.000+00:00',
      EventId: ['EventId', 'e1'],
      Stream: ['Session', ['SessionId', 'ses_a']],
      Fact: [
        'Agent',
        [
          tag,
          {
            SessionId: ['SessionId', 'ses_a'],
            TipRuleId: 'ignored-if-score-vector-present',
            ScoreVectorRef: ['BlobRef', 'blobs/old'],
          },
        ],
      ],
    })
    assert.equal(journal.containsLegacyScoreVectorEntry(legacy), true, tag)
    const decoded = journal.deserialize(legacy)
    assert.equal(decoded.ok, false, tag)
    assert.match(String(decoded.error), /TipRuleId|ScoreVectorRef|tip v2/)
  }
})

test('WHAT[BD-012] PERSIST_005_envelope_dual_decodes_legacy_observation_tags', () => {
  const observation = fact('BlogObservationCommitted', {
    SessionId: SESSION,
    BloggerSessionId: sessionId('ses_b'),
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
  const value = envelope({ seq: 4, stream: stream.session(SESSION), run: 'run-obs', fact: observation })
  const line = journal.serialize(value)
  assert.equal(line.includes('"BlogObservationCommitted"'), true)
  assert.equal(line.includes('"BlogEntryCommitted"'), false)

  const legacy = line.replaceAll('"BlogObservationCommitted"', '"BlogEntryCommitted"')
  const decoded = journal.deserialize(legacy)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(agentFactCaseOf(payloadOf(decoded.value.Fact)), 'BlogObservationCommitted')
  assert.equal(journal.serialize(decoded.value), line)
})
