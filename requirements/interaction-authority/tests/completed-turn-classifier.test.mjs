// INTERACTION-AUTHORITY proof — completed-turn classification and repair ownership.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as turns from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'

const text = (value) => ({ type: 'text', text: value })
const reasoning = (value) => ({ type: 'reasoning', text: value })
const toolCall = (callID, tool, args) => ({ type: 'tool-call', callID, tool, args })
const toolResult = (callID, result) => ({ type: 'tool-result', callID, result })
const activity = (kind) => ({ type: kind })

// Formal text excludes reasoning, tool calls, and tool results.
test('WHAT[INTERACTION-AUTHORITY-004] RECON_partsText_keeps_formal_text_only', () => {
  assert.equal(turns.partsText(null), '')
  assert.equal(turns.partsText([]), '')
  assert.equal(
    turns.partsText([text('a'), toolCall('c1', 'read', '{}'), text('b'), reasoning('think'), toolResult('c1', 'out')]),
    'ab',
  )
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_partsSessionText_keeps_visible_text_and_reasoning', () => {
  assert.equal(turns.partsSessionText(null), '')
  assert.equal(turns.partsSessionText([]), '')
  assert.equal(
    turns.partsSessionText([text('formal'), reasoning('visible thinking'), toolCall('c1', 'read', '{}'), toolResult('c1', 'raw')]),
    'formal\n\nvisible thinking',
  )
  assert.equal(turns.partsSessionText([toolCall('c1', 'exec', '{}')]), '')
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_tool_activity_detection_is_bounded', () => {
  assert.equal(turns.hasToolCallPart(null), false)
  assert.equal(turns.hasToolCallPart([]), false)
  assert.equal(turns.hasToolCallPart([text('prose')]), false)
  assert.equal(turns.hasToolCallPart([toolCall('c1', 'read', '{}')]), true)
  assert.equal(turns.hasToolCallPart([activity('patch')]), true)
  assert.equal(turns.hasToolCallPart([activity('step-start')]), true)
  assert.equal(turns.hasToolCallPart([activity('step-finish')]), true)
  assert.equal(turns.hasToolCallPart([activity('reasoning')]), false)
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_abort_error_name_is_case_insensitive', () => {
  assert.equal(turns.isAbortErrorName(undefined), false)
  assert.equal(turns.isAbortErrorName('AbortError'), true)
  assert.equal(turns.isAbortErrorName('ABORTED'), true)
  assert.equal(turns.isAbortErrorName('user abort requested'), true)
  assert.equal(turns.isAbortErrorName('RateLimitError'), false)
})

const classify = (completed, finish, errorName, parts = []) => turns.classifyOutcome(completed, finish, errorName, parts)

