// Split from tests/unit/codec/join-result-renderer.test.mjs (cutover Wave 2a); owner: participant-horizon.
// JoinResultRenderer — natural language consequences + WorkRecord; no legacy
// DTO plane (PH-004/005: 自然语言后果、不重述 echo、terminal exit_code 保留、
// 无 status 字段)。

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { providerLanguage, toList } from '../../verification-system/tests/support/domain.mjs'

const {
  renderJoinItemBatch,
  renderCompletedBatch,
  renderForkError,
  renderInterrupted,
} = await import('../../../dist/Execution/Delegation/Fork/OpenCode/JoinResultRenderer.js')

const {
  JoinItem,
  AgentJoinItem,
  PtyJoinItem,
  AgentCompletionPayload,
  AgentFailurePayload,
  PtyExit,
  PtyFailure,
  PtyAbort,
  RunCompletion,
  AgentCompletionOutcome,
} = await import('../../../dist/Execution/Session/AgentCompletion.js')

const { ForkError } = await import('../../../dist/Execution/Delegation/Fork/Model.js')
const { JoinInterruptReason } = await import('../../../dist/Execution/Session/Wait/CompletionMailbox.js')
const { NonEmptyBatch_ofHeadTail: batchOf } = await import('../../../dist/Execution/Session/Wait/CompletionMailbox.js')
const { Role } = await import('../../../dist/Foundation/Roles.js')

const lang = providerLanguage.english

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]/

const completedPayload = (over = {}) =>
  new AgentCompletionPayload('a1', undefined, 'run-1', Role.Coder, undefined, undefined, over.workRecord ?? '', undefined)
const failedPayload = (over = {}) =>
  new AgentFailurePayload('a1', undefined, 'run-1', Role.Coder, over.code ?? 'E1', over.message ?? 'boom')

const agentItem = (item) => new JoinItem(0, [item])
const ptyItem = (item) => new JoinItem(1, [item])

test('MISC_join_render_batch_agent_completed_natural_language_and_work_record', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(0, [completedPayload({ workRecord: 'did the thing' })])), toList([]))
  const wire = renderJoinItemBatch(lang, () => 'fast-coder', batch)
  assert.match(wire, /# fast-coder has returned\./)
  assert.match(wire, /# did the thing/)
  assert.ok(!LEGACY_DTO.test(wire))
  assert.ok(!wire.includes('work_record ='))
})

test('MISC_join_render_batch_agent_failed_natural_language_consequence', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(1, [failedPayload({ message: 'no' })])), toList([]))
  const wire = renderJoinItemBatch(lang, () => 'deep-reviewer', batch)
  assert.match(wire, /# deep-reviewer could not complete the charge\./)
  assert.match(wire, /# no/)
  assert.ok(!LEGACY_DTO.test(wire))
})

test('MISC_join_render_batch_agent_abandoned_natural_language', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(2, ['a1', 'operator abort'])), toList([]))
  const wire = renderJoinItemBatch(lang, () => 'deep-reviewer', batch)
  assert.match(wire, /# deep-reviewer did not return from this charge\./)
  assert.ok(!LEGACY_DTO.test(wire))
})

