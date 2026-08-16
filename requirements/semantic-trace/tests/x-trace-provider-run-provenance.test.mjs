// SEMANTIC-TRACE-004 — provenance is segmented by provider run, never a single
// model value.
//
// Peer Fallback switches the execution binding; the history stays one person's.
// The durable part ref must carry the run that produced each part, and a
// reanchor must open a new generation so renumbered Host turns do not collide.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as xTrace from '../../../dist/Context/Trace/XTraceSurface.js'

const SESSION = 'ses_provenance'

let seq = 0
const next = (factValue, run) => xTrace.envelope({ seq: (seq += 1), session: SESSION, run, fact: factValue })

const partFact = ({ sequence, turn = 0, partIndex = 0, kind = 'text', run = `msg_p${sequence}`, provenance } = {}) =>
  next(
    xTrace.fact('XTracePartAppended', {
      sessionId: SESSION,
      sequence,
      role: 'assistant',
      turn,
      partIndex,
      kind,
      toolName: undefined,
      textRef: `blob-p${sequence}`,
      textDigest: `sha-p${sequence}`,
      provenance: provenance ?? `g:0/turn:${turn}/part:${partIndex}`,
      providerRun: run,
    }),
    run,
  )

const reanchorFact = ({ previousEpoch = 0, nextEpoch = 1, run = 'msg_compaction' } = {}) =>
  next(
    xTrace.fact('ContextReanchored', {
      sessionId: SESSION,
      previousEpochId: previousEpoch,
      nextEpochId: nextEpoch,
      observedCompactionRun: run,
    }),
    run,
  )

const foldOk = (envelopes) => {
  const result = xTrace.fold(envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return xTrace.session(result.value, SESSION)
}

test('WHAT[SEMANTIC-TRACE-004] SEMANTIC_TRACE_provider_run_segments_fold_projection', () => {
  const s = foldOk([
    partFact({ sequence: 1, run: 'run-a', provenance: 'g:0/turn:0/part:0' }),
    partFact({ sequence: 2, run: 'run-b', provenance: 'g:0/turn:1/part:0' }),
    partFact({ sequence: 3, run: 'run-c', provenance: 'g:0/turn:2/part:0' }),
  ])

  const parts = xTrace.parts(s.xTrace)
  assert.deepEqual(
    parts.map((p) => p.providerRun),
    ['run-a', 'run-b', 'run-c'],
    'each part keeps the provider run that produced it — fallback/agent switches do not collapse into one model value',
  )
})

test('WHAT[SEMANTIC-TRACE-004] SEMANTIC_TRACE_reanchor_opens_a_new_provenance_generation', () => {
  const s = foldOk([
    partFact({ sequence: 1, turn: 0, provenance: 'g:0/turn:0/part:0' }),
    reanchorFact(),
    // After compaction the Host renumbers turns from 0 again; generation must
    // disambiguate, otherwise this part would collide with the pre-reanchor one.
    partFact({ sequence: 2, turn: 0, provenance: 'g:1/turn:0/part:0', run: 'msg_post' }),
  ])

  const parts = xTrace.parts(s.xTrace)
  assert.equal(parts.length, 2)
  assert.equal(parts[0].generation, 0)
  assert.equal(parts[1].generation, 1)
  assert.equal(parts[0].turn, 0)
  assert.equal(parts[1].turn, 0)
  // Same (generation, turn, part) would collide; the generation is what keeps
  // the post-reanchor numbering distinct from the pre-reanchor one.
  assert.notEqual(parts[0].provenance, parts[1].provenance)
  assert.equal(s.prefixEpoch.epochId, 1)
})
