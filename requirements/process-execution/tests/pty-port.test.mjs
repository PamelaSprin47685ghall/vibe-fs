// tests/unit/process/pty-port.test.mjs — PtyPort lifecycle boundary: Fork,
// Exists/Known, Send dispatch + abort marking, parked Read plumbing,
// Complete/CompleteAborted outcome codecs, Close/CloseAll escalation, List.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf, payloadOf, resultOf, okResult, errorResult } from '../../verification-system/tests/support/domain.mjs'

const {
  PtyPort,
  PtyPort__AddMailboxSender_15902874,
  PtyPort__get_MailboxSender,
  PtyPort__get_Handler,
  PtyPort__get_AgentProvider,
  PtyPort__Fork_515E235E,
  PtyPort__Exists_Z33F80F6F,
  PtyPort__Known_Z33F80F6F,
  PtyPort__Send_Z13021A56,
  PtyPort__Read_Z33F80F6F,
  PtyPort__ReadResult_3DD67D20,
  PtyPort__FailRead_Z3F1A8176,
  PtyPort__RegisterExitTask_3971C262,
  PtyPort__Complete_3BA7AC67,
  PtyPort__CompleteAborted_20FBD4C9,
  PtyPort__Close_3BA7AC67,
  PtyPort__CloseAll_71136F3F,
  PtyPort__List,
} = await import('../../../dist/Process/Pty.js')

const { PtyId_Create_Z721C83C5, PtyId__get_Value, PtyCommand, PtySignal } = await import(
  '../../../dist/Process/PtyTypes.js'
)

const id = (v) => PtyId_Create_Z721C83C5(v)
const agent = { Name: 'fast-distiller' }
const bytes = new TextEncoder().encode('hi')
const write = new PtyCommand(1, [bytes])
const signalOf = (s) => new PtyCommand(3, [s])

// Resolvable task stand-in matching the fable TaskCompletionSource surface.
const tcs = () => {
  let resolve
  const task = new Promise((r) => {
    resolve = r
  })
  return { get_Task: () => task, SetResult: (v) => resolve(v) }
}

const forkDefault = (p, pidValue, command = 'echo hi') =>
  PtyPort__Fork_515E235E(p, command, agent, pidValue ? id(pidValue) : undefined)

// ── constructor ──────────────────────────────────────────────────────────────

test('PORT_ctor_defaults_are_safe_and_functional', async () => {
  const p = new PtyPort()
  assert.equal(PtyPort__get_MailboxSender(p), undefined)
  assert.equal([...PtyPort__get_AgentProvider(p)()].length, 0)
  const handler = PtyPort__get_Handler(p)
  const r = resultOf(await handler(id('pty-x'))(write))
  assert.equal(r.ok, true)
})

test('PORT_ctor_keeps_supplied_sender_handler_and_agent_provider', async () => {
  const sender = () => {}
  const handler = async () => okResult(undefined)
  const provider = () => [agent]
  const p = new PtyPort(sender, handler, provider)
  assert.equal(PtyPort__get_MailboxSender(p), sender)
  assert.deepEqual(PtyPort__get_AgentProvider(p)(), [agent])
  const r = resultOf(await PtyPort__get_Handler(p)(id('pty-x'))(write))
  assert.equal(r.ok, true)
})

test('PORT_AddMailboxSender_reaches_every_registered_sender', () => {
  const p = new PtyPort()
  const got = []
  PtyPort__AddMailboxSender_15902874(p, (item) => got.push(caseOf(item)))
  PtyPort__AddMailboxSender_15902874(p, (item) => got.push(caseOf(item)))
  forkDefault(p, 'pty-s1')
  PtyPort__Complete_3BA7AC67(p, id('pty-s1'), okResult('done'))
  assert.deepEqual(got, ['PtyExited', 'PtyExited'])
})

// ── Fork ─────────────────────────────────────────────────────────────────────

test('PORT_fork_generates_pty_id_and_dispatches_spawn', () => {
  const seen = []
  const p = new PtyPort(undefined, async (pid, cmd) => {
    seen.push([PtyId__get_Value(pid), cmd.tag, cmd.fields])
    return okResult(undefined)
  })
  const pid = forkDefault(p, undefined, 'sleep 1')
  const value = PtyId__get_Value(pid)
  assert.match(value, /^pty-[0-9a-f]{8}$/)
  assert.deepEqual(seen, [[value, 0, ['sleep 1', '']]])
  assert.equal(PtyPort__Exists_Z33F80F6F(p, pid), true)
})

