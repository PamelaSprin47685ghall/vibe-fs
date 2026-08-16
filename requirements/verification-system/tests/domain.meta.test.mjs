// VERIFY-008 owner-surface contract checks.
//
// This file is intentionally about compiled production contracts, not compiler
// representation. Each assertion enters through the registered owner that owns
// the fact: Process owns deadlines, Persistence/Journal owns the line codec,
// Context owns context recovery folds, and Provider/Attempt/Fallback owns the
// cursor projection. No Fable union, collection, or emitted-name helper crosses
// this test boundary.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as deadline from '../../../dist/Process/DeadlineSurface.js'
import * as journalCodec from '../../../dist/Persistence/Journal/CodecSurface.js'
import * as factCodec from '../../../dist/Persistence/Journal/FactCodecSurface.js'
import * as contextFold from '../../../dist/Context/Companion/FoldSurface.js'
import * as fallback from '../../../dist/Participant/Provider/Attempt/Fallback/Surface.js'

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

const contextReanchor = (overrides = {}) => ({
  runtime: 'rt_context',
  seq: 1,
  observedAt: '2026-03-04T05:06:07Z',
  id: 'context-event-1',
  session: 'ses_context',
  run: 'run_context',
  fact: {
    family: 'Context',
    case: 'ContextReanchored',
    payload: {
      SessionId: 'ses_context',
      PreviousEpochId: 0,
      NextEpochId: 1,
      ObservedCompactionRun: 'run_context',
    },
  },
  ...overrides,
})

const runtimeStarted = (startedAt = '2026-01-01T00:00:00Z') => ({
  family: 'Runtime',
  case: 'RuntimeStarted',
  payload: { RuntimeId: 'rt_meta', ProcessId: 42, StartedAt: startedAt },
})

// ── Process owner: deadline calculations carry instant semantics ────────────

test('WHAT[VERIFICATION-SYSTEM-008] Process_deadline_uses_explicit_offset_semantics', () => {
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', value), false)
  assert.equal(deadline.remainingMs('2026-01-01T00:00:05Z', value), 0)
  assert.equal(deadline.isExpired('2026-01-01T00:00:05Z', value), true)
  assert.equal(deadline.isExpired('2026-01-01T00:00:06Z', value), true)

  // Same instant expressed with a non-zero offset must produce the same answer.
  assert.equal(deadline.remainingMs('2026-01-01T08:00:02+08:00', value), 3000)
  assert.equal(deadline.isExpired('2026-01-01T08:00:02+08:00', value), false)
})

test('WHAT[VERIFICATION-SYSTEM-007] Process_deadline_is_independent_of_ambient_timezone', () => {
  const original = process.env.TZ
  const value = deadline.create('2026-01-01T00:00:00Z', 5000)

  try {
    for (const zone of ['UTC', 'Asia/Shanghai', 'America/Los_Angeles']) {
      process.env.TZ = zone
      assert.equal(deadline.isExpired('2026-01-01T00:00:02Z', value), false, `expired under TZ=${zone}`)
      assert.equal(deadline.remainingMs('2026-01-01T00:00:02Z', value), 3000, `remaining under TZ=${zone}`)
    }
  } finally {
    if (original === undefined) delete process.env.TZ
    else process.env.TZ = original
  }
})

// ── Journal owner: canonical envelope and migration bytes ──────────────────

test('WHAT[VERIFICATION-SYSTEM-008] Journal_codec_serializes_one_envelope_to_one_UTC_line', () => {
  const line = journalCodec.serialize(envelope())
  assert.equal(line.includes('\n'), false)
  assert.equal(line.includes('\r'), false)
  assert.equal(JSON.parse(line).ObservedAt, '2026-03-04T05:06:07.000+00:00')

  const shifted = envelope({ observedAt: '2026-03-04T13:06:07+08:00' })
  assert.equal(journalCodec.serialize(shifted), line)
})

test('WHAT[VERIFICATION-SYSTEM-008] Journal_codec_round_trip_preserves_fold_relevant_fields', () => {
  const original = envelope({ seq: 7, providerRun: 'run_meta' })
  const encoded = journalCodec.encode([], [], original)
  const decoded = journalCodec.decode(encoded)

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.deepEqual(readEnvelope(decoded.value), readEnvelope(original))
  assert.equal(journalCodec.serialize(decoded.value), journalCodec.serialize(original))
  assert.equal(encoded.eventType, journalCodec.JournalEnvelopeEventType)
})

