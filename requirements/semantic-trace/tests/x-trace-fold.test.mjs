// PERSIST-010 — XTrace durable fold rules.
//
// OpeningPromptCaptured / XTracePartAppended / TerminalOutputCaptured are the
// three facts that maintain XTraceProjection: opening is idempotent, parts are
// strictly append-only, and terminal capture is idempotent per blob.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as xTrace from '../../../dist/Context/Trace/XTraceSurface.js'

const SESSION = 'ses_x'

let seq = 0
const next = (factValue, run) =>
  xTrace.envelope({ seq: (seq += 1), session: SESSION, run, fact: factValue })

const openingFact = ({ assignment = 'first task', requirements = [], run = 'msg_o1' } = {}) =>
  next(
    xTrace.fact('OpeningPromptCaptured', {
      sessionId: SESSION,
      assignmentText: assignment,
      authoritativeRequirements: requirements,
      providerRun: run,
    }),
    run,
  )

const partFact = ({
  sequence,
  role = 'user',
  turn = 0,
  partIndex = 0,
  kind = 'text',
  toolName,
  ref = `blob-p${sequence}`,
  digest = `sha-p${sequence}`,
  provenance = `turn:${turn}/part:${partIndex}`,
  run = `msg_p${sequence}`,
} = {}) =>
  next(
    xTrace.fact('XTracePartAppended', {
      sessionId: SESSION,
      sequence,
      role,
      turn,
      partIndex,
      kind,
      toolName,
      textRef: ref,
      textDigest: digest,
      provenance,
      providerRun: run,
    }),
    run,
  )

const terminalFact = ({ ref = 'blob-term', digest = 'sha-term', run = 'msg_term' } = {}) =>
  next(
    xTrace.fact('TerminalOutputCaptured', {
      sessionId: SESSION,
      textRef: ref,
      textDigest: digest,
      providerRun: run,
    }),
    run,
  )

const foldOk = (envelopes) => {
  const result = xTrace.fold(envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return xTrace.session(result.value, SESSION)
}

test('WHAT[SEMANTIC-TRACE-001] PERSIST_010_opening_is_captured_verbatim_and_idempotent', () => {
  const s = foldOk([openingFact(), openingFact()])
  assert.equal(s.xTrace.opening.assignmentText, 'first task')
  assert.deepEqual(s.xTrace.opening.authoritativeRequirements, [])
})

test('WHAT[SEMANTIC-TRACE-010] PERSIST_010_a_different_opening_is_refused', () => {
  const result = xTrace.fold([openingFact(), openingFact({ assignment: 'second task' })])
  assert.equal(result.ok, false)
  assert.equal(result.error.Fact, 'OpeningPromptCaptured')
  assert.match(result.error.Reason, /already captured with different text/)
})

test('WHAT[SEMANTIC-TRACE-001] PERSIST_010_opening_preserves_authoritative_requirement_order', () => {
  const s = foldOk([openingFact({ requirements: ['r1', 'r2', 'r3'] })])
  assert.deepEqual(s.xTrace.opening.authoritativeRequirements, ['r1', 'r2', 'r3'])
})

test('WHAT[SEMANTIC-TRACE-001] PERSIST_010_parts_append_in_strict_cursor_order', () => {
  const s = foldOk([
    partFact({ sequence: 1, turn: 0, partIndex: 0 }),
    partFact({ sequence: 2, turn: 0, partIndex: 1 }),
    partFact({ sequence: 3, turn: 1, partIndex: 0 }),
  ])

  const parts = xTrace.parts(s.xTrace)
  assert.equal(parts.length, 3)
  assert.deepEqual(
    parts.map((part) => part.cursor.sequence),
    [1, 2, 3],
  )
  assert.deepEqual(
    parts.map((part) => part.kind),
    ['text', 'text', 'text'],
  )
})

test('WHAT[SEMANTIC-TRACE-003] PERSIST_010_a_duplicate_cursor_is_refused', () => {
  const result = xTrace.fold([partFact({ sequence: 1 }), partFact({ sequence: 1 })])
  assert.equal(result.ok, false)
  assert.equal(result.error.Fact, 'XTracePartAppended')
  assert.match(result.error.Reason, /cursor 1 is not after the head 1/)
})

test('WHAT[SEMANTIC-TRACE-003] PERSIST_010_a_retreating_cursor_is_refused', () => {
  const result = xTrace.fold([partFact({ sequence: 5 }), partFact({ sequence: 3 })])
  assert.equal(result.ok, false)
  assert.equal(result.error.Fact, 'XTracePartAppended')
  assert.match(result.error.Reason, /cursor 3 is not after the head 5/)
})

test('WHAT[SEMANTIC-TRACE-002] PERSIST_010_parts_carry_turn_part_and_tool_name', () => {
  const s = foldOk([
    partFact({ sequence: 1, kind: 'tool_call', toolName: 'read', turn: 2, partIndex: 3 }),
  ])

  const parts = xTrace.parts(s.xTrace)
  assert.equal(parts[0].toolName, 'read')
  assert.equal(parts[0].turn, 2)
  assert.equal(parts[0].partIndex, 3)
  assert.equal(parts[0].role, 'user')
})

test('WHAT[SEMANTIC-TRACE-001] PERSIST_010_terminal_is_captured_once_and_idempotent', () => {
  const s = foldOk([terminalFact(), terminalFact()])
  assert.equal(s.xTrace.terminal.textRef, 'blob-term')
})

test('WHAT[SEMANTIC-TRACE-001] PERSIST_010_a_second_different_terminal_overwrites_for_reuse', () => {
  const result = xTrace.fold([terminalFact(), terminalFact({ ref: 'blob-term-2', digest: 'sha-term-2' })])
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  const s = xTrace.session(result.value, SESSION)
  assert.equal(s.xTrace.terminal.textRef, 'blob-term-2')
})

test('WHAT[SEMANTIC-TRACE-001] PERSIST_010_xtrace_facts_survive_NDJSON_and_still_fold', () => {
  const result = xTrace.replay([
    openingFact(),
    partFact({ sequence: 1 }),
    partFact({ sequence: 2, kind: 'reasoning' }),
    terminalFact(),
  ])

  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  const s = xTrace.session(result.value, SESSION)

  assert.equal(s.xTrace.opening.assignmentText, 'first task')
  const parts = xTrace.parts(s.xTrace)
  assert.equal(parts.length, 2)
  assert.equal(parts[1].kind, 'reasoning')
  assert.equal(s.xTrace.terminal.textRef, 'blob-term')
})