test('PORT_fork_honors_explicit_id_and_cwd', () => {
  const seen = []
  const p = new PtyPort(undefined, async (pid, cmd) => {
    seen.push([PtyId__get_Value(pid), cmd.fields])
    return okResult(undefined)
  })
  const pid = PtyPort__Fork_515E235E(p, 'ls -la', agent, id('pty-custom'), '/srv')
  assert.equal(PtyId__get_Value(pid), 'pty-custom')
  assert.deepEqual(seen, [['pty-custom', ['ls -la', '/srv']]])
})

test('PORT_fork_twice_on_same_id_replaces_the_handle', () => {
  const p = new PtyPort()
  forkDefault(p, 'pty-rf', 'first')
  forkDefault(p, 'pty-rf', 'second')
  const [, ptys] = PtyPort__List(p)
  assert.equal([...ptys].length, 1)
  assert.equal([...ptys][0].Command, 'second')
})

// ── Exists / Known ───────────────────────────────────────────────────────────

test('PORT_exists_and_known_track_active_and_closed', () => {
  const p = new PtyPort()
  assert.equal(PtyPort__Exists_Z33F80F6F(p, id('pty-ne')), false)
  assert.equal(PtyPort__Known_Z33F80F6F(p, id('pty-ne')), false)

  const pid = forkDefault(p, 'pty-ek')
  assert.equal(PtyPort__Exists_Z33F80F6F(p, pid), true)
  assert.equal(PtyPort__Known_Z33F80F6F(p, pid), true)

  PtyPort__Complete_3BA7AC67(p, pid, okResult('bye'))
  assert.equal(PtyPort__Exists_Z33F80F6F(p, pid), false)
  assert.equal(PtyPort__Known_Z33F80F6F(p, pid), true, 'completed ids stay known')
})

// ── Send ─────────────────────────────────────────────────────────────────────

test('PORT_send_unknown_and_closed_ids_fail_with_distinct_reasons', async () => {
  const p = new PtyPort()
  const unknown = resultOf(await PtyPort__Send_Z13021A56(p, id('pty-un'), write))
  assert.equal(unknown.ok, false)
  assert.equal(unknown.error, 'Unknown PTY id: pty-un')

  const pid = forkDefault(p, 'pty-cl')
  PtyPort__Complete_3BA7AC67(p, pid, okResult('done'))
  const closed = resultOf(await PtyPort__Send_Z13021A56(p, pid, write))
  assert.equal(closed.ok, false)
  assert.equal(closed.error, 'PTY closed')
})

test('PORT_send_forwards_command_and_propagates_handler_outcomes', async () => {
  const seen = []
  const p = new PtyPort(
    undefined,
    async (pid, cmd) => {
      if (cmd.tag !== 0) seen.push(caseOf(cmd)) // skip the Fork Spawn
      if (caseOf(cmd) === 'Write') return errorResult('disk full')
      if (caseOf(cmd) === 'Signal') throw new Error('signal exploded')
      return okResult(undefined)
    }
  )
  const pid = forkDefault(p, 'pty-sd')

  const ok = resultOf(await PtyPort__Send_Z13021A56(p, pid, new PtyCommand(4, [10, 10])))
  assert.equal(ok.ok, true)

  const err = resultOf(await PtyPort__Send_Z13021A56(p, pid, write))
  assert.equal(err.ok, false)
  assert.equal(err.error, 'disk full')

  const threw = resultOf(await PtyPort__Send_Z13021A56(p, pid, signalOf(PtySignal.Hangup)))
  assert.equal(threw.ok, false)
  assert.equal(threw.error, 'signal exploded')

  assert.deepEqual(seen, ['Resize', 'Write', 'Signal'])
})

test('PORT_send_term_kill_int_marks_abort_for_the_next_completion', async () => {
  for (const sig of [PtySignal.Terminate, PtySignal.Kill, PtySignal.Interrupt]) {
    const p = new PtyPort()
    const got = []
    PtyPort__AddMailboxSender_15902874(p, (item) => got.push(item))
    const pid = forkDefault(p, `pty-ab${sig.tag}`)
    const sent = resultOf(await PtyPort__Send_Z13021A56(p, pid, signalOf(sig)))
    assert.equal(sent.ok, true)
    PtyPort__Complete_3BA7AC67(p, pid, okResult('closed'))
    assert.equal(caseOf(got[0]), 'PtyAborted', `signal ${sig.tag} aborts`)
  }
})