test('WHAT[INTERACTION-AUTHORITY-004] RECON_classify_abort_error_name_wins', () => {
  assert.deepEqual(classify(true, 'stop', 'AbortError', [text('done')]), { kind: 'TurnAborted', reason: 'AbortError' })
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_classify_completed_error_is_failed', () => {
  assert.deepEqual(classify(true, undefined, 'StreamDied'), { kind: 'TurnFailed', reason: 'StreamDied' })
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_classify_abort_finish_is_case_insensitive', () => {
  for (const finish of ['aborted', 'Aborted', 'ABORTED']) {
    assert.deepEqual(classify(false, finish, undefined, [text('partial')]), { kind: 'TurnAborted', reason: 'finish=aborted' })
  }
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_classify_error_uses_name_or_finish', () => {
  assert.deepEqual(classify(false, 'error', 'ProviderBoom'), { kind: 'TurnFailed', reason: 'ProviderBoom' })
  assert.deepEqual(classify(false, 'error'), { kind: 'TurnFailed', reason: 'assistant finish=error' })
  assert.deepEqual(classify(false, 'Error', 'AbortError'), { kind: 'TurnAborted', reason: 'AbortError' })
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_stop_requires_usable_formal_text', () => {
  assert.equal(classify(false, 'stop', undefined, [text('the answer')]).kind, 'TurnCompleted')
  assert.match(classify(false, 'stop', undefined, [text('   ')]).reason, /empty terminal/)
  assert.match(classify(false, 'stop', undefined, [text('<tool_call>read</tool_call>')]).reason, /XML-only terminal/)
  assert.equal(classify(false, 'stop', undefined, [reasoning('thoughts but no answer')]).kind, 'TurnNeedsContinuation')
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_tool_calls_are_in_progress', () => {
  assert.equal(classify(false, 'tool-calls', undefined, [toolCall('c1', 'exec', '{}')]).kind, 'TurnInProgress')
  assert.equal(classify(false, 'Tool-Calls').kind, 'TurnInProgress')
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_length_and_unknown_finish_need_distinct_results', () => {
  assert.deepEqual(classify(false, 'length', undefined, [text('truncated')]), { kind: 'TurnNeedsContinuation', reason: 'assistant finish=length' })
  assert.deepEqual(classify(false, 'content_filter'), { kind: 'TurnFailed', reason: 'assistant finish=content_filter' })
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_no_finish_is_private_unknown_observation', () => {
  assert.deepEqual(classify(false, undefined, undefined, [text('streaming')]), { kind: 'TurnUnknown', reason: null })
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_repair_role_table_respects_host_tool_work', () => {
  const roles = ['manager', 'orchestrator', 'coder', 'reviewer', 'inspector', 'devops', 'browser', 'inquiry']
  for (const role of roles) {
    assert.equal(turns.needsInteractionRepair(role, false, 'tool-calls', []), true)
    assert.equal(turns.needsInteractionRepair(role, false, 'tool-calls', [toolCall('c-live', 'write', '{}')]), false)
    assert.equal(turns.needsInteractionRepair(role, false, 'length', []), true)
    for (const finish of ['stop', 'aborted', 'error']) {
      assert.equal(turns.needsInteractionRepair(role, false, finish, [text('done')]), false)
    }
  }
  for (const role of ['distiller', 'blogger']) {
    assert.equal(turns.needsInteractionRepair(role, false, 'tool-calls', []), false)
  }
  assert.equal(turns.needsInteractionRepair('', false, 'tool-calls', []), false)
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_roleOfAgent_prefers_managed_agent_then_fallback', () => {
  assert.equal(turns.roleOfAgent(undefined, 'coder'), 'coder')
  assert.equal(turns.roleOfAgent('coder', 'reviewer'), 'coder')
  assert.equal(turns.roleOfAgent('not-a-managed-agent', 'reviewer'), 'reviewer')
  assert.equal(turns.roleOfAgent('not-a-managed-agent', undefined), '')
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_buildTurn_returns_plain_identity_and_outcome', () => {
  const turn = turns.buildTurn(
    'ses_build_turn',
    'user-1',
    'user-1',
    {
      id: 'asst-9',
      role: 'assistant',
      agent: 'reviewer',
      finish: 'stop',
      completed: true,
      parts: [text('LGTM'), reasoning('checked twice')],
      model: { providerID: 'anthropic', modelID: 'model-x' },
    },
    undefined,
    '/repo/dir',
  )
  assert.equal(turn.session, 'ses_build_turn')
  assert.equal(turn.providerRun, 'asst-9')
  assert.equal(turn.role, 'reviewer')
  assert.equal(turn.directory, '/repo/dir')
  assert.equal(turn.outcome, 'TurnCompleted')
  assert.equal(turn.finish, 'stop')
  assert.equal(turn.model.modelID, 'model-x')
  assert.deepEqual(turn.parts, [text('LGTM'), reasoning('checked twice')].map((part) => ({ kind: part.type, text: part.text })))
})

test('WHAT[INTERACTION-AUTHORITY-004] RECON_buildTurn_without_agent_uses_fallback', () => {
  const turn = turns.buildTurn(
    'ses_build_turn_fail',
    'user-2',
    'user-2',
    { id: 'asst-10', finish: 'error', errorName: 'Timeout', completed: true, parts: [] },
    'coder',
    undefined,
  )
  assert.equal(turn.role, 'coder')
  assert.equal(turn.outcome, 'TurnFailed')
  assert.equal(turn.reason, 'Timeout')
  assert.equal(turn.directory, null)
  assert.equal(turn.providerRun, 'asst-10')
})
