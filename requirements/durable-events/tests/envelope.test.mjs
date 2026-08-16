// Journal envelope laws use the public codec boundary. The typed Envelope and
// Fact values never cross into this semantic test zone.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as journalCodec from '../../../dist/Persistence/Journal/CodecSurface.js'
import * as factCodec from '../../../dist/Persistence/Journal/FactCodecSurface.js'

const SESSION = 'ses_a'
const CLOSED = {
  family: 'Companion',
  case: 'CompanionBloggerClosed',
  payload: { SessionId: SESSION },
}

const env = (overrides = {}) => ({
  runtime: 'rt_a',
  seq: 1,
  observedAt: '2026-01-02T03:04:05Z',
  id: 'a'.repeat(32),
  stream: { kind: 'Session', id: SESSION },
  providerRun: null,
  fact: CLOSED,
  ...overrides,
})

const readEnvelope = (value) => ({
  runtime: value.runtime,
  seq: Number(value.seq),
  event: value.id,
  stream: value.stream,
  providerRun: value.providerRun,
  fact: value.fact.case,
})

const mustOk = (result, label = 'result') => {
  assert.equal(result.ok, true, `${label} should be Ok: ${JSON.stringify(result.error)}`)
  return result.value
}

test('WHAT[DURABLE-EVENTS-002] PERSIST_001_an_envelope_serializes_to_exactly_one_line', () => {
  const line = journalCodec.serialize(env({ seq: 7 }))
  assert.equal(line.includes('\n'), false)
  assert.equal(line.includes('\r'), false)

  assert.deepEqual(Object.keys(JSON.parse(line)).sort(), [
    'EventId',
    'Fact',
    'LocalSeq',
    'ObservedAt',
    'RuntimeId',
    'Stream',
  ])
})

test('WHAT[DURABLE-EVENTS-003] PERSIST_001_serialization_is_deterministic_for_one_envelope', () => {
  const value = env({ seq: 3, observedAt: '2026-02-03T04:05:06Z' })
  assert.equal(journalCodec.serialize(value), journalCodec.serialize(value))
  assert.equal(journalCodec.serialize(value), journalCodec.serialize(env({ seq: 3, observedAt: '2026-02-03T04:05:06Z' })))
})

test('WHAT[DURABLE-EVENTS-003] PERSIST_001_an_absent_provider_run_is_omitted_rather_than_written_null', () => {
  const withoutRun = journalCodec.serialize(env({ seq: 1 }))
  assert.equal(withoutRun.includes('ProviderRun'), false)

  const withRun = journalCodec.serialize(env({ seq: 1, providerRun: 'run_9' }))
  assert.equal(withRun.includes('ProviderRun'), true)
  assert.equal(withRun.includes('run_9'), true)
})

test('WHAT[DURABLE-EVENTS-003] PERSIST_001_an_envelope_survives_a_round_trip_unchanged', () => {
  const original = env({ seq: 4, observedAt: '2026-03-04T05:06:07Z', providerRun: 'run_1' })
  const line = journalCodec.serialize(original)
  const decoded = journalCodec.deserialize(line)

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.deepEqual(readEnvelope(decoded.value), readEnvelope(original))
  assert.equal(journalCodec.serialize(decoded.value), line)
})

test('WHAT[DURABLE-EVENTS-003] PERSIST_001_serialized_bytes_do_not_depend_on_the_writers_utc_offset', () => {
  const instant = '2026-03-04T05:06:07Z'
  const atUtc = env({ seq: 1, observedAt: instant })
  const shanghai = env({ seq: 1, observedAt: '2026-03-04T13:06:07+08:00' })
  const newYork = env({ seq: 1, observedAt: '2026-03-04T00:06:07-05:00' })

  assert.equal(new Date(shanghai.observedAt).getTime(), new Date(instant).getTime())
  assert.equal(new Date(newYork.observedAt).getTime(), new Date(instant).getTime())

  const line = journalCodec.serialize(atUtc)
  assert.equal(journalCodec.serialize(shanghai), line)
  assert.equal(journalCodec.serialize(newYork), line)
  assert.equal(JSON.parse(line).ObservedAt, '2026-03-04T05:06:07.000+00:00')
})

test('WHAT[DURABLE-EVENTS-014] PERSIST_001_ordering_is_by_local_seq_inside_a_runtime_and_by_time_across', () => {
  const a1 = env({ runtime: 'rt_a', seq: 1, observedAt: '2026-01-01T00:00:09Z' })
  const a2 = env({ runtime: 'rt_a', seq: 2, observedAt: '2026-01-01T00:00:00Z' })
  assert.equal(journalCodec.compareSortKey(a1, a2) < 0, true)
  assert.equal(journalCodec.compareSortKey(a2, a1) > 0, true)
  assert.equal(journalCodec.compareSortKey(a1, a1), 0)

  const b1 = env({ runtime: 'rt_b', seq: 1, observedAt: '2026-01-01T00:00:05Z' })
  assert.equal(journalCodec.compareSortKey(a1, b1) > 0, true)
  assert.equal(journalCodec.compareSortKey(b1, a1) < 0, true)
})

