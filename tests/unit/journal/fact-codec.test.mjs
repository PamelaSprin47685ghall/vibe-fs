// tests/unit/journal/fact-codec.test.mjs — VERIFY-009 coverage: PERSIST-005 fact codec.
//
// Round trips for DateTimeOffset-bearing facts (pinToUtc on both write and read),
// the pre-0.5.0 refuse markers, the tip-v2 clean break, and the two textual
// migrations (HandleCompleted.CompletionRef, HandleLinked.Ownership).

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  agentFactCaseOf,
  blobDigest,
  blobRef,
  caseOf,
  completionKind,
  fact,
  handleAbandonReason,
  handleId,
  handleOwnership,
  journal,
  offsetAt,
  payloadOf,
  roles,
  runtimeStartedFact,
  sessionId,
  utcOffset,
} from '../support/domain.mjs'

test('PERSIST_005_pre050_marker_refuses_with_migration_message', () => {
  for (const marker of [
    '"FailuresOnCurrentSide"',
    '"IsDead"',
    '"BaseModelID"',
    '"AgentLinked"',
    '"OrchestratorPublished"',
    '"EnforcementCycleCommitted"',
    '"DurableEffectRequested"',
    '"DurableEffectAccepted"',
  ]) {
    assert.equal(journal.containsLegacyFallbackFields(`{"RuntimeFact":${marker}}`), true, marker)
  }

  const decoded = journal.deserializeFact('{"AgentLinked":{"SessionId":"s"}}')
  assert.equal(decoded.ok, false)
  assert.equal(decoded.error, journal.pre050MigrationMessage)
})

test('PERSIST_005_modern_json_has_no_legacy_markers', () => {
  assert.equal(journal.containsLegacyFallbackFields('{"RuntimeStarted":{"Runtime":"rt"}}'), false)
})

test('ENFORCER_072_score_vector_entry_refuses_with_tip_v2_message', () => {
  const legacy = '{"BlogEntryCommitted":{"ScoreVectorRef":"sv-1","TipRuleId":"rule"}}'
  assert.equal(journal.containsLegacyScoreVectorEntry(legacy), true)

  const decoded = journal.deserializeFact(legacy)
  assert.equal(decoded.ok, false)
  assert.equal(decoded.error, journal.tipV2CleanBreakMessage)
})

test('ENFORCER_072_entry_without_tip_rule_id_is_legacy', () => {
  const noTipRule = '{"BlogEntryCommitted":{"Entry":"e"}}'
  assert.equal(journal.containsLegacyScoreVectorEntry(noTipRule), true)
})

test('ENFORCER_072_modern_tip_v2_entry_passes_the_marker_check', () => {
  const modern = '{"BlogEntryCommitted":{"TipRuleId":"rule-x","Entry":"e"}}'
  assert.equal(journal.containsLegacyScoreVectorEntry(modern), false)

  // The marker check only fires on BlogEntryCommitted lines.
  assert.equal(journal.containsLegacyScoreVectorEntry('{"Other":{"TipRuleId":null}}'), false)
})

test('PERSIST_001_runtime_started_pins_offset_on_serialize_and_deserialize', () => {
  const started = runtimeStartedFact({ startedAt: '2026-01-01T00:00:00Z' })
  const line = journal.serializeFact(started)
  assert.match(line, /StartedAt[^Z]*Z|StartedAt.*\+00:00/, `offset must be pinned to UTC: ${line}`)

  // Decode a line whose embedded offset is NOT zero but denotes the SAME
  // instant (08:00 at +08:00 ≡ 00:00Z): the decoded fact must carry +00:00 so
  // a re-serialize is byte-stable across reader timezones.
  const shifted = line.replace('T00:00:00.000+00:00', 'T08:00:00.000+08:00')
  const decoded = journal.deserializeFact(shifted)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(journal.serializeFact(decoded.value), line)
})

test('PERSIST_001_handle_abandoned_pins_abandoned_at_offset', () => {
  const value = fact('HandleAbandoned', {
    ParentSessionId: sessionId('ses_pin'),
    Handle: handleId.agent('h-pin'),
    Reason: handleAbandonReason.parentCancelled(),
    AbandonedAt: offsetAt('2026-01-01T00:00:00Z', 480),
  })
  const line = journal.serializeFact(value)
  const decoded = journal.deserializeFact(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(journal.serializeFact(decoded.value), line, 'offset must normalise to +00:00')
})

test('MIGRATION_handle_completed_without_completion_ref_gets_nulls_injected', () => {
  // A 0.5.1 line: no CompletionRef / CompletionDigest keys.
  const modern = fact('HandleCompleted', {
    ParentSessionId: sessionId('ses_hc'),
    Handle: handleId.agent('h-hc'),
    Kind: completionKind.of('Terminal'),
    CompletionRef: undefined,
    CompletionDigest: undefined,
  })
  const line = journal.serializeFact(modern)

  // Strip the two keys to simulate the pre-0.5.2 shape, then decode.
  const stripped = line
    .replace(/,"CompletionRef":null/g, '')
    .replace(/,"CompletionDigest":null/g, '')
    .replace(/"CompletionRef":null,/g, '')
    .replace(/"CompletionDigest":null,/g, '')
  assert.equal(stripped.includes('CompletionRef'), false, 'fixture must lack CompletionRef')
  assert.equal(stripped.includes('CompletionDigest'), false, 'fixture must lack CompletionDigest')

  const decoded = journal.deserializeFact(stripped)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(agentFactCaseOf(payloadOf(decoded.value)), 'HandleCompleted')
})

test('MIGRATION_handle_completed_with_completion_ref_passes_through', () => {
  const withRef = fact('HandleCompleted', {
    ParentSessionId: sessionId('ses_hc2'),
    Handle: handleId.agent('h-hc2'),
    Kind: completionKind.of('Terminal'),
    CompletionRef: blobRef('blobs/ref-1'),
    CompletionDigest: blobDigest('digest-1'),
  })
  const line = journal.serializeFact(withRef)
  assert.equal(line.includes('"CompletionRef"'), true)

  const decoded = journal.deserializeFact(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(journal.serializeFact(decoded.value), line)
})

test('MIGRATION_handle_linked_without_ownership_defaults_to_durable_parent', () => {
  const modern = fact('HandleLinked', {
    ParentSessionId: sessionId('ses_hl'),
    ChildSessionId: sessionId('ses_hl_child'),
    Handle: handleId.agent('h-hl'),
    TargetAgent: 'fast-reviewer',
    CanonicalRole: roles.of('Reviewer'),
    Ownership: handleOwnership.durableParentHandle(),
  })
  const line = journal.serializeFact(modern)
  assert.equal(line.includes('"Ownership"'), true)

  const stripped = line.replace(/"Ownership":"DurableParentHandle",?/, '')
  assert.equal(stripped.includes('"Ownership"'), false, 'fixture must lack Ownership')

  const decoded = journal.deserializeFact(stripped)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  // The injected default must be the pre-change meaning: every legacy handle
  // was parent-visible (GLORY-002 / SURFACE-006).
  assert.equal(journal.serializeFact(decoded.value), line)
})

test('PERSIST_005_unparseable_json_is_a_decode_error_not_a_throw', () => {
  const decoded = journal.deserializeFact('{not json')
  assert.equal(decoded.ok, false)
  assert.equal(typeof decoded.error, 'string')
})

test('PERSIST_005_unknown_case_is_a_decode_error', () => {
  const decoded = journal.deserializeFact('{"NoSuchFactCase":{"X":1}}')
  assert.equal(decoded.ok, false)
})
