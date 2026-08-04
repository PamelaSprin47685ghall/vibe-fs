/**
 * C5: BloggerRequestMaterialized + unified Entry|Squash ProviderRun receipts.
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  blobDigest,
  blobRef,
  bloggerRequestId,
  envelope,
  fact,
  fold,
  frameEpochId,
  prefixEpochId,
  promptKey,
  providerRun,
  sessionId,
  stream,
  toList,
} from '../support/domain.mjs'

const session = sessionId('ses-main')
const blogger = sessionId('ses-blogger')

let seq = 0
const next = (factValue, run) =>
  envelope({ seq: (seq += 1), stream: stream.session(session), run, fact: factValue })

const foldOk = (envelopes) => {
  const result = fold.replay(envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return fold.session(result.value, 'ses-main')
}

const foldErr = (envelopes) => {
  const result = fold.replay(envelopes)
  assert.equal(result.ok, false, 'expected fold rejection')
  return result.error
}

const materialize = ({ requestId = 'req-1', kind = 'main', n = 1, run } = {}) =>
  next(
    fact('BloggerRequestMaterialized', {
      RequestId: bloggerRequestId(requestId),
      MainSessionId: session,
      BloggerSessionId: blogger,
      RequestKind: kind,
      ContextRef: blobRef(`blob-ctx-${n}`),
      ContextDigest: blobDigest(`sha-ctx-${n}`),
      ObservedPrefixEpochId: prefixEpochId(0),
      PreviousIngestedThroughSequence: 0n,
      NextIngestedThroughSequence: kind === 'main' ? 1n : 0n,
      FrameEpochId: frameEpochId(0),
      SelectedFrameDigests: toList([]),
      PromptKey: undefined,
    }),
    run,
  )

const entry = ({ requestId = 'req-1', run = 'msg_e1', n = 1 } = {}) =>
  next(
    fact('BlogEntryCommitted', {
      SessionId: session,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId(requestId),
      FrameEpochId: frameEpochId(0),
      PreviousIngestedThroughSequence: 0n,
      NextIngestedThroughSequence: 1n,
      PreviousCoverableTurnCutoffExclusive: 0,
      NextCoverableTurnCutoffExclusive: 1,
      NextCoveredPrefixDigest: 'd-1',
      TextRef: blobRef(`blob-e${n}`),
      TextDigest: blobDigest(`sha-e${n}`),
      ProviderRun: providerRun(run),
      ToolCallIds: [],
      ScoreVectorRef: undefined,
      EvidenceRef: undefined,
      ObservedPrefixEpochId: prefixEpochId(0),
    }),
    run,
  )

const squash = ({ requestId = 'req-s1', run = 'msg_s1', n = 1 } = {}) =>
  next(
    fact('BlogSquashCommitted', {
      SessionId: session,
      BloggerSessionId: blogger,
      RequestId: bloggerRequestId(requestId),
      PreviousFrameEpochId: frameEpochId(0),
      NextFrameEpochId: frameEpochId(1),
      CoveredFrameCount: 1,
      TextRef: blobRef(`blob-s${n}`),
      TextDigest: blobDigest(`sha-s${n}`),
      ProviderRun: providerRun(run),
    }),
    run,
  )

test('C5_materialize_opens_request_queryable_by_blogger', () => {
  const s = foldOk([materialize({ requestId: 'req-open' })])
  assert.ok(s.BloggerCycles, 'BloggerCycles projection exists')
  assert.equal(s.BloggerCycles.OpenByRequestId.size, 1)
  assert.equal(s.BloggerCycles.OpenByBlogger.size, 1)
})

test('C5_entry_commit_records_receipt_and_clears_open_request', () => {
  const s = foldOk([
    materialize({ requestId: 'req-e1' }),
    entry({ requestId: 'req-e1', run: 'msg_e1' }),
  ])
  assert.equal(s.BloggerCycles.OpenByRequestId.size, 0, 'open cleared')
  assert.equal(s.BloggerCycles.ByProviderRun.size, 1, 'receipt indexed')
  assert.equal(s.Enforcement.ByProviderRun.size, 1, 'enforcement half still present')
})

test('C5_same_provider_run_cannot_be_both_entry_and_squash', () => {
  const error = foldErr([
    entry({ requestId: 'req-e1', run: 'msg_same' }),
    // Need a frame for squash — seed with entry first then try same run squash via
    // a second line with same ProviderRun is enough for receipt reject.
    next(
      fact('BlogSquashCommitted', {
        SessionId: session,
        BloggerSessionId: blogger,
        RequestId: bloggerRequestId('req-s1'),
        PreviousFrameEpochId: frameEpochId(0),
        NextFrameEpochId: frameEpochId(1),
        CoveredFrameCount: 1,
        TextRef: blobRef('blob-s-same'),
        TextDigest: blobDigest('sha-s-same'),
        ProviderRun: providerRun('msg_same'),
      }),
      'msg_same',
    ),
  ])
  assert.ok(error, 'mixed Entry+Squash on one ProviderRun must fail')
})

test('C5_same_request_materialize_is_idempotent', () => {
  // Same RequestId + same context digest: restart re-materialize must not fail.
  const s = foldOk([
    materialize({ requestId: 'req-idem', n: 1 }),
    materialize({ requestId: 'req-idem', n: 1 }),
  ])
  assert.equal(s.BloggerCycles.OpenByRequestId.size, 1)
})

test('C5_materialize_prompt_key_fill_in_after_send', () => {
  // Pre-send PromptKey=None; post-send same context + Some PromptKey is the
  // RequestId ownership binding. Must not reject as "different context".
  const base = materialize({ requestId: 'req-key', n: 1 })
  const withKey = next(
    fact('BloggerRequestMaterialized', {
      RequestId: bloggerRequestId('req-key'),
      MainSessionId: session,
      BloggerSessionId: blogger,
      RequestKind: 'main',
      ContextRef: blobRef('blob-ctx-1'),
      ContextDigest: blobDigest('sha-ctx-1'),
      ObservedPrefixEpochId: prefixEpochId(0),
      PreviousIngestedThroughSequence: 0n,
      NextIngestedThroughSequence: 1n,
      FrameEpochId: frameEpochId(0),
      SelectedFrameDigests: toList([]),
      PromptKey: promptKey('pk-blog-1'),
    }),
  )
  const s = foldOk([base, withKey])
  const open = [...s.BloggerCycles.OpenByRequestId.values()][0]
  assert.ok(open.PromptKey, 'PromptKey filled in on open request')
})

test('C5_materialize_prompt_key_cannot_rebind', () => {
  const first = next(
    fact('BloggerRequestMaterialized', {
      RequestId: bloggerRequestId('req-rebind'),
      MainSessionId: session,
      BloggerSessionId: blogger,
      RequestKind: 'main',
      ContextRef: blobRef('blob-ctx-1'),
      ContextDigest: blobDigest('sha-ctx-1'),
      ObservedPrefixEpochId: prefixEpochId(0),
      PreviousIngestedThroughSequence: 0n,
      NextIngestedThroughSequence: 1n,
      FrameEpochId: frameEpochId(0),
      SelectedFrameDigests: toList([]),
      PromptKey: promptKey('pk-a'),
    }),
  )
  const second = next(
    fact('BloggerRequestMaterialized', {
      RequestId: bloggerRequestId('req-rebind'),
      MainSessionId: session,
      BloggerSessionId: blogger,
      RequestKind: 'main',
      ContextRef: blobRef('blob-ctx-1'),
      ContextDigest: blobDigest('sha-ctx-1'),
      ObservedPrefixEpochId: prefixEpochId(0),
      PreviousIngestedThroughSequence: 0n,
      NextIngestedThroughSequence: 1n,
      FrameEpochId: frameEpochId(0),
      SelectedFrameDigests: toList([]),
      PromptKey: promptKey('pk-b'),
    }),
  )
  const error = foldErr([first, second])
  assert.ok(error, 'PromptKey rebind must fail')
})

test('C5_duplicate_request_materialize_different_context_rejected', () => {
  const error = foldErr([
    materialize({ requestId: 'req-dup' }),
    materialize({ requestId: 'req-dup', n: 2 }),
  ])
  assert.ok(error, 'same RequestId with different context digest must fail')
})

test('C5_abandon_clears_open_request', () => {
  const abandon = next(
    fact('BloggerRequestAbandoned', {
      RequestId: bloggerRequestId('req-ab'),
      MainSessionId: session,
      BloggerSessionId: blogger,
      Reason: 'send failed',
    }),
  )
  const s = foldOk([materialize({ requestId: 'req-ab' }), abandon])
  assert.equal(s.BloggerCycles.OpenByRequestId.size, 0)
  assert.equal(s.BloggerCycles.ByProviderRun.size, 0)
})

test('C5_request_id_cannot_rebind_to_different_provider_run', () => {
  const error = foldErr([
    entry({ requestId: 'req-bind', run: 'msg_a', n: 1 }),
    // Second entry advances coverage but reuses RequestId with a different run.
    next(
      fact('BlogEntryCommitted', {
        SessionId: session,
        BloggerSessionId: blogger,
        RequestId: bloggerRequestId('req-bind'),
        FrameEpochId: frameEpochId(0),
        PreviousIngestedThroughSequence: 1n,
        NextIngestedThroughSequence: 2n,
        PreviousCoverableTurnCutoffExclusive: 1,
        NextCoverableTurnCutoffExclusive: 2,
        NextCoveredPrefixDigest: 'd-2',
        TextRef: blobRef('blob-e2'),
        TextDigest: blobDigest('sha-e2'),
        ProviderRun: providerRun('msg_b'),
        ToolCallIds: [],
        ScoreVectorRef: undefined,
        EvidenceRef: undefined,
        ObservedPrefixEpochId: prefixEpochId(0),
      }),
      'msg_b',
    ),
  ])
  assert.ok(error, 'RequestId rebinding must fail')
})
