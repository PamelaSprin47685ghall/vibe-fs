// PERSIST-010 — XTrace 持久化 facts 的 fold 规则。
//
// OpeningPromptCaptured / XTracePartAppended / TerminalOutputCaptured 三个事实
// 由 Fold 维护 XTraceProjection：opening 幂等不可覆盖、part 严格单调 append-only、
// terminal 幂等不可覆盖（COMPANION-003、HOST-005、PERSIST-010）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { envelope, fact, fold, sessionId, stream, providerRun, blobRef, blobDigest, listItems } from '../support/domain.mjs'

const SESSION = 'ses_x'
const session = sessionId(SESSION)

let seq = 0
const next = (factValue, run) => envelope({ seq: (seq += 1), stream: stream.session(session), run, fact: factValue })

const openingFact = ({ assignment = 'first task', requirements = [], run = 'msg_o1' } = {}) =>
  next(
    fact('OpeningPromptCaptured', {
      SessionId: session,
      AssignmentText: assignment,
      AuthoritativeRequirements: requirements,
      ProviderRun: providerRun(run),
    }),
    run,
  )

const partFact = ({ sequence, role = 'user', turn = 0, partIndex = 0, kind = 'text', toolName = undefined, ref = `blob-p${sequence}`, digest = `sha-p${sequence}`, run = `msg_p${sequence}` } = {}) =>
  next(
    fact('XTracePartAppended', {
      SessionId: session,
      CursorSequence: BigInt(sequence),
      Role: role,
      Turn: turn,
      PartIndex: partIndex,
      Kind: kind,
      ToolName: toolName,
      TextRef: blobRef(ref),
      TextDigest: blobDigest(digest),
      Provenance: `turn:${turn}/part:${partIndex}`,
      ProviderRun: providerRun(run),
    }),
    run,
  )

const terminalFact = ({ ref = 'blob-term', digest = 'sha-term', run = 'msg_term' } = {}) =>
  next(
    fact('TerminalOutputCaptured', {
      SessionId: session,
      TextRef: blobRef(ref),
      TextDigest: blobDigest(digest),
      ProviderRun: providerRun(run),
    }),
    run,
  )

const foldOk = (envelopes) => {
  const result = fold.apply(fold.empty, envelopes)
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return fold.session(result.value, SESSION)
}

test('PERSIST_010_opening_is_captured_verbatim_and_idempotent', () => {
  const s = foldOk([openingFact(), openingFact()])
  assert.equal(s.XTrace.Opening.AssignmentText, 'first task')
  assert.deepEqual(s.XTrace.Opening.AuthoritativeRequirements, [])
})

test('PERSIST_010_a_different_opening_is_refused', () => {
  const result = fold.apply(fold.empty, [openingFact(), openingFact({ assignment: 'second task' })])
  assert.equal(result.ok, false)
  assert.equal(result.error.Fact, 'OpeningPromptCaptured')
  assert.match(result.error.Reason, /already captured with different text/)
})

test('PERSIST_010_opening_preserves_authoritative_requirement_order', () => {
  const s = foldOk([openingFact({ requirements: ['r1', 'r2', 'r3'] })])
  assert.deepEqual(s.XTrace.Opening.AuthoritativeRequirements, ['r1', 'r2', 'r3'])
})

test('PERSIST_010_parts_append_in_strict_cursor_order', () => {
  const s = foldOk([
    partFact({ sequence: 1, turn: 0, partIndex: 0 }),
    partFact({ sequence: 2, turn: 0, partIndex: 1 }),
    partFact({ sequence: 3, turn: 1, partIndex: 0 }),
  ])

  const parts = listItems(s.XTrace.Parts)
  assert.equal(parts.length, 3)
  assert.deepEqual(
    parts.map((part) => Number(part.Cursor.Sequence)),
    [1, 2, 3],
  )
  assert.deepEqual(
    parts.map((part) => part.Kind),
    ['text', 'text', 'text'],
  )
})

test('PERSIST_010_a_duplicate_cursor_is_refused', () => {
  const result = fold.apply(fold.empty, [partFact({ sequence: 1 }), partFact({ sequence: 1 })])
  assert.equal(result.ok, false)
  assert.equal(result.error.Fact, 'XTracePartAppended')
  assert.match(result.error.Reason, /cursor 1 is not after the head 1/)
})

test('PERSIST_010_a_retreating_cursor_is_refused', () => {
  const result = fold.apply(fold.empty, [partFact({ sequence: 5 }), partFact({ sequence: 3 })])
  assert.equal(result.ok, false)
  assert.equal(result.error.Fact, 'XTracePartAppended')
  assert.match(result.error.Reason, /cursor 3 is not after the head 5/)
})

test('PERSIST_010_parts_carry_turn_part_and_tool_name', () => {
  const s = foldOk([
    partFact({ sequence: 1, kind: 'tool_call', toolName: 'read', turn: 2, partIndex: 3 }),
  ])

  const parts = listItems(s.XTrace.Parts)
  assert.equal(parts[0].ToolName, 'read')
  assert.equal(parts[0].Turn, 2)
  assert.equal(parts[0].PartIndex, 3)
  assert.equal(parts[0].Role, 'user')
})

test('PERSIST_010_terminal_is_captured_once_and_idempotent', () => {
  const s = foldOk([terminalFact(), terminalFact()])
  assert.deepEqual(s.XTrace.Terminal[0].fields[0], 'blob-term')
})

test('PERSIST_010_a_second_different_terminal_overwrites_for_reuse', () => {
  const result = fold.apply(fold.empty, [terminalFact(), terminalFact({ ref: 'blob-term-2', digest: 'sha-term-2' })])
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  const s = fold.session(result.value, SESSION)
  // EXEC-009: reuse overwrites the private terminal marker per work unit.
  // Terminal is not an LWR section; last assistant text lives in Recent work.
  assert.deepEqual(s.XTrace.Terminal[0].fields[0], 'blob-term-2')
})

test('PERSIST_010_xtrace_facts_survive_NDJSON_and_still_fold', () => {
  const result = fold.replay([
    openingFact(),
    partFact({ sequence: 1 }),
    partFact({ sequence: 2, kind: 'reasoning' }),
    terminalFact(),
  ])

  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  const s = fold.session(result.value, SESSION)

  assert.equal(s.XTrace.Opening.AssignmentText, 'first task')
  const parts = listItems(s.XTrace.Parts)
  assert.equal(parts.length, 2)
  assert.equal(parts[1].Kind, 'reasoning')
  assert.deepEqual(s.XTrace.Terminal[0].fields[0], 'blob-term')
})
