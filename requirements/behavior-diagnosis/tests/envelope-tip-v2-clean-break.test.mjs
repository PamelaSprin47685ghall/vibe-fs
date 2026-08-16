// Tip-v2 clean break at the durable Journal envelope boundary.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const observationFact = () => ({
  case: 'BlogObservationCommitted',
  sessionId: 'ses_a',
  bloggerSessionId: 'ses_b',
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
})

const envelope = (fact) => ({
  runtimeId: 'rt1',
  localSeq: 4,
  observedAt: '2026-01-01T00:00:00.000+00:00',
  eventId: 'e1',
  stream: { kind: 'Session', id: 'ses_a' },
  providerRun: 'run-obs',
  fact,
})

test('WHAT[BD-008] ENFORCER_072_observation_commit_without_TipRuleId_is_refused_at_envelope_decode', () => {
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
            TextDigest: ['BlobDigest', 'sha'],
            TextRef: ['BlobRef', 'blobs/sha'],
          },
        ],
      ],
    })
    assert.equal(blog.containsLegacyScoreVectorEntry(legacy), true, tag)
    const decoded = blog.deserializeEnvelope(legacy)
    assert.equal(decoded.ok, false, tag)
    assert.equal(decoded.error, blog.tipV2CleanBreakMessage)
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
    assert.equal(blog.containsLegacyScoreVectorEntry(legacy), true, tag)
    const decoded = blog.deserializeEnvelope(legacy)
    assert.equal(decoded.ok, false, tag)
    assert.match(String(decoded.error), /TipRuleId|ScoreVectorRef|tip v2/)
  }
})

test('WHAT[BD-012] PERSIST_005_envelope_dual_decodes_legacy_observation_tags', () => {
  const line = blog.serializeEnvelope(envelope(observationFact()))
  assert.equal(line.includes('BlogObservationCommitted'), true)
  assert.equal(line.includes('BlogEntryCommitted'), false)

  const legacy = line.replaceAll('BlogObservationCommitted', 'BlogEntryCommitted')
  const decoded = blog.deserializeEnvelope(legacy)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.value.case, 'BlogObservationCommitted')
  assert.equal(decoded.value.line, line)
})
