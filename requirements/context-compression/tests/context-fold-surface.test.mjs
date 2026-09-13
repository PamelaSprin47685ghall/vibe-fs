// Context companion fold surface: plain envelope fold, replay, and unknown fact rejection.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as contextFold from '../../../dist/Context/Companion/FoldSurface.js'

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

test('WHAT[CONTEXT-COMPRESSION-019] Context_fold_accepts_plain_envelopes_and_replays_the_line_codec', () => {
  const folded = contextFold.fold([contextReanchor()])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.equal(Number(folded.value.sessions.ses_context.PrefixEpoch.EpochId), 1)

  const replayed = contextFold.replay([contextReanchor()])
  assert.equal(replayed.ok, true, replayed.ok ? '' : JSON.stringify(replayed.error))
  assert.deepEqual(replayed.value.sessions, folded.value.sessions)
})

test('WHAT[CONTEXT-COMPRESSION-019] Context_fold_rejects_unknown_fact_cases_loudly', () => {
  assert.throws(
    () => contextFold.fold([
      contextReanchor({
        fact: { family: 'Context', case: 'NoSuchContextFact', payload: {} },
      }),
    ]),
    /unknown context fact/i,
  )
})
