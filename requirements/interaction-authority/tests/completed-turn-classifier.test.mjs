// RECON_: CompletedTurnClassifier — pure turn classification decision table.
// Every classifyOutcome finish/error branch, needsInteractionRepair role
// support, roleOfAgent fallback, and buildTurn assembly.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  authorityRoot,
  caseOf,
  payloadOf,
  physicalUser,
  providerRun,
  roles,
  sessionId,
  idValue,
  xTraceCapture,
} from '../../verification-system/tests/support/domain.mjs'
import {
  buildTurn,
  classifyOutcome,
  hasToolCallPart,
  isAbortErrorName,
  needsInteractionRepair,
  partsSessionText,
  partsText,
  roleOfAgent,
} from '../../../dist/Interaction/Repair/CompletedTurn.js'
import { SessionMessage } from '../../../dist/OpenCode/Host/SessionSnapshotPort.js'

const text = (value) => xTraceCapture.text(value)
const reasoning = (value) => xTraceCapture.reasoning(value)
const toolCall = (id, name, args) => xTraceCapture.toolCall(id, name, args)
const toolResult = (id, result) => xTraceCapture.toolResult(id, result)
const activity = (kind) => xTraceCapture.activity(kind)

const assistant = ({
  id = 'asst-1',
  agent = undefined,
  finish = undefined,
  errorName = undefined,
  completed = false,
  parts = [],
  model = undefined,
  parentId = undefined,
} = {}) =>
  new SessionMessage(id, 'assistant', agent, finish, errorName, model, parentId, completed, false, undefined, parts)

// ── partsText / partsSessionText / hasToolCallPart / isAbortErrorName ──────

test('RECON_partsText_null_and_mixed_parts_keep_formal_text_only', () => {
  assert.equal(partsText(null), '')
  assert.equal(partsText(undefined), '')
  assert.equal(partsText([]), '')
  assert.equal(
    partsText([text('a'), toolCall('c1', 'read', '{}'), text('b'), reasoning('think'), toolResult('c1', 'out')]),
    'ab',
    'only Text parts contribute to formal text',
  )
})

test('RECON_partsSessionText_joins_text_and_reasoning_drops_tools', () => {
  assert.equal(partsSessionText(null), '')
  assert.equal(partsSessionText([]), '')
  assert.equal(
    partsSessionText([text('formal'), reasoning('visible thinking'), toolCall('c1', 'read', '{}'), toolResult('c1', 'raw')]),
    'formal\n\nvisible thinking',
    'terminal material = text + reasoning, raw tool parts excluded (COMPANION-003)',
  )
  assert.equal(partsSessionText([toolCall('c1', 'exec', '{}')]), '')
})

test('RECON_hasToolCallPart_detects_calls_and_patch_step_activities', () => {
  assert.equal(hasToolCallPart(null), false)
  assert.equal(hasToolCallPart([]), false)
  assert.equal(hasToolCallPart([text('prose')]), false)
  assert.equal(hasToolCallPart([toolCall('c1', 'read', '{}')]), true)
  assert.equal(hasToolCallPart([activity('patch')]), true)
  assert.equal(hasToolCallPart([activity('step-start')]), true)
  assert.equal(hasToolCallPart([activity('step-finish')]), true)
  assert.equal(hasToolCallPart([activity('reasoning')]), false, 'other activity kinds are bookkeeping, not tool calls')
})

test('RECON_isAbortErrorName_matches_case_insensitive_abort_substring', () => {
  assert.equal(isAbortErrorName(undefined), false)
  assert.equal(isAbortErrorName('AbortError'), true)
  assert.equal(isAbortErrorName('ABORTED'), true)
  assert.equal(isAbortErrorName('user abort requested'), true)
  assert.equal(isAbortErrorName('RateLimitError'), false)
})

// ── classifyOutcome decision table ─────────────────────────────────────────

test('RECON_classify_abort_error_name_wins_over_everything', () => {
  const outcome = classifyOutcome(true, 'stop', 'AbortError', [text('done')])
  assert.equal(caseOf(outcome), 'TurnAborted')
  assert.equal(payloadOf(outcome), 'AbortError')
})

test('RECON_classify_completed_with_error_is_failed', () => {
  const outcome = classifyOutcome(true, undefined, 'StreamDied', [])
  assert.equal(caseOf(outcome), 'TurnFailed')
  assert.equal(payloadOf(outcome), 'StreamDied')
})