test('PORT_send_plain_signal_does_not_abort_the_completion', async () => {
  const p = new PtyPort()
  const got = []
  PtyPort__AddMailboxSender_15902874(p, (item) => got.push(item))
  const pid = forkDefault(p, 'pty-hup')
  await PtyPort__Send_Z13021A56(p, pid, signalOf(PtySignal.Hangup))
  PtyPort__Complete_3BA7AC67(p, pid, okResult('closed'))
  assert.equal(caseOf(got[0]), 'PtyExited')
})

// ── Read plumbing ────────────────────────────────────────────────────────────

test('PORT_read_unknown_id_is_an_error', async () => {
  const p = new PtyPort()
  const r = resultOf(await PtyPort__Read_Z33F80F6F(p, id('pty-ru')))
  assert.equal(r.ok, false)
  assert.equal(r.error, 'Unknown PTY id: pty-ru')
})

test('PORT_read_after_close_returns_empty_closed_without_handling', async () => {
  const seen = []
  const p = new PtyPort(undefined, async (pid, cmd) => {
    if (cmd.tag !== 0) seen.push(caseOf(cmd))
    return okResult(undefined)
  })
  const pid = forkDefault(p, 'pty-rc')
  PtyPort__Complete_3BA7AC67(p, pid, okResult('done'))
  const r = resultOf(await PtyPort__Read_Z33F80F6F(p, pid))
  assert.deepEqual([r.ok, r.value], [true, ['', true]])
  assert.deepEqual(seen, [], 'no Read command sent after close')
})

test('PORT_read_parks_waiter_and_read_result_resolves_it', async () => {
  const seen = []
  const p = new PtyPort(undefined, async (pid, cmd) => {
    if (cmd.tag !== 0) seen.push(caseOf(cmd))
    return okResult(undefined)
  })
  const pid = forkDefault(p, 'pty-pr')
  const read = PtyPort__Read_Z33F80F6F(p, pid)
  assert.deepEqual(seen, ['Read'])
  PtyPort__ReadResult_3DD67D20(p, pid, 'buffered', false)
  const r = resultOf(await read)
  assert.deepEqual([r.ok, r.value], [true, ['buffered', false]])
})

test('PORT_read_result_can_report_closed_and_reparks_after_resolution', async () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-pr2')
  const first = PtyPort__Read_Z33F80F6F(p, pid)
  PtyPort__ReadResult_3DD67D20(p, pid, 'tail', true)
  const r1 = resultOf(await first)
  assert.deepEqual(r1.value, ['tail', true])

  const second = PtyPort__Read_Z33F80F6F(p, pid)
  PtyPort__ReadResult_3DD67D20(p, pid, 'again', false)
  const r2 = resultOf(await second)
  assert.deepEqual(r2.value, ['again', false])
})

test('PORT_concurrent_read_fails_fast_without_unparking', async () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-cc')
  const first = PtyPort__Read_Z33F80F6F(p, pid)
  const second = resultOf(await PtyPort__Read_Z33F80F6F(p, pid))
  assert.equal(second.ok, false)
  assert.equal(second.error, 'PTY read already in progress')

  PtyPort__ReadResult_3DD67D20(p, pid, 'kept', false)
  const r1 = resultOf(await first)
  assert.deepEqual(r1.value, ['kept', false], 'first waiter still resolves')
})

test('PORT_fail_read_resolves_parked_reader_with_error', async () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-fr')
  const read = PtyPort__Read_Z33F80F6F(p, pid)
  PtyPort__FailRead_Z3F1A8176(p, pid, 'backend died')
  const r = resultOf(await read)
  assert.equal(r.ok, false)
  assert.equal(r.error, 'backend died')
})

test('PORT_read_result_and_fail_read_without_waiter_are_noops', () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-nw')
  PtyPort__ReadResult_3DD67D20(p, pid, 'orphan', false)
  PtyPort__FailRead_Z3F1A8176(p, pid, 'orphan')
})

// ── Complete ─────────────────────────────────────────────────────────────────

const completedItem = (p, pid, outcome) => {
  const got = []
  PtyPort__AddMailboxSender_15902874(p, (item) => got.push(item))
  PtyPort__Complete_3BA7AC67(p, pid, outcome)
  return got[0]
}

test('PORT_complete_default_publishes_pty_exited_closed', () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-cd')
  const item = completedItem(p, pid, undefined)
  assert.equal(caseOf(item), 'PtyExited')
  const info = payloadOf(item)
  assert.deepEqual(
    [info.PtyId, info.Outcome, info.Closed],
    ['pty-cd', 'closed', true]
  )
})

