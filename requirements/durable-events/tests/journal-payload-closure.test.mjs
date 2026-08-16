// JournalPayloadClosure — the single mapping from a Journal fact to its EventStore
// payload dependencies (DURABLE-EVENTS-012). A blob handle is a payload reference
// only when it is a real lowercase-sha256 content address; placeholder strings a
// test may carry are not payload dependencies.
//
// Pure test: no NDJSON file I/O, no payload writes.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
  fact,
  handleId,
  listItems,
  managerLifecycleFact,
  managerLifeId,
  physicalUser,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../dist/Persistence/EventStore/Model.js')
const Writer = await import('../../../dist/Persistence/Journal/EventStoreJournalWriter.js')

const closureOf = Writer.JournalPayloadClosure_ofFact
const refsOf = (value) => listItems(value).map((r) => Domain.PayloadRefModule_value(r))

const hex = (ch) => ch.repeat(64)

test('WHAT[DURABLE-EVENTS-012] closure_lifts_a_content_addressed_digest_into_payload_refs', () => {
  const digest = hex('a')
  const f = fact('ParentJoinCorrectionRequested', {
    ParentSessionId: sessionId('ses_p'),
    OriginalHandle: handleId.agent('h1'),
    ReplacementHandle: handleId.agent('h2'),
    BadCompletionDigest: blobDigest(digest),
  })
  assert.deepEqual(refsOf(closureOf(f)), [digest])
})

test('WHAT[DURABLE-EVENTS-012] closure_dedupes_a_matching_blob_ref_and_digest_pair', () => {
  const digest = hex('b')
  const f = managerLifecycleFact('LifeOpened', {
    SessionId: sessionId('ses_l'),
    LifeId: managerLifeId('life_1'),
    OpeningUserMessageId: physicalUser('m_1'),
    OpeningTextRef: blobRef(`blobs/${digest}`),
    OpeningTextDigest: blobDigest(digest),
    OpeningCursorSequence: 1n,
  })
  assert.deepEqual(refsOf(closureOf(f)), [digest])
})

test('WHAT[DURABLE-EVENTS-012] closure_ignores_non_content_addressed_placeholder_handles', () => {
  // Placeholder strings (not sha256) are not EventStore payload dependencies.
  const f = fact('XTracePartAppended', {
    SessionId: sessionId('ses_x'),
    CursorSequence: 1n,
    Role: 'user',
    Turn: 0,
    PartIndex: 0,
    Kind: 'text',
    ToolName: undefined,
    TextRef: blobRef('blobs/placeholder'),
    TextDigest: blobDigest('sha-placeholder'),
    Provenance: 'turn:0/part:0',
    ProviderRun: undefined,
    ToolCallId: undefined,
    HostToolPartId: undefined,
  })
  assert.deepEqual(refsOf(closureOf(f)), [])
})

test('WHAT[DURABLE-EVENTS-012] closure_is_empty_for_a_fact_without_blob_fields', () => {
  const f = fact('CompanionBloggerClosed', { SessionId: sessionId('ses_c') })
  assert.deepEqual(refsOf(closureOf(f)), [])
})
