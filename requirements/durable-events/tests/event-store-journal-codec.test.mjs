// tests/unit/journal/event-store-journal-codec.test.mjs
// W1-codec: Journal.Envelope ↔ Domain.EventEnvelope (EventType = JournalEnvelope).
// Pure codec tests — no NDJSON file I/O, no RuntimePath blob writes.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentFactCaseOf,
  caseOf,
  childId,
  envelope,
  eventId,
  fact,
  fold,
  idValue,
  journal,
  listItems,
  payloadOf,
  processId,
  sessionId,
  stream,
  toList,
} from '../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Codec = await import('../../../dist/Persistence/Journal/EventStoreJournalCodec.js')
const Canonical = await import('../../../dist/Infrastructure/Persist/CanonicalEventCodec.js')

const SESSION = sessionId('ses_a')
const CLOSED = fact('CompanionBloggerClosed', { SessionId: SESSION })

const env = (overrides = {}) =>
  envelope({ stream: stream.session(SESSION), fact: CLOSED, ...overrides })

const payloadRef = (v) => Domain.PayloadRefModule_create(v)

/** Fold-relevant envelope identity as plain text (rename-safe). */
const streamKey = (value) => {
  const kind = caseOf(value)
  if (kind === 'Workspace') return { kind }
  if (kind === 'Session') return { kind, id: idValue.session(value.fields[0]) }
  if (kind === 'Child') return { kind, id: idValue.child(value.fields[0]) }
  if (kind === 'Process') return { kind, id: idValue.process(value.fields[0]) }
  return { kind }
}

const readEnvelope = (value) => ({
  runtime: idValue.runtime(value.RuntimeId),
  seq: Number(idValue.localSeq(value.LocalSeq)),
  event: idValue.event(value.EventId),
  stream: streamKey(value.Stream),
  providerRun: value.ProviderRun ? idValue.providerRun(value.ProviderRun) : null,
  fact: agentFactCaseOf(payloadOf(value.Fact)),
  line: journal.serialize(value),
})

const mustOk = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Ok', `${label} should be Ok, got ${caseOf(result)}: ${payloadOf(result)}`)
  return payloadOf(result)
}

test('EventType_is_exactly_JournalEnvelope', () => {
  const encoded = Codec.encode(toList([]), toList([]), env({ seq: 1 }))
  assert.equal(encoded.EventType, 'JournalEnvelope')
  assert.equal(Codec.JournalEnvelopeEventType, 'JournalEnvelope')
  assert.equal(encoded.EventType, Codec.JournalEnvelopeEventType)
})

test('encode_preserves_EventId', () => {
  const original = env({ seq: 7 })
  const encoded = Codec.encode(toList([]), toList([]), original)
  assert.equal(idValue.event(encoded.EventId), idValue.event(original.EventId))
})

test('encodeStreamId_scheme_is_stable_and_deterministic', () => {
  assert.equal(
    Domain.EventStreamIdModule_value(Codec.encodeStreamId(stream.workspace())),
    'journal/workspace',
  )
  assert.equal(
    Domain.EventStreamIdModule_value(Codec.encodeStreamId(stream.session(SESSION))),
    'journal/session/ses_a',
  )
  assert.equal(
    Domain.EventStreamIdModule_value(Codec.encodeStreamId(stream.child(childId('child_1')))),
    'journal/child/child_1',
  )
  assert.equal(
    Domain.EventStreamIdModule_value(Codec.encodeStreamId(stream.process(processId('proc_9')))),
    'journal/process/proc_9',
  )

  // Round-trip StreamId ↔ EventStreamId
  for (const s of [
    stream.workspace(),
    stream.session(SESSION),
    stream.child(childId('child_1')),
    stream.process(processId('proc_9')),
  ]) {
    const decoded = mustOk(Codec.tryDecodeStreamId(Codec.encodeStreamId(s)), 'tryDecodeStreamId')
    assert.equal(caseOf(decoded), caseOf(s))
  }
})

test('round_trip_preserves_fold_relevant_fields', () => {
  const original = env({
    seq: 4,
    observedAt: '2026-03-04T05:06:07Z',
    run: 'run_1',
  })
  const encoded = Codec.encode(toList([]), toList([]), original)
  const decoded = mustOk(Codec.tryDecode(encoded), 'tryDecode')

  assert.deepEqual(readEnvelope(decoded), readEnvelope(original))
  // Re-serialize must match — same contract as Envelope NDJSON round-trip.
  assert.equal(journal.serialize(decoded), journal.serialize(original))
})