test('PORT_complete_ok_publishes_exited_with_outcome_text', () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-ok')
  const item = completedItem(p, pid, okResult('script output'))
  assert.equal(caseOf(item), 'PtyExited')
  assert.equal(payloadOf(item).Outcome, 'script output')
})

test('PORT_complete_error_publishes_failed_with_code_and_message', () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-err')
  const item = completedItem(p, pid, errorResult('PTY spawn failed: boom'))
  assert.equal(caseOf(item), 'PtyFailed')
  const info = payloadOf(item)
  assert.equal(info.Code, 'ERROR')
  assert.equal(info.Message, 'PTY spawn failed: boom')
  assert.equal(info.Closed, true)
})

test('PORT_complete_after_terminate_publishes_aborted', async () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-ab')
  await PtyPort__Send_Z13021A56(p, pid, signalOf(PtySignal.Terminate))
  const item = completedItem(p, pid, undefined)
  assert.equal(caseOf(item), 'PtyAborted')
  const info = payloadOf(item)
  assert.deepEqual([info.Code, info.Message, info.Closed], ['PTY_ABORTED', 'PTY aborted', true])
})

test('PORT_complete_abort_with_error_outcome_carries_the_error_text', async () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-ab2')
  await PtyPort__Send_Z13021A56(p, pid, signalOf(PtySignal.Kill))
  const item = completedItem(p, pid, errorResult('owner SIGKILLed'))
  assert.equal(caseOf(item), 'PtyAborted')
  assert.equal(payloadOf(item).Message, 'owner SIGKILLed')
})

test('PORT_complete_on_inactive_id_publishes_nothing', () => {
  const p = new PtyPort()
  const got = []
  PtyPort__AddMailboxSender_15902874(p, (item) => got.push(item))
  PtyPort__Complete_3BA7AC67(p, id('pty-ghost'), okResult('x'))
  assert.equal(got.length, 0)
})

test('PORT_complete_isolates_failing_senders', () => {
  const p = new PtyPort()
  const got = []
  PtyPort__AddMailboxSender_15902874(p, () => {
    throw new Error('sender exploded')
  })
  PtyPort__AddMailboxSender_15902874(p, (item) => got.push(item))
  const pid = forkDefault(p, 'pty-th')
  PtyPort__Complete_3BA7AC67(p, pid, okResult('done'))
  assert.deepEqual(got.map(caseOf), ['PtyExited'])
})

test('PORT_complete_removes_the_exit_task_entry', async () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-et')
  PtyPort__RegisterExitTask_3971C262(p, pid, tcs().get_Task())
  PtyPort__Complete_3BA7AC67(p, pid, okResult('done'))
  // CloseAll with no exit task entries must not hang.
  await PtyPort__CloseAll_71136F3F(p, 0)
})

// ── CompleteAborted ──────────────────────────────────────────────────────────

test('PORT_complete_aborted_forces_abort_without_terminate_mark', () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-cab')
  const item = completedItem(p, pid, undefined)
  assert.equal(caseOf(item), 'PtyExited', 'Complete before CompleteAborted is a normal exit')

  // A fresh fork of the same id is active again; CompleteAborted needs no TERM mark.
  forkDefault(p, 'pty-cab', 'again')
  const second = []
  PtyPort__AddMailboxSender_15902874(p, (i) => second.push(i))
  PtyPort__CompleteAborted_20FBD4C9(p, pid, 'owner interrupt')
  assert.equal(caseOf(second[0]), 'PtyAborted')
  const info = payloadOf(second[0])
  assert.deepEqual([info.Code, info.Message], ['PTY_ABORTED', 'owner interrupt'])
})

test('PORT_complete_aborted_defaults_message_and_clears_active', () => {
  const p = new PtyPort()
  const pid = forkDefault(p, 'pty-cab2')
  const got = []
  PtyPort__AddMailboxSender_15902874(p, (i) => got.push(i))
  PtyPort__CompleteAborted_20FBD4C9(p, pid)
  assert.equal(payloadOf(got[0]).Message, 'PTY aborted')
  assert.equal(PtyPort__Exists_Z33F80F6F(p, pid), false)
})

// ── Close / CloseAll ─────────────────────────────────────────────────────────