test('MISC_join_render_batch_pty_exited_failed_aborted', () => {
  const terminal = (ptyId) => (id) => (id === ptyId ? 'npm test' : 'Terminal')

  const exited = renderJoinItemBatch(lang, () => '', batchOf(ptyItem(new PtyJoinItem(0, [new PtyExit('pty-1', 'exit 0', true)])), toList([])), terminal('pty-1'))
  assert.match(exited, /# npm test has ended\./)
  assert.match(exited, /exit_code = 0/)
  assert.ok(!exited.includes('pty_id'))

  const failed = renderJoinItemBatch(lang, () => '', batchOf(ptyItem(new PtyJoinItem(1, [new PtyFailure('pty-2', 'crash', false, 'RC', 'kaboom')])), toList([])), terminal('pty-2'))
  assert.match(failed, /# npm test has ended\./)
  assert.match(failed, /output = "kaboom"/)
  assert.ok(!failed.includes('code ='))

  const aborted = renderJoinItemBatch(lang, () => '', batchOf(ptyItem(new PtyJoinItem(2, [new PtyAbort('pty-3', 'interrupted', false, 'AB', 'esc')])), toList([])), terminal('pty-3'))
  assert.match(aborted, /# npm test was interrupted\./)
  assert.match(aborted, /output = "esc"/)
  assert.ok(!aborted.includes('pty_id'))
})

test('MISC_join_render_batch_multiple_items_stable_order', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(1, [failedPayload()])), toList([
    ptyItem(new PtyJoinItem(2, [new PtyAbort('p', 'x', false, 'C', 'm')])),
    agentItem(new AgentJoinItem(0, [completedPayload()])),
  ]))
  const wire = renderJoinItemBatch(lang, () => 'coder', batch, () => 'Terminal')
  assert.equal([...wire.matchAll(/could not complete/g)].length, 1)
  assert.equal([...wire.matchAll(/has returned\./g)].length, 1)
  assert.equal([...wire.matchAll(/was interrupted\./g)].length, 1)
  assert.ok(!LEGACY_DTO.test(wire))
})

test('MISC_join_render_interrupted_natural_language', () => {
  const operatorWire = renderInterrupted(lang, JoinInterruptReason.OperatorAbort)
  assert.match(operatorWire, /# Your waiting was interrupted\./)
  assert.ok(!LEGACY_DTO.test(operatorWire))

  const userWire = renderInterrupted(lang, JoinInterruptReason.UserMessageArrived)
  assert.match(userWire, /# Something nearer has arrived\./)
  assert.ok(!LEGACY_DTO.test(userWire))

  const deadlineWire = renderInterrupted(lang, JoinInterruptReason.DeadlineExpired)
  assert.match(deadlineWire, /# No return reached you before your waiting ended\./)
  assert.ok(!LEGACY_DTO.test(deadlineWire))
})

test('MISC_join_render_fork_error_natural_language', () => {
  const cases = [
    [new ForkError(0, []), /nothing away to receive/],
    [new ForkError(1, []), /nothing away to receive/],
    [new ForkError(2, []), /wait was cancelled/],
    [new ForkError(3, []), /already in progress/],
    [new ForkError(4, ['h1', 'gave up']), /did not return from this charge/],
    [new ForkError(5, ['h2']), /No one by that name is away/],
    [new ForkError(6, []), /waiting ended/],
    [new ForkError(7, ['h3']), /return could not be gathered/],
  ]
  for (const [error, pattern] of cases) {
    const wire = renderForkError(lang, error, () => 'deep-reviewer')
    assert.match(wire, pattern, String(error))
    assert.ok(!LEGACY_DTO.test(wire), String(error))
    assert.equal(parseToml(wire).status, undefined, String(error))
  }
})

test('MISC_join_render_completed_managed_agent_name_and_raw_resolve', () => {
  const completion = (agentName) =>
    new RunCompletion('run-1', agentName, Role.Coder, new AgentCompletionOutcome(0, [completedPayload()]), new Date())
  const viaName = renderCompletedBatch(lang, () => false, () => '', batchOf(completion('fast-coder'), toList([])))
  assert.match(viaName, /# fast-coder has returned\./)

  const viaResolve = renderCompletedBatch(lang, () => false, () => 'deep-inspector', batchOf(completion(''), toList([])))
  assert.match(viaResolve, /# deep-inspector has returned\./)

  const rawResolve = renderCompletedBatch(lang, () => false, () => 'weird raw name', batchOf(completion(''), toList([])))
  assert.match(rawResolve, /# weird raw name has returned\./)
})

test('MISC_join_render_completed_pty_aborted_round_trip', () => {
  const run = new RunCompletion('pty-9', '', Role.Coder, new AgentCompletionOutcome(0, [completedPayload()]), new Date())
  const wire = renderCompletedBatch(lang, (runId) => runId === 'pty-9', () => '', batchOf(run, toList([])), (id) => (id === 'pty-9' ? 'shell' : 'Terminal'))
  assert.match(wire, /# shell has ended\./)
  assert.ok(!wire.includes('pty_id'))
})
