// SEMANTIC-TRACE-009 — Host compaction must not delete the XTrace.
//
// The containment fact ContextReanchored retires the prefix and zeroes
// PrefixCoverage (that half is owned by prefix-stability / durable-events).
// This file pins the OTHER half: the XTrace parts and the Opening survive the
// reanchor byte for byte. If a future writer "cleaned up" the trace on
// compaction, Y's gap-filling and LWR self-containment both break (HOST-005).

import assert from 'node:assert/strict'
import test from 'node:test'
import { envelope, fact, fold, sessionId, stream, providerRun, blobRef, blobDigest, prefixEpochId, idValue, listItems } from '../../verification-system/tests/support/domain.mjs'

const { XTraceProjection_parts: xTraceParts } = await import('../../../dist/Journal/XTraceProjection.js')

const SESSION = 'ses_survive'
const session = sessionId(SESSION)

let seq = 0
const next = (factValue, run) => envelope({ seq: (seq += 1), stream: stream.session(session), run, fact: factValue })

const openingFact = ({ assignment = 'first task', requirements = ['r1'], run = 'msg_o1' } = {}) =>
  next(
    fact('OpeningPromptCaptured', {
      SessionId: session,
      AssignmentText: assignment,
      AuthoritativeRequirements: requirements,
      ProviderRun: providerRun(run),
    }),
    run,
  )

const partFact = ({ sequence, role = 'user', turn = 0, partIndex = 0, kind = 'text', run = `msg_p${sequence}` } = {}) =>
  next(
    fact('XTracePartAppended', {
      SessionId: session,
      CursorSequence: BigInt(sequence),
      Role: role,
      Turn: turn,
      PartIndex: partIndex,
      Kind: kind,
      ToolName: undefined,
      TextRef: blobRef(`blob-p${sequence}`),
      TextDigest: blobDigest(`sha-p${sequence}`),
      Provenance: `g:0/turn:${turn}/part:${partIndex}`,
      ProviderRun: providerRun(run),
    }),
    run,
  )

const reanchorFact = ({ previousEpoch = 0, nextEpoch = 1, run = 'msg_compaction' } = {}) =>
  next(
    fact('ContextReanchored', {
      SessionId: session,
      PreviousEpochId: prefixEpochId(previousEpoch),
      NextEpochId: prefixEpochId(nextEpoch),
      ObservedCompactionRun: providerRun(run),
    }),
    run,
  )

const foldOk = (envelopes) => {
  const result = fold.apply(fold.empty, envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return fold.session(result.value, SESSION)
}

test('SEMANTIC_TRACE_reanchor_preserves_xtrace_parts_and_opening', () => {
  const base = [openingFact(), partFact({ sequence: 1, role: 'user' }), partFact({ sequence: 2, role: 'assistant' })]

  const before = foldOk(base)
  const after = foldOk([...base, reanchorFact()])

  // The prefix half moved (that is the point of the fact) …
  assert.equal(after.PrefixEpoch.Snapshot, undefined)
  assert.equal(idValue.prefixEpoch(after.PrefixEpoch.EpochId), 1n)

  // … but the trace is untouched: same part refs, same opening, same coverage base.
  assert.deepEqual(listItems(xTraceParts(after.XTrace)), listItems(xTraceParts(before.XTrace)), 'XTrace parts must survive reanchor')
  assert.equal(after.XTrace.Opening.AssignmentText, 'first task')
  assert.deepEqual(after.XTrace.Opening.AuthoritativeRequirements, ['r1'])
})

test('SEMANTIC_TRACE_reanchor_does_not_reset_the_cursor_sequence', () => {
  const s = foldOk([
    partFact({ sequence: 1 }),
    partFact({ sequence: 2 }),
    reanchorFact(),
    partFact({ sequence: 3, run: 'msg_post' }),
  ])

  const parts = listItems(xTraceParts(s.XTrace))
  assert.deepEqual(
    parts.map((p) => Number(p.Cursor.Sequence)),
    [1, 2, 3],
    'cursor keeps counting across the reanchor; Host turn indices are the only thing that restart',
  )
})
