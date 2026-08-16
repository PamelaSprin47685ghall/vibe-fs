// Journal codec laws use plain JS envelopes and event objects only.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as journalCodec from '../../../dist/Persistence/Journal/CodecSurface.js'
import * as eventCodec from '../../../dist/Persistence/EventStore/CodecSurface.js'

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
  id: 'a'.repeat(40),
  stream: { kind: 'Session', id: SESSION },
  providerRun: null,
  fact: CLOSED,
  ...overrides,
})

const eventShape = (event) => ({
  id: event.eventId,
  stream: event.streamId,
  type: event.eventType,
  parents: event.parents,
  payload: event.payload,
  payloadRefs: event.payloadRefs,
})

const readEnvelope = (value) => ({
  runtime: value.runtime,
  seq: Number(value.seq),
  event: value.id,
  stream: value.stream,
  providerRun: value.providerRun,
  fact: value.fact.case,
  line: value.line,
})

const mustOk = (result, label = 'result') => {
  assert.equal(result.ok, true, `${label} should be Ok: ${JSON.stringify(result.error)}`)
  return result.value
}

test('WHAT[DURABLE-EVENTS-002] EventType_is_exactly_JournalEnvelope', () => {
  const encoded = journalCodec.encode([], [], env())
  assert.equal(encoded.eventType, 'JournalEnvelope')
  assert.equal(journalCodec.JournalEnvelopeEventType, 'JournalEnvelope')
  assert.equal(encoded.eventType, journalCodec.JournalEnvelopeEventType)
})

test('WHAT[DURABLE-EVENTS-002] encode_preserves_EventId', () => {
  const original = env({ seq: 7 })
  const encoded = journalCodec.encode([], [], original)
  assert.equal(encoded.eventId, original.id)
})

test('WHAT[DURABLE-EVENTS-002] encodeStreamId_scheme_is_stable_and_deterministic', () => {
  assert.equal(journalCodec.encodeStreamId({ kind: 'Workspace' }), 'journal/workspace')
  assert.equal(journalCodec.encodeStreamId({ kind: 'Session', id: SESSION }), 'journal/session/ses_a')
  assert.equal(journalCodec.encodeStreamId({ kind: 'Child', id: 'child_1' }), 'journal/child/child_1')
  assert.equal(journalCodec.encodeStreamId({ kind: 'Process', id: 'proc_9' }), 'journal/process/proc_9')

  for (const stream of [
    { kind: 'Workspace' },
    { kind: 'Session', id: SESSION },
    { kind: 'Child', id: 'child_1' },
    { kind: 'Process', id: 'proc_9' },
  ]) {
    const decoded = mustOk(journalCodec.decodeStreamId(journalCodec.encodeStreamId(stream)), 'decodeStreamId')
    assert.deepEqual(decoded, stream)
  }
})

test('WHAT[DURABLE-EVENTS-002] round_trip_preserves_fold_relevant_fields', () => {
  const original = env({ seq: 4, observedAt: '2026-03-04T05:06:07Z', providerRun: 'run_1' })
  const encoded = journalCodec.encode([], [], original)
  const decoded = mustOk(journalCodec.decode(encoded), 'decode')

  assert.deepEqual(readEnvelope(decoded), {
    runtime: original.runtime,
    seq: original.seq,
    event: original.id,
    stream: original.stream,
    providerRun: original.providerRun,
    fact: original.fact.case,
    line: journalCodec.serialize(original),
  })
  assert.equal(journalCodec.serialize(decoded), journalCodec.serialize(original))
})

test('WHAT[DURABLE-EVENTS-002] round_trip_fold_equates_with_journal_fold', () => {
  const original = env({ seq: 2, observedAt: '2026-02-03T04:05:06Z', providerRun: 'run_x' })
  const encoded = journalCodec.encode([], [], original)
  const decoded = mustOk(journalCodec.decode(encoded), 'decode')
  assert.deepEqual(readEnvelope(decoded), {
    ...readEnvelope(original),
    line: journalCodec.serialize(original),
  })
})

test('WHAT[DURABLE-EVENTS-003] parents_are_accepted_and_canonicalized', () => {
  const parentA = 'b'.repeat(40)
  const parentB = 'a'.repeat(40)
  const encoded = journalCodec.encode([parentA, parentB, parentA], [], env())
  assert.deepEqual(encoded.parents, [parentB, parentA])
})

test('WHAT[DURABLE-EVENTS-003] payloadRefs_are_accepted_and_canonicalized_without_RuntimePath_IO', () => {
  const encoded = journalCodec.encode([], ['ref-z', 'ref-a', 'ref-z'], env())
  assert.deepEqual(encoded.payloadRefs, ['ref-a', 'ref-z'])
})

test('WHAT[DURABLE-EVENTS-003] canonical_identity_bytes_stable_under_section_5_0', () => {
  const original = env({ seq: 3, observedAt: '2026-01-02T03:04:05Z', providerRun: 'run_stable' })
  const parents = ['c'.repeat(40), 'b'.repeat(40)]
  const refs = ['oid-2', 'oid-1']

  const a = journalCodec.encode(parents, refs, original)
  const b = journalCodec.encode([...parents].reverse(), [...refs].reverse(), original)

  assert.equal(eventCodec.encode(eventShape(a)), eventCodec.encode(eventShape(b)))
  assert.equal(eventCodec.checkIdentity(eventShape(a), eventShape(b)).ok, true)

  const redecoded = eventCodec.decode(eventCodec.encode(eventShape(a)))
  assert.equal(redecoded.ok, true, 'event decode should be Ok')
  assert.equal(eventCodec.encode(redecoded.event), eventCodec.encode(eventShape(a)))
})

test('WHAT[DURABLE-EVENTS-002] tryDecode_rejects_wrong_EventType', () => {
  const encoded = journalCodec.encode([], [], env({ seq: 1 }))
  const result = journalCodec.decode({ ...encoded, eventType: 'JobRequested' })
  assert.equal(result.ok, false)
  assert.match(result.error, /JournalEnvelope/)
})

test('WHAT[DURABLE-EVENTS-002] workspace_child_process_streams_round_trip', () => {
  const cases = [
    { stream: { kind: 'Workspace' }, seq: 1 },
    { stream: { kind: 'Child', id: 'ch_9' }, seq: 2 },
    { stream: { kind: 'Process', id: 'p_3' }, seq: 3 },
  ]
  for (const { stream, seq } of cases) {
    const original = env({ stream, seq })
    const decoded = mustOk(journalCodec.decode(journalCodec.encode([], [], original)))
    assert.deepEqual(readEnvelope(decoded), {
      ...readEnvelope(original),
      line: journalCodec.serialize(original),
    })
  }
})
