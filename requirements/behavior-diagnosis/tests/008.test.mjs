// BD-008: Elimination of numeric scores and bridge fields
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const catalogRules = enforcer.rules()
const fields = enforcer.fieldNames()
const firstField = () => fields[0]

test('WHAT[BD-008] ENFORCER_170_no_bridge_fields_on_rule', () => {
  for (const rule of catalogRules) {
    assert.equal(rule.scoreWhen, undefined, `rule ${rule.ruleId} still has ScoreWhen`)
    assert.equal(rule.nudge, undefined, `rule ${rule.ruleId} still has Nudge`)
    assert.equal(rule.family, undefined, `rule ${rule.ruleId} still has Family`)
    assert.equal(rule.catalogOrdinal, undefined, `rule ${rule.ruleId} still has CatalogOrdinal`)
  }
})

test('WHAT[BD-008] ENFORCER_024_extra_numeric_properties_are_ignored', () => {
  const field = firstField()
  const result = enforcer.decodeCall({
    text: 'entry',
    tip: field,
    'primitive-obsession': 7,
    some_other_number: 3,
  })
  assert.equal(result.ok, true)
  assert.equal(result.value.tip.fieldName, field)
  assert.equal(result.value.evidence, null)
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

test('WHAT[BD-008] ENFORCER_TIP_03_04_facade_surface_has_tip_not_numeric_scores', () => {
  const sample = enforcer.decodeCall({ text: 'x', tip: fields[0] })
  assert.equal(sample.ok, true)
  assert.equal(typeof sample.value.tip.ruleId, 'string')
  assert.equal(sample.value.tip.fieldName in Object.fromEntries(fields.map((f) => [f, true])), true)
  assert.equal(sample.value.Scores, undefined)
})