test('RECON_classify_finish_aborted_is_aborted_regardless_of_case', () => {
  for (const finish of ['aborted', 'Aborted', 'ABORTED']) {
    const outcome = classifyOutcome(false, finish, undefined, [text('partial')])
    assert.equal(caseOf(outcome), 'TurnAborted', finish)
    assert.equal(payloadOf(outcome), 'finish=aborted')
  }
})

test('RECON_classify_finish_error_uses_error_name_or_default', () => {
  const named = classifyOutcome(false, 'error', 'ProviderBoom', [])
  assert.equal(caseOf(named), 'TurnFailed')
  assert.equal(payloadOf(named), 'ProviderBoom')

  const anonymous = classifyOutcome(false, 'error', undefined, [])
  assert.equal(caseOf(anonymous), 'TurnFailed')
  assert.equal(payloadOf(anonymous), 'assistant finish=error')

  const abortNamed = classifyOutcome(false, 'Error', 'AbortError', [])
  assert.equal(caseOf(abortNamed), 'TurnAborted', 'finish=error with abort name stays an abort')
})

test('RECON_classify_stop_requires_valid_formal_text', () => {
  const valid = classifyOutcome(false, 'stop', undefined, [text('the answer')])
  assert.equal(caseOf(valid), 'TurnCompleted')

  const empty = classifyOutcome(false, 'stop', undefined, [text('   ')])
  assert.equal(caseOf(empty), 'TurnNeedsContinuation')
  assert.match(String(payloadOf(empty)), /empty terminal/)

  const xmlOnly = classifyOutcome(false, 'stop', undefined, [text('<tool_call>read</tool_call>')])
  assert.equal(caseOf(xmlOnly), 'TurnNeedsContinuation')
  assert.match(String(payloadOf(xmlOnly)), /XML-only terminal/)

  const reasoningOnly = classifyOutcome(false, 'stop', undefined, [reasoning('thoughts but no answer')])
  assert.equal(caseOf(reasoningOnly), 'TurnNeedsContinuation', 'reasoning is not formal text (CTX-004)')
})

test('RECON_classify_tool_calls_is_in_progress', () => {
  const outcome = classifyOutcome(false, 'tool-calls', undefined, [toolCall('c1', 'exec', '{}')])
  assert.equal(caseOf(outcome), 'TurnInProgress')
  assert.equal(caseOf(classifyOutcome(false, 'Tool-Calls', undefined, [])), 'TurnInProgress')
})

test('RECON_classify_length_is_needs_continuation', () => {
  const outcome = classifyOutcome(false, 'length', undefined, [text('truncated')])
  assert.equal(caseOf(outcome), 'TurnNeedsContinuation')
  assert.equal(payloadOf(outcome), 'assistant finish=length')
})

test('RECON_classify_unknown_finish_is_failed_with_finish_name', () => {
  const outcome = classifyOutcome(false, 'content_filter', undefined, [])
  assert.equal(caseOf(outcome), 'TurnFailed')
  assert.equal(payloadOf(outcome), 'assistant finish=content_filter')
})

test('RECON_classify_no_finish_is_unknown_even_with_parts', async () => {
  const observation = classifyOutcome(false, undefined, undefined, [text('streaming')])
  assert.equal(caseOf(observation), 'TurnUnknown')

  // HOST-004 Clean Break: finish=None is a private SnapshotObservation, not a
  // publishable TurnOutcome case. SnapshotObservation is a single-case DU.
  assert.deepEqual(
    observation.cases(),
    ['TurnUnknown'],
    'finish=None must classify as SnapshotObservation.TurnUnknown, not TurnOutcome',
  )

  const mod = await import(new URL('../../../dist/Composition/Turn/Program.js', import.meta.url).pathname)
  assert.equal(typeof mod.SnapshotObservation, 'function')
  assert.ok(
    observation instanceof mod.SnapshotObservation,
    'finish=None classification must be SnapshotObservation, not TurnOutcome',
  )
  assert.equal(
    observation instanceof mod.TurnOutcome,
    false,
    'finish=None must not produce TurnOutcome.TurnUnknown',
  )
})

// ── needsInteractionRepair ─────────────────────────────────────────────────