test('PORT_close_requests_terminate_but_keeps_the_session_live', async () => {
  const seen = []
  const p = new PtyPort(undefined, async (pid, cmd) => {
    if (cmd.tag !== 0) seen.push([PtyId__get_Value(pid), caseOf(cmd), cmd.fields[0].tag])
    return okResult(undefined)
  })
  const pid = forkDefault(p, 'pty-cs')
  const got = []
  PtyPort__AddMailboxSender_15902874(p, (i) => got.push(i))
  PtyPort__Close_3BA7AC67(p, pid)
  assert.deepEqual(seen, [['pty-cs', 'Signal', 0]]) // Terminate
  assert.equal(PtyPort__Exists_Z33F80F6F(p, pid), true, 'close does not drop active')
  assert.equal(got.length, 0, 'close does not publish completion')
})

test('PORT_close_all_with_no_sessions_resolves', async () => {
  const p = new PtyPort()
  await PtyPort__CloseAll_71136F3F(p, 0)
})

test('PORT_close_all_awaits_exit_task_when_it_resolves_in_grace', async () => {
  const seen = []
  const exit = tcs()
  const p = new PtyPort(undefined, async (pid, cmd) => {
    if (cmd.tag !== 0) seen.push(caseOf(cmd))
    return okResult(undefined)
  })
  const pid = forkDefault(p, 'pty-cg')
  PtyPort__RegisterExitTask_3971C262(p, pid, exit.get_Task())
  const closing = PtyPort__CloseAll_71136F3F(p, 1000)
  exit.SetResult(undefined)
  await closing
  assert.deepEqual(seen, ['Signal'], 'only TERM, no KILL escalation')
})

test('PORT_close_all_escalates_to_kill_after_grace', async () => {
  const seen = []
  const exit = tcs()
  const p = new PtyPort(undefined, async (pid, cmd) => {
    if (cmd.tag !== 0) seen.push(caseOf(cmd))
    if (cmd.tag === 3 && cmd.fields[0].tag === 1) exit.SetResult(undefined) // KILL → exit
    return okResult(undefined)
  })
  const pid = forkDefault(p, 'pty-ck')
  PtyPort__RegisterExitTask_3971C262(p, pid, exit.get_Task())
  await PtyPort__CloseAll_71136F3F(p, 0)
  assert.deepEqual(seen, ['Signal', 'Signal'], 'TERM then KILL')
})

test('PORT_close_all_kill_failure_propagates', async () => {
  const exit = tcs()
  const p = new PtyPort(undefined, async (pid, cmd) => {
    if (cmd.tag === 3 && cmd.fields[0].tag === 1) return errorResult('no such process')
    return okResult(undefined)
  })
  const pid = forkDefault(p, 'pty-cf')
  PtyPort__RegisterExitTask_3971C262(p, pid, exit.get_Task())
  await assert.rejects(
    PtyPort__CloseAll_71136F3F(p, 0),
    /PTY kill failed for pty-cf: no such process/
  )
})

test('PORT_close_all_skips_ids_without_exit_task', async () => {
  const seen = []
  const p = new PtyPort(undefined, async (pid, cmd) => {
    if (cmd.tag !== 0) seen.push(caseOf(cmd))
    return okResult(undefined)
  })
  forkDefault(p, 'pty-cn')
  await PtyPort__CloseAll_71136F3F(p, 0)
  assert.deepEqual(seen, ['Signal'])
})

// ── List ─────────────────────────────────────────────────────────────────────

test('PORT_list_reports_agents_and_active_handles', () => {
  const p = new PtyPort(undefined, undefined, () => [agent])
  const pid = forkDefault(p, 'pty-ls', 'tail -f')
  const [agents, ptys] = PtyPort__List(p)
  assert.deepEqual(agents, [agent])
  const handles = [...ptys]
  assert.equal(handles.length, 1)
  assert.equal(handles[0].Command, 'tail -f')
  assert.equal(PtyId__get_Value(handles[0].Id), 'pty-ls')
  assert.equal(handles[0].Agent, agent)
  assert.ok(handles[0].StartedAt instanceof Date)

  PtyPort__Complete_3BA7AC67(p, pid, okResult('done'))
  assert.equal([...PtyPort__List(p)[1]].length, 0, 'completed pty leaves the list')
})

test('PORT_list_without_provider_returns_empty_agents', () => {
  const p = new PtyPort()
  forkDefault(p, 'pty-le')
  const [agents, ptys] = PtyPort__List(p)
  assert.equal([...agents].length, 0)
  assert.equal([...ptys].length, 1)
})
