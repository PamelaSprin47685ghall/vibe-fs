// SEMANTIC-TRACE-002 / SEMANTIC-TRACE-008 — typed capture boundary and the
// "never before it happened" capture law.
//
// Two structural claims:
//   1. The durable XTrace part ref carries only semantic + provenance identity —
//      no transport metadata (usage/cost/timestamp/directory/finish/runtime id).
//   2. The XTrace projection's only writers are the three capture facts
//      (OpeningPromptCaptured / XTracePartAppended / TerminalOutputCaptured);
//      any speculative/candidate fact family would trip the capture law.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { envelope, fact, fold, sessionId, stream, providerRun, blobRef, blobDigest, listItems, idValue } from '../../verification-system/tests/support/domain.mjs'

const { XTraceProjection_parts: xTraceParts } = await import('../../../dist/Context/Trace/Projection.js')

const SESSION = 'ses_boundary'
const session = sessionId(SESSION)

let seq = 0
const next = (factValue, run) => envelope({ seq: (seq += 1), stream: stream.session(session), run, fact: factValue })

const partFact = ({ sequence, role = 'user', turn = 0, partIndex = 0, kind = 'text', toolName = undefined, run = `msg_p${sequence}` } = {}) =>
  next(
    fact('XTracePartAppended', {
      SessionId: session,
      CursorSequence: BigInt(sequence),
      Role: role,
      Turn: turn,
      PartIndex: partIndex,
      Kind: kind,
      ToolName: toolName,
      TextRef: blobRef(`blob-p${sequence}`),
      TextDigest: blobDigest(`sha-p${sequence}`),
      Provenance: `g:0/turn:${turn}/part:${partIndex}`,
      ProviderRun: providerRun(run),
    }),
    run,
  )

const foldOk = (envelopes) => {
  const result = fold.apply(fold.empty, envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return fold.session(result.value, SESSION)
}

test('SEMANTIC_TRACE_capture_boundary_excludes_transport_metadata', () => {
  const s = foldOk([
    partFact({ sequence: 1, kind: 'text' }),
    partFact({ sequence: 2, kind: 'tool_call', toolName: 'todowrite' }),
  ])

  const parts = listItems(xTraceParts(s.XTrace))
  assert.equal(parts.length, 2)

  const keys = new Set(Object.keys(parts[0]))
  // The durable identity is exactly: cursor + provenance + generation + semantic
  // coordinates + kind/tool + provider run + blob ref/digest. Nothing else may
  // ride along as history — HOST-005's exclusion list.
  for (const forbidden of [
    'Usage',
    'Cost',
    'Timestamp',
    'Elapsed',
    'Directory',
    'FinishReason',
    'RuntimeId',
    'Tokens',
    'UiDelta',
  ]) {
    assert.ok(!keys.has(forbidden), `XTrace part ref must not carry ${forbidden}`)
  }

  // Provenance and provider run are the transport identity, kept for proof/locating
  // only — and they must survive the fold so a consumer can locate the run.
  assert.equal(idValue.providerRun(parts[0].ProviderRun), 'msg_p1')
  assert.equal(parts[1].ToolName, 'todowrite')
})

test('SEMANTIC_TRACE_appendable_xtrace_facts_are_exactly_three', () => {
  // CompanionFactFold is the only writer of the XTrace projection. Its apply
  // branches ARE the capture boundary: if a speculative/candidate fact family
  // could reach the trace, it would appear here as a fourth apply site.
  const source = readFileSync(
    new URL('../../../src/Wanxiangshu/Journal/CompanionFactFold.fs', import.meta.url),
    'utf8',
  )

  const applied = [...source.matchAll(/XTraceProjection\.apply\w+/g)].map((m) => m[0]).sort()
  assert.deepEqual(
    applied,
    ['XTraceProjection.applyOpening', 'XTraceProjection.applyPart', 'XTraceProjection.applyTerminal'].sort(),
    'exactly the three capture facts may write the XTrace',
  )

  // SEMANTIC-TRACE-008: no speculative fact family may exist for the trace.
  for (const forbidden of ['StrengthCandidatePrepared', 'StrengthCandidatePromoted', 'PrefixProbeRolledBack', 'CandidateFrameAppended']) {
    assert.ok(!source.includes(forbidden), `forbidden speculative fact family ${forbidden} in XTrace fold`)
  }
})

test('SEMANTIC_TRACE_unknown_or_speculative_facts_leave_xtrace_untouched', () => {
  // A folded fact that is not one of the three capture facts must not create or
  // mutate XTrace state. Fold an unrelated Companion/Context fact (opening link
  // machinery) and assert the trace stays empty.
  const result = fold.apply(
    fold.empty,
    [
      next(
        fact('CompanionBloggerLinked', {
          SessionId: session,
          BloggerSessionId: sessionId('ses_other'),
        }),
        'msg_link',
      ),
    ],
  )
  assert.equal(result.ok, true)
  const s = fold.session(result.value, SESSION)
  assert.equal(s.XTrace, undefined, 'no XTrace state may be created by a non-capture fact')
})