test('RECON_needs_interactionRepair_role_by_outcome_table', () => {
  const inProgress = classifyOutcome(false, 'tool-calls', undefined, [])
  const inProgressWithRealTool = classifyOutcome(false, 'tool-calls', undefined, [toolCall('c-live', 'write', '{}')])
  const needsMore = classifyOutcome(false, 'length', undefined, [])
  const completed = classifyOutcome(false, 'stop', undefined, [text('ok')])
  const aborted = classifyOutcome(false, 'aborted', undefined, [])
  const failed = classifyOutcome(false, 'error', 'boom', [])
  const unknown = classifyOutcome(false, undefined, undefined, [])

  for (const role of ['Manager', 'Orchestrator', 'Coder', 'Reviewer', 'Inspector', 'DevOps', 'Browser', 'Inquiry']) {
    assert.equal(needsInteractionRepair(roles.of(role), inProgress, []), true, `${role} InProgress without a real tool part repairs`)
    assert.equal(
      needsInteractionRepair(roles.of(role), inProgressWithRealTool, [toolCall('c-live', 'write', '{}')]),
      false,
      `${role} normal tool-call continuation must stay on the Host provider/tool lane`,
    )
    assert.equal(needsInteractionRepair(roles.of(role), needsMore, []), true, `${role} NeedsContinuation`)
    for (const [label, outcome] of [['Completed', completed], ['Aborted', aborted], ['Failed', failed], ['Unknown', unknown]]) {
      assert.equal(needsInteractionRepair(roles.of(role), outcome, []), false, `${role} ${label} never repairs`)
    }
  }

  for (const role of ['Distiller', 'Blogger']) {
    assert.equal(needsInteractionRepair(roles.of(role), inProgress, []), false, `${role} has no interaction repair`)
  }
  assert.equal(needsInteractionRepair(undefined, inProgress, []), false, 'no role → no repair')
})

// ── roleOfAgent / buildTurn ────────────────────────────────────────────────

test('RECON_roleOfAgent_prefers_host_agent_name_then_fallback', () => {
  const coder = roles.of('Coder')
  const reviewer = roles.of('Reviewer')
  assert.equal(roleOfAgent(undefined, coder).tag, coder.tag, 'no agent → fallback')
  assert.equal(roleOfAgent('deep-coder', reviewer).tag, coder.tag, 'managed agent name wins over fallback')
  assert.equal(roleOfAgent('not-a-managed-agent', reviewer).tag, reviewer.tag, 'unparseable agent → fallback')
  assert.equal(roleOfAgent('not-a-managed-agent', undefined), undefined, 'unparseable agent and no fallback → no role')
})

test('RECON_buildTurn_assembles_reconciled_turn_from_assistant_message', () => {
  const session = sessionId('ses_build_turn')
  const physical = physicalUser('user-1')
  const root = authorityRoot('user-1')
  const parts = [text('LGTM'), reasoning('checked twice')]
  const message = assistant({
    id: 'asst-9',
    agent: 'fast-reviewer',
    finish: 'stop',
    completed: true,
    parts,
    model: 'model-x',
  })

  const turn = buildTurn(session, physical, root, message, undefined, '/repo/dir')

  assert.equal(idValue.session(turn.SessionId), 'ses_build_turn')
  assert.equal(idValue.providerRun(turn.ProviderRun), 'asst-9', 'HOST-010: assistant message id IS the provider run')
  assert.equal(turn.Role.tag, roles.of('Reviewer').tag, 'role resolved from managed agent name')
  assert.equal(turn.Directory, '/repo/dir')
  assert.equal(caseOf(turn.Outcome), 'TurnCompleted')
  assert.equal(turn.Finish, 'stop')
  assert.equal(turn.Model, 'model-x')
  assert.equal(turn.Parts, parts)
})

test('RECON_buildTurn_without_agent_uses_fallback_role_and_classifies_failure', () => {
  const message = assistant({ id: 'asst-10', finish: 'error', errorName: 'Timeout', completed: true })
  const turn = buildTurn(
    sessionId('ses_build_turn_fail'),
    physicalUser('user-2'),
    authorityRoot('user-2'),
    message,
    roles.of('Coder'),
    undefined,
  )
  assert.equal(turn.Role.tag, roles.of('Coder').tag)
  assert.equal(caseOf(turn.Outcome), 'TurnFailed')
  assert.equal(payloadOf(turn.Outcome), 'Timeout')
  assert.equal(turn.Directory, undefined)
  assert.equal(idValue.providerRun(turn.ProviderRun), 'asst-10')
})

test('RECON_buildTurn_provider_run_identity_uses_message_id', () => {
  const message = assistant({ id: 'asst-11', finish: 'stop', parts: [text('x')] })
  const turn = buildTurn(
    sessionId('ses_build_turn_run'),
    physicalUser('user-3'),
    authorityRoot('user-3'),
    message,
    undefined,
    undefined,
  )
  assert.equal(idValue.providerRun(turn.ProviderRun), idValue.providerRun(providerRun('asst-11')))
})
