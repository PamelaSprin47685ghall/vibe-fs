import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");
const journalCodec = await import("../../../dist/Persistence/Journal/CodecSurface.js");
const factCodec = await import("../../../dist/Persistence/Journal/FactCodecSurface.js");

const SESSION = 'ses_a'
const ROOT_ACCEPTED = {
  family: 'Prompt',
  case: 'AuthorityRootAccepted',
  payload: {
    SchemaVersion: 2,
    SessionId: SESSION,
    LogicalRunId: 'run_a',
    AuthorityRootUserMessageId: 'msg_root_a',
    AuthorityKind: 'HumanRoot',
    ExpectedClosureKind: 'HumanRootManagedRun',
    OwnerLogicalRunId: undefined,
    AgentOwnerLife: undefined,
    IdentitySeed: {
      kind: 'RootSelection',
      ownerSession: null,
      ownerLogicalRun: null,
      ownerAuthorityRoot: null,
      participantIdentity: { participant: 'engineer', role: 'engineer', persona: 'Engineer', personaCatalogVersion: 1, origin: 'ResolvedAtRoot' },
    },
  },
}
const env = (overrides = {}) => ({
  runtime: 'rt_a',
  seq: 1,
  observedAt: '2026-01-02T03:04:05Z',
  id: 'a'.repeat(32),
  stream: { kind: 'Session', id: SESSION },
  providerRun: null,
  fact: ROOT_ACCEPTED,
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

test('WHAT[durable-events-002] PERSIST_001_an_envelope_serializes_to_exactly_one_line', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const journalCodec = await import("../../../dist/Persistence/Journal/CodecSurface.js");
const eventCodec = await import("../../../dist/Persistence/EventStore/CodecSurface.js");

const SESSION = 'ses_a'
const ROOT_ACCEPTED = {
  family: 'Prompt',
  case: 'AuthorityRootAccepted',
  payload: {
    SchemaVersion: 2,
    SessionId: SESSION,
    LogicalRunId: 'run_a',
    AuthorityRootUserMessageId: 'msg_root_a',
    AuthorityKind: 'HumanRoot',
    ExpectedClosureKind: 'HumanRootManagedRun',
    OwnerLogicalRunId: undefined,
    AgentOwnerLife: undefined,
    IdentitySeed: {
      kind: 'RootSelection',
      ownerSession: null,
      ownerLogicalRun: null,
      ownerAuthorityRoot: null,
      participantIdentity: { participant: 'engineer', role: 'engineer', persona: 'Engineer', personaCatalogVersion: 1, origin: 'ResolvedAtRoot' },
    },
  },
}
const env = (overrides = {}) => ({
  runtime: 'rt_a',
  seq: 1,
  observedAt: '2026-01-02T03:04:05Z',
  id: 'a'.repeat(40),
  stream: { kind: 'Session', id: SESSION },
  providerRun: null,
  fact: ROOT_ACCEPTED,
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

test('WHAT[durable-events-002] EventType_is_exactly_JournalEnvelope', () => {
  const encoded = journalCodec.encode([], [], env())
  assert.equal(encoded.eventType, 'JournalEnvelope')
  assert.equal(journalCodec.JournalEnvelopeEventType, 'JournalEnvelope')
  assert.equal(encoded.eventType, journalCodec.JournalEnvelopeEventType)
})
test('WHAT[durable-events-002] encode_preserves_EventId', () => {
  const original = env({ seq: 7 })
  const encoded = journalCodec.encode([], [], original)
  assert.equal(encoded.eventId, original.id)
})
test('WHAT[durable-events-002] encodeStreamId_scheme_is_stable_and_deterministic', () => {
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
test('WHAT[durable-events-002] round_trip_preserves_fold_relevant_fields', () => {
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
test('WHAT[durable-events-002] round_trip_fold_equates_with_journal_fold', () => {
  const original = env({ seq: 2, observedAt: '2026-02-03T04:05:06Z', providerRun: 'run_x' })
  const encoded = journalCodec.encode([], [], original)
  const decoded = mustOk(journalCodec.decode(encoded), 'decode')
  assert.deepEqual(readEnvelope(decoded), {
    ...readEnvelope(original),
    line: journalCodec.serialize(original),
  })
})
test('WHAT[durable-events-002] tryDecode_rejects_wrong_EventType', () => {
  const encoded = journalCodec.encode([], [], env({ seq: 1 }))
  const result = journalCodec.decode({ ...encoded, eventType: 'JobRequested' })
  assert.equal(result.ok, false)
  assert.match(result.error, /JournalEnvelope/)
})
test('WHAT[durable-events-002] workspace_child_process_streams_round_trip', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const factCodec = await import("../../../dist/Persistence/Journal/FactCodecSurface.js");

const runtimeStarted = (startedAt = '2026-01-01T00:00:00Z') => ({
  family: 'Runtime',
  case: 'RuntimeStarted',
  payload: { RuntimeId: 'rt_fact', ProcessId: 42, StartedAt: startedAt },
})
const handleAbandoned = (abandonedAt = '2026-01-01T00:00:00Z') => ({
  family: 'Execution',
  case: 'HandleAbandoned',
  payload: {
    ParentSessionId: 'ses_pin',
    Handle: 'h-pin',
    Reason: 'ParentCancelled',
    AbandonedAt: abandonedAt,
  },
})
const handleCompleted = (overrides = {}) => ({
  family: 'Execution',
  case: 'HandleCompleted',
  payload: {
    ParentSessionId: 'ses_hc',
    Handle: 'h-hc',
    Kind: 'Terminal',
    CompletionRef: null,
    CompletionDigest: null,
    ...overrides,
  },
})
const handleLinked = (overrides = {}) => ({
  family: 'Execution',
  case: 'HandleLinked',
  payload: {
    ParentSessionId: 'ses_hl',
    ChildSessionId: 'ses_hl_child',
    Handle: 'h-hl',
    TargetAgent: 'coder',
    Byname: 'Rhea',
    CanonicalRole: 'Engineer',
    Ownership: 'DurableParentHandle',
    ...overrides,
  },
})

test('WHAT[durable-events-002] handle_completed_with_completion_fields_round_trips_canonically', () => {
  const line = factCodec.encode(handleCompleted({
    ParentSessionId: 'ses_hc2',
    Handle: 'h-hc2',
    CompletionRef: 'blobs/ref-1',
    CompletionDigest: 'digest-1',
  }))
  assert.equal(line.includes('CompletionRef'), true)

  const decoded = factCodec.decode(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.line, line)
})
test('WHAT[durable-events-002] handle_completed_missing_completion_fields_is_rejected_without_decode_migration', () => {
  const line = factCodec.encode(handleCompleted())
  const missing = line
    .replace(/,"CompletionRef":null/g, '')
    .replace(/,"CompletionDigest":null/g, '')
    .replace(/"CompletionRef":null,/g, '')
    .replace(/"CompletionDigest":null,/g, '')
  assert.equal(missing.includes('CompletionRef'), false)
  assert.equal(missing.includes('CompletionDigest'), false)
  assert.equal(factCodec.decode(missing).ok, false)
})
test('WHAT[durable-events-002] malformed_completion_and_ownership_labels_fail_closed', () => {
  assert.throws(
    () => factCodec.encode(handleCompleted({ Kind: 'forged-kind' })),
    /unknown completion kind/i,
  )
  assert.throws(
    () => factCodec.encode(handleLinked({ CanonicalRole: 'forged-role' })),
    /unknown role/i,
  )
  assert.throws(
    () => factCodec.encode(handleLinked({ Ownership: 'forged-ownership' })),
    /unknown ownership/i,
  )
  assert.throws(
    () => factCodec.encode({
      family: 'Execution',
      case: 'HandleAbandoned',
      payload: {
        ParentSessionId: 'ses_hc',
        Handle: 'h-hc',
        Reason: 'forged-reason',
        AbandonedAt: '2026-01-01T00:00:00Z',
      },
    }),
    /unknown abandon reason/i,
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const hostTurn = await import("../../../dist/Execution/Delegation/HostTurnObservedSurface.js");

const observed = (overrides = {}) => ({
  SessionId: 'ses_obs',
  ProviderRun: 'run_abc',
  ObservedAt: '2026-04-01T08:00:00Z',
  ...overrides,
})

test('WHAT[durable-events-002] EXEC_HostTurnObserved_serializes_round_trip_with_provider_run', () => {
  const line = hostTurn.serialize(observed())
  assert.equal(line.includes('HostTurnObserved'), true)
  assert.equal(line.includes('run_abc'), true)

  const decoded = hostTurn.deserialize(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.case, 'HostTurnObserved')
  assert.equal(decoded.sessionId, 'ses_obs')
  assert.equal(decoded.providerRun, 'run_abc')
  assert.equal(decoded.line, line)
})
test('WHAT[durable-events-002] EXEC_HostTurnObserved_serializes_round_trip_without_provider_run', () => {
  const value = observed({ ProviderRun: null })
  const line = hostTurn.serialize(value)
  const decoded = hostTurn.deserialize(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.case, 'HostTurnObserved')
  assert.equal(decoded.providerRun == null, true)
  assert.equal(decoded.line, line)
})
test('WHAT[durable-events-002] EXEC_HostTurnObserved_fold_is_noop_on_agent_projection', () => {
  const folded = hostTurn.foldNoop(observed({ ProviderRun: 'run_1' }))
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.equal(folded.hasSession, false)
})
test('WHAT[durable-events-002] EXEC_HostTurnObserved_identity_key_is_session_plus_provider_run', () => {
  const withRun = observed({ SessionId: 'ses_a', ProviderRun: 'run_x' })
  const sameKeyLater = observed({ SessionId: 'ses_a', ProviderRun: 'run_x', ObservedAt: '2026-04-01T08:00:01Z' })
  const differentRun = observed({ SessionId: 'ses_a', ProviderRun: 'run_y' })

  assert.equal(hostTurn.identityKey(withRun), 'ses_a|run_x')
  assert.equal(hostTurn.identityKey(sameKeyLater), hostTurn.identityKey(withRun))
  assert.equal(hostTurn.identityKey(differentRun), 'ses_a|run_y')
  assert.notEqual(hostTurn.identityKey(withRun), hostTurn.identityKey(differentRun))
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const journalCodec = await import("../../../dist/Persistence/Journal/CodecSurface.js");

const SESSION = 'ses_meta'
const ROOT_ACCEPTED = {
  family: 'Prompt',
  case: 'AuthorityRootAccepted',
  payload: {
    SchemaVersion: 2,
    SessionId: SESSION,
    LogicalRunId: 'run_meta',
    AuthorityRootUserMessageId: 'msg_root_meta',
    AuthorityKind: 'HumanRoot',
    ExpectedClosureKind: 'HumanRootManagedRun',
    OwnerLogicalRunId: undefined,
    AgentOwnerLife: undefined,
    IdentitySeed: {
      kind: 'RootSelection',
      ownerSession: null,
      ownerLogicalRun: null,
      ownerAuthorityRoot: null,
      participantIdentity: { participant: 'engineer', role: 'engineer', persona: 'Engineer', personaCatalogVersion: 1, origin: 'ResolvedAtRoot' },
    },
  },
}
const envelope = (overrides = {}) => ({
  runtime: 'rt_meta',
  seq: 1,
  observedAt: '2026-03-04T05:06:07Z',
  id: 'a'.repeat(40),
  stream: { kind: 'Session', id: SESSION },
  providerRun: null,
  fact: ROOT_ACCEPTED,
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

test('WHAT[durable-events-002] Journal_codec_round_trip_preserves_fold_relevant_fields', () => {
  const original = envelope({ seq: 7, providerRun: 'run_meta' })
  const encoded = journalCodec.encode([], [], original)
  const decoded = journalCodec.decode(encoded)

  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.deepEqual(readEnvelope(decoded.value), readEnvelope(original))
  assert.equal(journalCodec.serialize(decoded.value), journalCodec.serialize(original))
  assert.equal(encoded.eventType, journalCodec.JournalEnvelopeEventType)
})
test('WHAT[durable-events-002] Journal_stream_owner_round_trips_all_public_stream_kinds', () => {
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
}


