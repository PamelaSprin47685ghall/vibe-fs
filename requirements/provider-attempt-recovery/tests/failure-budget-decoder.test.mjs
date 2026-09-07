// failure-budget-decoder.test.mjs — legacy Fallback byte migration (PAR-002).
//
// The ONLY test in this package allowed to carry raw legacy Fallback bytes.
// It proves the one-way migration: a stored `Fallback` envelope
// (FallbackCursorAdvanced with PreviousOffset/NextOffset, FallbackExhausted
// with FinalOffset) decodes into the ProviderFailure family with the count
// preserved and the offsets dropped — and the re-encoded line never carries
// offsets again. All other tests use the ProviderFailure family directly.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as codec from '../../../dist/Persistence/Journal/CodecSurface.js'

const legacyAdvance = JSON.stringify({
  RuntimeId: ['RuntimeId', 'rt-1'],
  LocalSeq: ['LocalSeq', '2'],
  ObservedAt: '2026-01-01T00:00:00.000+00:00',
  EventId: ['EventId', 'e2'],
  Stream: ['Session', ['SessionId', 'ses_a']],
  ProviderRun: ['ProviderRunIdentity', 'p1'],
  Fact: ['Agent', ['Fallback', ['FallbackCursorAdvanced', {
    SessionId: ['SessionId', 'ses_a'],
    LogicalRunId: ['LogicalRunId', 'run_L'],
    AuthorityRootUserMessageId: ['AuthorityRootUserMessageId', 'msg_u1'],
    ProviderRun: ['ProviderRunIdentity', 'p1'],
    PreviousOffset: 0,
    NextOffset: 1,
    ConsecutiveFailureCount: 1,
    Reason: 'provider_error',
  }]]],
})

const legacyExhausted = JSON.stringify({
  RuntimeId: ['RuntimeId', 'rt-1'],
  LocalSeq: ['LocalSeq', '3'],
  ObservedAt: '2026-01-01T00:00:00.000+00:00',
  EventId: ['EventId', 'e3'],
  Stream: ['Session', ['SessionId', 'ses_a']],
  ProviderRun: ['ProviderRunIdentity', 'p1'],
  Fact: ['Agent', ['Fallback', ['FallbackExhausted', {
    SessionId: ['SessionId', 'ses_a'],
    LogicalRunId: ['LogicalRunId', 'run_L'],
    AuthorityRootUserMessageId: ['AuthorityRootUserMessageId', 'msg_u1'],
    FinalConsecutiveFailureCount: 12,
    FinalOffset: 3,
  }]]],
})

const legacySucceeded = JSON.stringify({
  RuntimeId: ['RuntimeId', 'rt-1'],
  LocalSeq: ['LocalSeq', '4'],
  ObservedAt: '2026-01-01T00:00:00.000+00:00',
  EventId: ['EventId', 'e4'],
  Stream: ['Session', ['SessionId', 'ses_a']],
  ProviderRun: ['ProviderRunIdentity', 'p1'],
  Fact: ['Agent', ['Fallback', ['FallbackSucceeded', {
    SessionId: ['SessionId', 'ses_a'],
    LogicalRunId: ['LogicalRunId', 'run_L'],
    AuthorityRootUserMessageId: ['AuthorityRootUserMessageId', 'msg_u1'],
    ProviderRun: ['ProviderRunIdentity', 'p1'],
  }]]],
})

test('WHAT[PAR-002] legacy_fallback_bytes_decode_one_way_and_never_re_encode_offsets', () => {
  const advance = codec.deserialize(legacyAdvance)
  assert.equal(advance.ok, true, advance.ok ? '' : advance.error)
  assert.match(advance.value.line, /FailureRecorded/)
  assert.match(advance.value.line, /"ConsecutiveFailureCount":1/)
  assert.doesNotMatch(advance.value.line, /Offset/)

  const exhausted = codec.deserialize(legacyExhausted)
  assert.equal(exhausted.ok, true, exhausted.ok ? '' : exhausted.error)
  assert.match(exhausted.value.line, /RetryExhausted/)
  assert.match(exhausted.value.line, /"FinalConsecutiveFailureCount":12/)
  assert.doesNotMatch(exhausted.value.line, /Offset/)

  const succeeded = codec.deserialize(legacySucceeded)
  assert.equal(succeeded.ok, true, succeeded.ok ? '' : succeeded.error)
  assert.match(succeeded.value.line, /SuccessRecorded/)
  assert.doesNotMatch(succeeded.value.line, /Offset/)
})