test('WHAT[VERIFICATION-SYSTEM-008] Journal_stream_owner_round_trips_all_public_stream_kinds', () => {
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

test('WHAT[VERIFICATION-SYSTEM-008] Journal_codec_refuses_unknown_facts_and_streams', () => {
  assert.throws(
    () => journalCodec.serialize(envelope({ stream: { kind: 'Unknown' } })),
    /unknown stream/i,
  )
  assert.throws(
    () => journalCodec.serialize(envelope({ fact: { family: 'Unknown', case: 'NoSuchFact', payload: {} } })),
    /unknown fact/i,
  )
})

test('WHAT[VERIFICATION-SYSTEM-008] Fact_codec_reports_migration_markers_as_data_errors', () => {
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
    const line = JSON.stringify({ Fact: ['Agent', ['FallbackCursorAdvanced', { [marker]: 1 }]] })
    assert.equal(factCodec.containsLegacyFallbackFields(line), true, `${marker} must be refused`)
    const decoded = factCodec.decode(line)
    assert.equal(decoded.ok, false)
    assert.equal(decoded.error, factCodec.pre050MigrationMessage)
  }
})

test('WHAT[VERIFICATION-SYSTEM-008] Fact_codec_distinguishes_current_and_malformed_lines', () => {
  assert.equal(factCodec.containsLegacyFallbackFields(journalCodec.serialize(envelope())), false)

  const current = factCodec.decode(factCodec.encode(runtimeStarted()))
  assert.equal(current.ok, true, current.ok ? '' : current.error)
  assert.equal(current.case, 'RuntimeStarted')

  const malformed = factCodec.decode('{not json')
  assert.equal(malformed.ok, false)
  assert.equal(typeof malformed.error, 'string')
})

// ── Context owner: plain arrays cross the fold boundary ─────────────────────

test('WHAT[VERIFICATION-SYSTEM-008] Context_fold_accepts_plain_envelopes_and_replays_the_line_codec', () => {
  const folded = contextFold.fold([contextReanchor()])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.equal(Number(folded.value.sessions.ses_context.PrefixEpoch.EpochId), 1)

  const replayed = contextFold.replay([contextReanchor()])
  assert.equal(replayed.ok, true, replayed.ok ? '' : JSON.stringify(replayed.error))
  assert.deepEqual(replayed.value.sessions, folded.value.sessions)
})

test('WHAT[VERIFICATION-SYSTEM-008] Context_fold_rejects_unknown_fact_cases_loudly', () => {
  assert.throws(
    () => contextFold.fold([
      contextReanchor({
        fact: { family: 'Context', case: 'NoSuchContextFact', payload: {} },
      }),
    ]),
    /unknown context fact/i,
  )
})

// ── Fallback owner: cursor projection is a named semantic result ────────────

test('WHAT[VERIFICATION-SYSTEM-008] Fallback_owner_exposes_cursor_and_dedupe_state', () => {
  assert.deepEqual(fallback.ownerFailure(), {
    offset: 1,
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
  assert.deepEqual(fallback.counterfactualBloggerFailure(), {
    offset: 2,
    failures: 2,
    dedupeKeys: 2,
    exhausted: false,
  })
  assert.deepEqual(fallback.recordSuccess(), {
    offset: 1,
    failures: 0,
    dedupeKeys: 0,
    exhausted: false,
  })
})

// The explicit export checks are compiled-surface checks, not Fable export
// discovery. They keep this contract file loud when an owner surface is
// renamed or omitted from the published artifact.
test('WHAT[VERIFICATION-SYSTEM-008] registered_owner_surfaces_publish_their_contract_entries', () => {
  for (const [name, value] of Object.entries({
    'Process.Deadline.create': deadline.create,
    'Process.Deadline.remainingMs': deadline.remainingMs,
    'Journal.Codec.serialize': journalCodec.serialize,
    'Journal.FactCodec.decode': factCodec.decode,
    'Context.Fold.fold': contextFold.fold,
    'Fallback.ownerFailure': fallback.ownerFailure,
  })) {
    assert.equal(typeof value, 'function', `${name} must be callable`)
  }
})
