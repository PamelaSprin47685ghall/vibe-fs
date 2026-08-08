// JoinResultRenderer uncovered branches: production renderJoinItemBatch over
// JoinItem DUs (abandoned / failed / pty aborted), renderForkError code paths,
// agentName ManagedAgent resolution, completed-batch AgentName paths.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  renderJoinItemBatch,
  renderCompletedBatch,
  renderForkError,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/JoinResultRenderer.js')

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
} = await import('../../../dist/Session/AgentCompletion.js')

const { ForkError } = await import('../../../dist/Session/ForkTypes.js')
const { NonEmptyBatch_ofHeadTail: batchOf } = await import('../../../dist/Session/CompletionMailbox.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')
const { toList } = await import('../support/domain.mjs')

const completedPayload = (over = {}) =>
  new AgentCompletionPayload('a1', undefined, 'run-1', Role.Coder, undefined, undefined, over.workRecord ?? '', undefined)
const failedPayload = (over = {}) =>
  new AgentFailurePayload('a1', undefined, 'run-1', Role.Coder, over.code ?? 'E1', over.message ?? 'boom')

const agentItem = (item) => new JoinItem(0, [item])
const ptyItem = (item) => new JoinItem(1, [item])

test('MISC_join_render_batch_agent_completed_resolves_name_and_work_record', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(0, [completedPayload({ workRecord: 'did the thing' })])), toList([]))
  const wire = renderJoinItemBatch(() => 'fast-coder', batch)
  assert.match(wire, /status = "completed"/)
  assert.match(wire, /count = 1/)
  assert.match(wire, /kind = "agent"/)
  assert.match(wire, /agent = "fast-coder"/)
  assert.match(wire, /# did the thing/)
  assert.match(wire, /\[\[result\]\]/)
  assert.ok(!wire.includes('work_record ='))
})

test('MISC_join_render_batch_agent_failed_flat_code_message', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(1, [failedPayload({ code: 'ERR_X', message: 'no' })])), toList([]))
  const wire = renderJoinItemBatch(() => '', batch)
  assert.match(wire, /kind = "agent"/)
  assert.match(wire, /status = "failed"/)
  assert.match(wire, /agent = "a1"/, 'empty resolve falls back to agent id')
  assert.match(wire, /code = "ERR_X"/)
  assert.match(wire, /message = "no"/)
})

test('MISC_join_render_batch_agent_abandoned_uses_resolved_name', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(2, ['a1', 'operator abort'])), toList([]))
  const wire = renderJoinItemBatch(() => 'deep-reviewer', batch)
  assert.match(wire, /status = "abandoned"/)
  assert.match(wire, /agent = "deep-reviewer"/)
  assert.match(wire, /reason = "operator abort"/)
})

test('MISC_join_render_batch_pty_exited_failed_aborted', () => {
  const exited = renderJoinItemBatch(() => '', batchOf(ptyItem(new PtyJoinItem(0, [new PtyExit('pty-1', 'exit 0', true)])), toList([])))
  assert.match(exited, /kind = "pty"/)
  assert.match(exited, /status = "completed"/)
  assert.match(exited, /outcome = "exit 0"/)
  assert.match(exited, /closed = true/)
  assert.match(exited, /pty_id = "pty-1"/)

  const failed = renderJoinItemBatch(() => '', batchOf(ptyItem(new PtyJoinItem(1, [new PtyFailure('pty-2', 'crash', false, 'RC', 'kaboom')])), toList([])))
  assert.match(failed, /status = "failed"/)
  assert.match(failed, /code = "RC"/)
  assert.match(failed, /message = "kaboom"/)

  const aborted = renderJoinItemBatch(() => '', batchOf(ptyItem(new PtyJoinItem(2, [new PtyAbort('pty-3', 'interrupted', false, 'AB', 'esc')])), toList([])))
  assert.match(aborted, /status = "aborted"/)
  assert.match(aborted, /outcome = "interrupted"/)
  assert.match(aborted, /closed = false/)
  assert.match(aborted, /pty_id = "pty-3"/)
})

