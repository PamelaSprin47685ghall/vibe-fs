// Journal line codec surface: serialization, round-trip, stream kinds, and unknown rejection.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as journalCodec from '../../../dist/Persistence/Journal/CodecSurface.js'

const SESSION = 'ses_meta'

const CLOSED = {
  family: 'Companion',
  case: 'CompanionBloggerClosed',
  payload: { SessionId: SESSION },
}

const envelope = (overrides = {}) => ({
  runtime: 'rt_meta',
  seq: 1,
  observedAt: '2026-03-04T05:06:07Z',
  id: 'a'.repeat(40),
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
  fact: value.fact,
})

test('WHAT[DURABLE-EVENTS-003] Journal_codec_serializes_one_envelope_to_one_UTC_line', () => {
  const line = journalCodec.serialize(envelope())
  assert.equal(line.includes('\n'), false)
  assert.equal(line.includes('\r'), false)
  assert.equal(JSON.parse(line).ObservedAt, '2026-03-04T05:06:07.000+00:00')

  const shifted = envelope({ observedAt: '2026-03-04T13:06:07+08:00' })
  assert.equal(journalCodec.serialize(shifted), line)
})

test('WHAT[DURABLE-EVENTS-002] Journal_codec_round_trip_preserves_fold_relevant_fields', () => {
  const original = envelope({ seq: 7, providerRun: 'run_meta' })
  const encoded = journalCodec.encode([], [], original)
  const decoded = journalCodec.decode(encoded)

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.deepEqual(readEnvelope(decoded.value), readEnvelope(original))
  assert.equal(journalCodec.serialize(decoded.value), journalCodec.serialize(original))
  assert.equal(encoded.eventType, journalCodec.JournalEnvelopeEventType)
})

test('WHAT[DURABLE-EVENTS-002] Journal_stream_owner_round_trips_all_public_stream_kinds', () => {
  for (const stream of [
    { kind: 'Workspace' },
    { kind: 'Session', id: SESSION },
    { kind: 'Child', id: 'child_meta' },
    { kind: 'Process', id: 'process_meta' },
  ]) {
    const streamId = journalCodec.encodeStreamId(stream)
    const decoded = journalCodec.decodeStreamId(streamId)
    assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
    assert.deepEqual(decoded.value, stream)
  }
})

test('WHAT[DURABLE-EVENTS-007] Journal_codec_refuses_unknown_facts_and_streams', () => {
  assert.throws(
    () => journalCodec.serialize(envelope({ stream: { kind: 'Unknown' } })),
    /unknown stream/i,
  )
  assert.throws(
    () => journalCodec.serialize(envelope({ fact: { family: 'Unknown', case: 'NoSuchFact', payload: {} } })),
    /unknown fact/i,
  )
})