test('WHAT[DURABLE-EVENTS-014] PERSIST_001_same_instant_across_runtimes_breaks_the_tie_by_runtime_id', () => {
  const at = '2026-01-01T00:00:00Z'
  const a = env({ runtime: 'rt_a', seq: 1, observedAt: at })
  const b = env({ runtime: 'rt_b', seq: 1, observedAt: at })
  assert.equal(journalCodec.compareSortKey(a, b) < 0, true)
  assert.equal(journalCodec.compareSortKey(b, a) > 0, true)
})

test('WHAT[DURABLE-EVENTS-014] PERSIST_001_k_way_merge_is_a_total_order_regardless_of_input_order', () => {
  const at = (s) => `2026-01-01T00:00:0${s}Z`
  const streamA = [
    env({ runtime: 'rt_a', seq: 1, observedAt: at(1) }),
    env({ runtime: 'rt_a', seq: 2, observedAt: at(4) }),
  ]
  const streamB = [
    env({ runtime: 'rt_b', seq: 1, observedAt: at(2) }),
    env({ runtime: 'rt_b', seq: 2, observedAt: at(3) }),
  ]
  const label = (merged) => merged.map((value) => `${value.runtime}#${value.seq}`)
  const expected = ['rt_a#1', 'rt_b#1', 'rt_b#2', 'rt_a#2']

  assert.deepEqual(label(journalCodec.kWayMerge([streamA, streamB])), expected)
  assert.deepEqual(label(journalCodec.kWayMerge([streamB, streamA])), expected)
  assert.deepEqual(label(journalCodec.kWayMerge([[], streamA, [], streamB])), expected)
})

test('WHAT[DURABLE-EVENTS-009] PERSIST_005_legacy_fallback_counters_and_model_ids_are_fatal', () => {
  const markers = [
    'FailuresOnCurrentSide',
    'IsDead',
    'TotalFailures',
    'BaseModelID',
    'BaseProviderID',
    'EffectiveModelID',
    'EffectiveProviderID',
  ]

  for (const marker of markers) {
    const line = JSON.stringify({ LocalSeq: 1, [marker]: 3 })
    assert.equal(factCodec.containsLegacyFallbackFields(line), true, `${marker} must be recognised as pre-0.5.0`)
    const decoded = factCodec.decode(line)
    assert.equal(decoded.ok, false)
    assert.equal(decoded.error, factCodec.pre050MigrationMessage)
  }
})

test('WHAT[DURABLE-EVENTS-009] PERSIST_005_replaced_fact_names_produce_the_migration_message_not_a_codec_error', () => {
  const retired = [
    'PluginPromptAccepted',
    'HumanPromptAccepted',
    'GuardPromptAccepted',
    'InteractionRepairClaimed',
    'ReviewConfirmedIdle',
    'AgentLinked',
    'AgentForked',
    'AgentUnlinked',
    'OrchestratorCandidateRegistered',
    'OrchestratorRebased',
    'OrchestratorPublishClaimed',
    'DurableEffectRequested',
    'DurableEffectAccepted',
  ]

  for (const name of retired) {
    const line = JSON.stringify({ Fact: ['Agent', [name, {}]] })
    assert.equal(factCodec.decode(line).error, factCodec.pre050MigrationMessage, `${name} must be diagnosed by name`)
  }
})

test('WHAT[DURABLE-EVENTS-009] PERSIST_005_the_migration_message_tells_the_operator_what_to_do', () => {
  assert.equal(
    factCodec.pre050MigrationMessage,
    'Wanxiangshu 0.5.0 does not support pre-0.5.0 runtime journals.\n' +
      'Archive or remove the old Wanxiangshu runtime journal before starting.',
  )
})

test('WHAT[DURABLE-EVENTS-009] PERSIST_005_a_current_fact_is_not_mistaken_for_a_legacy_one', () => {
  const line = journalCodec.serialize(env({ seq: 1 }))
  assert.equal(factCodec.containsLegacyFallbackFields(line), false)

  for (const current of [
    'HandleLinked',
    'HandleCompleted',
    'HandleAbandoned',
    'HandleRetired',
    'HostTurnObserved',
    'CandidateReady',
    'PublishClaimed',
  ]) {
    assert.equal(
      factCodec.containsLegacyFallbackFields(JSON.stringify({ Fact: ['Agent', [current, {}]] })),
      false,
      `${current} is a current fact and must not trip the pre-0.5.0 check`,
    )
  }
})

test('WHAT[DURABLE-EVENTS-007] PERSIST_005_malformed_json_is_an_error_value_not_an_exception', () => {
  for (const bad of ['', '{', '{"unclosed": ', 'null', '[]', 'not json at all']) {
    const decoded = journalCodec.deserialize(bad)
    assert.equal(decoded.ok, false, `${JSON.stringify(bad)} must not decode`)
    assert.equal(typeof decoded.error, 'string')
  }
})

test('WHAT[DURABLE-EVENTS-003] PERSIST_001_parents_and_payload_refs_are_canonicalized_at_the_codec_boundary', () => {
  const encoded = journalCodec.encode(
    ['b'.repeat(32), 'a'.repeat(32), 'b'.repeat(32)],
    ['ref-z', 'ref-a', 'ref-z'],
    env(),
  )
  assert.deepEqual(encoded.parents, ['a'.repeat(32), 'b'.repeat(32)])
  assert.deepEqual(encoded.payloadRefs, ['ref-a', 'ref-z'])
})