test('MISC_join_render_batch_multiple_items_ordinal_stable', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(1, [failedPayload()])), toList([
    ptyItem(new PtyJoinItem(2, [new PtyAbort('p', 'x', false, 'C', 'm')])),
    agentItem(new AgentJoinItem(0, [completedPayload()])),
  ]))
  const wire = renderJoinItemBatch(() => 'coder', batch)
  assert.match(wire, /count = 3/)
  const ordinals = [...wire.matchAll(/ordinal = (\d)/g)].map((m) => m[1])
  assert.deepEqual(ordinals, ['1', '2', '3'])
  assert.equal([...wire.matchAll(/\[\[result\]\]/g)].length, 3)
})

test('MISC_join_render_batch_empty_work_record_no_comment', () => {
  const batch = batchOf(agentItem(new AgentJoinItem(0, [completedPayload({ workRecord: '' })])), toList([]))
  const wire = renderJoinItemBatch(() => 'x', batch)
  assert.ok(!wire.includes('# '))
})

// ── renderForkError ──────────────────────────────────────────────────────────

test('MISC_join_render_fork_error_all_codes', () => {
  const cases = [
    [new ForkError(0, []), 'EMPTY', undefined],
    [new ForkError(1, []), 'NOTHING_TO_JOIN', undefined],
    [new ForkError(2, []), 'CANCELLED', undefined],
    [new ForkError(3, []), 'JOIN_IN_PROGRESS', undefined],
    [new ForkError(4, ['h1', 'gave up']), 'ABANDONED:h1:gave up', 'h1'],
    [new ForkError(5, ['h2']), 'NOT_FOUND:h2', 'h2'],
    [new ForkError(6, []), 'TIMED_OUT', undefined],
    [new ForkError(7, ['h3']), 'TERMINAL_MATERIALIZATION_FAILED:h3', 'h3'],
  ]
  for (const [error, code, agent] of cases) {
    const wire = renderForkError(error)
    assert.match(wire, /status = "failed"/, code)
    assert.match(wire, new RegExp(`code = "${code}"`), code)
    assert.match(wire, /\[error\]/, code)
    if (agent) {
      assert.match(wire, new RegExp(`agent = "${agent}"`), code)
    } else {
      assert.ok(!/^agent =/m.test(wire), `${code} must not carry an agent field`)
    }
  }
})

// ── renderCompletedBatch AgentName / resolve paths ───────────────────────────

test('MISC_join_render_completed_managed_agent_name_and_raw_resolve', () => {
  const completion = (agentName) =>
    new RunCompletion('run-1', 'a1', agentName, Role.Coder, new AgentCompletionOutcome(0, [completedPayload()]), new Date())
  const viaName = renderCompletedBatch(() => false, () => '', batchOf(completion('fast-coder'), toList([])))
  assert.match(viaName, /agent = "fast-coder"/)

  const viaResolve = renderCompletedBatch(() => false, () => 'deep-inspector', batchOf(completion(''), toList([])))
  assert.match(viaResolve, /agent = "deep-inspector"/)

  const rawResolve = renderCompletedBatch(() => false, () => 'weird raw name', batchOf(completion(''), toList([])))
  assert.match(rawResolve, /agent = "weird raw name"/)
})

test('MISC_join_render_completed_pty_aborted_round_trip', () => {
  // Pty join items never round-trip through RunCompletion in the production
  // batch path; renderCompletedBatch with a pty run id uses PtyJoinItem.
  const run = new RunCompletion('pty-9', 'pty-9', '', Role.Coder, new AgentCompletionOutcome(0, [completedPayload()]), new Date())
  const wire = renderCompletedBatch((runId) => runId === 'pty-9', () => '', batchOf(run, toList([])))
  assert.match(wire, /kind = "pty"/)
  assert.match(wire, /status = "completed"/)
  assert.match(wire, /pty_id = "pty-9"/)
})