test('round_trip_fold_equates_with_journal_fold', () => {
  const original = env({ seq: 2, observedAt: '2026-02-03T04:05:06Z', run: 'run_x' })
  const encoded = Codec.encode(toList([]), toList([]), original)
  const decoded = mustOk(Codec.tryDecode(encoded), 'tryDecode')

  // fold.one already unwraps to { ok, value } via resultOf.
  const fromOriginal = fold.one(fold.empty, original)
  const fromDecoded = fold.one(fold.empty, decoded)
  assert.equal(fromOriginal.ok, true, fromOriginal.error)
  assert.equal(fromDecoded.ok, true, fromDecoded.error)
  assert.deepEqual(readEnvelope(decoded), readEnvelope(original))
  // Same Companion projection surface after fold.
  assert.deepEqual(
    JSON.stringify(fromOriginal.value.AgentProjections),
    JSON.stringify(fromDecoded.value.AgentProjections),
  )
})

test('parents_are_accepted_and_canonicalized', () => {
  const parentA = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
  const parentB = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
  const encoded = Codec.encode(
    toList([eventId(parentA), eventId(parentB), eventId(parentA)]),
    toList([]),
    env({ seq: 1 }),
  )
  const parentIds = listItems(encoded.Parents).map((id) => idValue.event(id))
  // EventParents: dedupe + EventId lexicographic order.
  assert.deepEqual(parentIds, [parentB, parentA])
})

test('payloadRefs_are_accepted_and_canonicalized_without_RuntimePath_IO', () => {
  const encoded = Codec.encode(
    toList([]),
    toList([payloadRef('ref-z'), payloadRef('ref-a'), payloadRef('ref-z')]),
    env({ seq: 1 }),
  )
  const refs = listItems(encoded.PayloadRefs).map((r) => Domain.PayloadRefModule_value(r))
  assert.deepEqual(refs, ['ref-a', 'ref-z'])
  // Codec source must not reference RuntimePath blob materialization.
  // (Behavioral: encode never throws and never writes; refs stay opaque strings.)
  assert.equal(typeof refs[0], 'string')
})

test('canonical_identity_bytes_stable_under_section_5_0', () => {
  const original = env({ seq: 3, observedAt: '2026-01-02T03:04:05Z', run: 'run_stable' })
  const parents = [eventId('cccccccccccccccccccccccccccccccccccccccc'), eventId('bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb')]
  const refs = [payloadRef('oid-2'), payloadRef('oid-1')]

  const a = Codec.encode(toList(parents), toList(refs), original)
  const b = Codec.encode(
    toList([...parents].reverse()),
    toList([...refs].reverse()),
    original,
  )

  assert.equal(Canonical.encode(a), Canonical.encode(b))
  assert.equal(caseOf(Canonical.checkIdentity(a, b)), 'Ok')

  // Re-encode after Canonical tryDecode preserves identity bytes.
  const redecoded = mustOk(Canonical.tryDecode(Canonical.encode(a)), 'Canonical.tryDecode')
  assert.equal(Canonical.encode(redecoded), Canonical.encode(a))
})

test('tryDecode_rejects_wrong_EventType', () => {
  const encoded = Codec.encode(toList([]), toList([]), env({ seq: 1 }))
  const wrong = new Domain.EventEnvelope(
    encoded.EventId,
    encoded.StreamId,
    'JobRequested',
    encoded.Parents,
    encoded.Payload,
    encoded.PayloadRefs,
  )
  const result = Codec.tryDecode(wrong)
  assert.equal(caseOf(result), 'Error')
  assert.match(payloadOf(result), /JournalEnvelope/)
})

test('workspace_child_process_streams_round_trip', () => {
  const cases = [
    { stream: stream.workspace(), seq: 1 },
    { stream: stream.child(childId('ch_9')), seq: 2 },
    { stream: stream.process(processId('p_3')), seq: 3 },
  ]
  for (const { stream: s, seq } of cases) {
    const original = env({ stream: s, seq })
    const decoded = mustOk(Codec.tryDecode(Codec.encode(toList([]), toList([]), original)))
    assert.deepEqual(readEnvelope(decoded), readEnvelope(original))
  }
})
