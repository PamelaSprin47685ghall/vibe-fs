// SEMANTIC-TRACE-004 — provenance is segmented by provider run, never a single
// model value.
//
// Peer Fallback switches the execution binding; the history stays one person's.
// The durable part ref must carry the run that produced each part, and a
// reanchor must open a new generation so renumbered Host turns do not collide.

import assert from 'node:assert/strict'
import test from 'node:test'
import { envelope, fact, fold, sessionId, stream, providerRun, blobRef, blobDigest, prefixEpochId, idValue, listItems } from '../../verification-system/tests/support/domain.mjs'

const { XTraceProjection_parts: xTraceParts } = await import('../../../dist/Journal/XTraceProjection.js')

const SESSION = 'ses_provenance'
const session = sessionId(SESSION)

let seq = 0
const next = (factValue, run) => envelope({ seq: (seq += 1), stream: stream.session(session), run, fact: factValue })

const partFact = ({ sequence, turn = 0, partIndex = 0, kind = 'text', run = `msg_p${sequence}`, provenance } = {}) =>
  next(
    fact('XTracePartAppended', {
      SessionId: session,
      CursorSequence: BigInt(sequence),
      Role: 'assistant',
      Turn: turn,
      PartIndex: partIndex,
      Kind: kind,
      ToolName: undefined,
      TextRef: blobRef(`blob-p${sequence}`),
      TextDigest: blobDigest(`sha-p${sequence}`),
      Provenance: provenance ?? `g:0/turn:${turn}/part:${partIndex}`,
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

test('SEMANTIC_TRACE_provider_run_segments_fold_projection', () => {
  const s = foldOk([
    partFact({ sequence: 1, run: 'run-a', provenance: 'g:0/turn:0/part:0' }),
    partFact({ sequence: 2, run: 'run-b', provenance: 'g:0/turn:1/part:0' }),
    partFact({ sequence: 3, run: 'run-c', provenance: 'g:0/turn:2/part:0' }),
  ])

  const parts = listItems(xTraceParts(s.XTrace))
  assert.deepEqual(
    parts.map((p) => p.ProviderRun),
    [providerRun('run-a'), providerRun('run-b'), providerRun('run-c')],
    'each part keeps the provider run that produced it — fallback/agent switches do not collapse into one model value',
  )
})

test('SEMANTIC_TRACE_reanchor_opens_a_new_provenance_generation', () => {
  const s = foldOk([
    partFact({ sequence: 1, turn: 0, provenance: 'g:0/turn:0/part:0' }),
    reanchorFact(),
    // After compaction the Host renumbers turns from 0 again; generation must
    // disambiguate, otherwise this part would collide with the pre-reanchor one.
    partFact({ sequence: 2, turn: 0, provenance: 'g:1/turn:0/part:0', run: 'msg_post' }),
  ])

  const parts = listItems(xTraceParts(s.XTrace))
  assert.equal(parts.length, 2)
  assert.equal(parts[0].Generation, 0)
  assert.equal(parts[1].Generation, 1)
  assert.equal(parts[0].Turn, 0)
  assert.equal(parts[1].Turn, 0)
  // Same (generation, turn, part) would collide; the generation is what keeps
  // the post-reanchor numbering distinct from the pre-reanchor one.
  assert.notEqual(parts[0].Provenance, parts[1].Provenance)
  assert.equal(idValue.prefixEpoch(s.PrefixEpoch.EpochId), 1n)
})
