// SEMANTIC-TRACE-009 — Host compaction must not delete the XTrace.
//
// The containment fact ContextReanchored retires the prefix and zeroes
// PrefixCoverage (that half is owned by prefix-stability / durable-events).
// This file pins the OTHER half: the XTrace parts and the Opening survive the
// reanchor byte for byte. If a future writer "cleaned up" the trace on
// compaction, Y's gap-filling and LWR self-containment both break (HOST-005).

import assert from 'node:assert/strict'
import test from 'node:test'
import * as xTrace from '../../../dist/Context/Trace/XTraceSurface.js'

const SESSION = 'ses_survive'

let seq = 0
const next = (factValue, run) => xTrace.envelope({ seq: (seq += 1), session: SESSION, run, fact: factValue })

const openingFact = ({ assignment = 'first task', requirements = ['r1'], run = 'msg_o1' } = {}) =>
  next(
    xTrace.fact('OpeningPromptCaptured', {
      sessionId: SESSION,
      assignmentText: assignment,
      authoritativeRequirements: requirements,
      providerRun: run,
    }),
    run,
  )

const partFact = ({ sequence, role = 'user', turn = 0, partIndex = 0, kind = 'text', run = `msg_p${sequence}` } = {}) =>
  next(
    xTrace.fact('XTracePartAppended', {
      sessionId: SESSION,
      sequence,
      role,
      turn,
      partIndex,
      kind,
      toolName: undefined,
      textRef: `blob-p${sequence}`,
      textDigest: `sha-p${sequence}`,
      provenance: `g:0/turn:${turn}/part:${partIndex}`,
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

test('WHAT[SEMANTIC-TRACE-009] SEMANTIC_TRACE_reanchor_preserves_xtrace_parts_and_opening', () => {
  const base = [openingFact(), partFact({ sequence: 1, role: 'user' }), partFact({ sequence: 2, role: 'assistant' })]

  const before = foldOk(base)
  const after = foldOk([...base, reanchorFact()])

  // The prefix half moved (that is the point of the fact) …
  assert.equal(after.prefixEpoch.snapshot, null)
  assert.equal(after.prefixEpoch.epochId, 1)

  // … but the trace is untouched: same part refs, same opening, same coverage base.
  assert.deepEqual(xTrace.parts(after.xTrace), xTrace.parts(before.xTrace), 'XTrace parts must survive reanchor')
  assert.equal(after.xTrace.opening.assignmentText, 'first task')
  assert.deepEqual(after.xTrace.opening.authoritativeRequirements, ['r1'])
})

test('WHAT[SEMANTIC-TRACE-009] SEMANTIC_TRACE_reanchor_does_not_reset_the_cursor_sequence', () => {
  const s = foldOk([
    partFact({ sequence: 1 }),
    partFact({ sequence: 2 }),
    reanchorFact(),
    partFact({ sequence: 3, run: 'msg_post' }),
  ])

  const parts = xTrace.parts(s.xTrace)
  assert.deepEqual(
    parts.map((p) => p.cursor.sequence),
    [1, 2, 3],
    'cursor keeps counting across the reanchor; Host turn indices are the only thing that restart',
  )
})
