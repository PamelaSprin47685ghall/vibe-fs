// Process owner API: PTY lifecycle, command dispatch, completion and cleanup.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  createPtyPort,
  ptyId,
  ptyIdView,
  ptyCommandWrite,
  ptyCommandSignal,
  ptyCommandResize,
  ptyCommandRead,
  portAddMailboxSender,
  portFork,
  portExists,
  portKnown,
  portSend,
  portRead,
  portReadResult,
  portFailRead,
  portRegisterExitTask,
  portComplete,
  portCompleteAborted,
  portClose,
  portCloseAll,
  portList,
} = await import('../../../dist/Process/Surface.js')

const agent = { Name: 'distiller' }
const bytes = new TextEncoder().encode('hi')
const write = ptyCommandWrite(bytes)
const signalOf = (name) => ptyCommandSignal(name)
const id = (value) => ptyId(value)
const forkDefault = (port, value, command = 'echo hi') =>
  portFork(port, command, 'distiller', value === undefined ? undefined : id(value), undefined)
const success = { ok: true, value: undefined }
const failure = (error) => ({ ok: false, error })
const exitSignal = () => {
  let resolve
  const promise = new Promise((r) => { resolve = r })
  return { promise, resolve }
}

// ── constructor / handler boundary ──────────────────────────────────────────

test('WHAT[PROC-001] PORT_ctor_defaults_are_safe_and_functional', async () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-x')
  assert.equal(portExists(port, pid), true)
  assert.deepEqual(await portSend(port, pid, write), success)
})

test('WHAT[PROC-001] PORT_ctor_keeps_supplied_sender_handler_and_agent_provider', async () => {
  const seen = []
  const sender = (item) => seen.push(item.kind)
  const handler = async (_pid, command) => {
    if (command.kind !== 'Spawn') seen.push(command.kind)
    return success
  }
  const port = createPtyPort({ sender, handler, agentProvider: () => [agent] })
  const pid = forkDefault(port, 'pty-x')
  assert.deepEqual(await portSend(port, pid, write), success)
  assert.deepEqual(seen, ['Write'])
  assert.equal(portList(port).agents.length, 1)
})

test('WHAT[PROC-003] PORT_AddMailboxSender_reaches_every_registered_sender', () => {
  const port = createPtyPort({})
  const got = []
  portAddMailboxSender(port, (item) => got.push(item.kind))
  portAddMailboxSender(port, (item) => got.push(item.kind))
  const pid = forkDefault(port, 'pty-s1')
  portComplete(port, pid, { ok: true, value: 'done' })
  assert.deepEqual(got, ['PtyExited', 'PtyExited'])
})

// ── Fork / Exists / Known ────────────────────────────────────────────────────

test('WHAT[PROC-001] PORT_fork_generates_pty_id_and_dispatches_spawn', async () => {
  const seen = []
  const port = createPtyPort({
    handler: async (pid, command) => {
      seen.push([pid, command.kind, command.command, command.cwd])
      return success
    },
  })
  const pid = forkDefault(port, undefined, 'sleep 1')
  const value = ptyIdView(pid)
  assert.match(value, /^pty-[0-9a-f]{8}$/)
  assert.deepEqual(seen, [[value, 'Spawn', 'sleep 1', '']])
  assert.equal(portExists(port, pid), true)
})

test('WHAT[PROC-001] PORT_fork_honors_explicit_id_and_cwd', async () => {
  const seen = []
  const port = createPtyPort({
    handler: async (pid, command) => {
      seen.push([pid, command.command, command.cwd])
      return success
    },
  })
  const pid = portFork(port, 'ls -la', 'distiller', id('pty-custom'), '/srv')
  assert.equal(ptyIdView(pid), 'pty-custom')
  assert.deepEqual(seen, [['pty-custom', 'ls -la', '/srv']])
})

test('WHAT[PROC-001] PORT_fork_twice_on_same_id_replaces_the_handle', () => {
  const port = createPtyPort({})
  forkDefault(port, 'pty-rf', 'first')
  forkDefault(port, 'pty-rf', 'second')
  assert.equal(portList(port).ptys.length, 1)
  assert.equal(portList(port).ptys[0].command, 'second')
})

test('WHAT[PROC-001] PORT_exists_and_known_track_active_and_closed', () => {
  const port = createPtyPort({})
  const unknown = id('pty-ne')
  assert.equal(portExists(port, unknown), false)
  assert.equal(portKnown(port, unknown), false)

  const pid = forkDefault(port, 'pty-ek')
  assert.equal(portExists(port, pid), true)
  assert.equal(portKnown(port, pid), true)

  portComplete(port, pid, { ok: true, value: 'bye' })
  assert.equal(portExists(port, pid), false)
  assert.equal(portKnown(port, pid), true)
})

// ── Send ─────────────────────────────────────────────────────────────────────

test('WHAT[PROC-001] PORT_send_unknown_and_closed_ids_fail_with_distinct_reasons', async () => {
  const port = createPtyPort({})
  assert.deepEqual(await portSend(port, id('pty-un'), write), failure('Unknown PTY id: pty-un'))

  const pid = forkDefault(port, 'pty-cl')
  portComplete(port, pid, { ok: true, value: 'done' })
  assert.deepEqual(await portSend(port, pid, write), failure('PTY closed'))
})

test('WHAT[PROC-001] PORT_send_forwards_command_and_propagates_handler_outcomes', async () => {
  const seen = []
  const port = createPtyPort({
    handler: async (_pid, command) => {
      if (command.kind !== 'Spawn') seen.push(command.kind)
      if (command.kind === 'Write') return failure('disk full')
      if (command.kind === 'Signal') throw new Error('signal exploded')
      return success
    },
  })
  const pid = forkDefault(port, 'pty-sd')

  assert.deepEqual(await portSend(port, pid, ptyCommandResize(10, 10)), success)
  assert.deepEqual(await portSend(port, pid, write), failure('disk full'))
  assert.deepEqual(await portSend(port, pid, signalOf('HUP')), failure('signal exploded'))
  assert.deepEqual(seen, ['Resize', 'Write', 'Signal'])
})

test('WHAT[PROC-002] PORT_send_term_kill_int_marks_abort_for_the_next_completion', async () => {
  for (const signal of ['TERM', 'KILL', 'INT']) {
    const port = createPtyPort({})
    const got = []
    portAddMailboxSender(port, (item) => got.push(item))
    const pid = forkDefault(port, `pty-ab${signal}`)
    assert.deepEqual(await portSend(port, pid, signalOf(signal)), success)
    portComplete(port, pid, { ok: true, value: 'closed' })
    assert.equal(got[0].kind, 'PtyAborted', `signal ${signal} aborts`)
  }
})

test('WHAT[PROC-003] PORT_send_plain_signal_does_not_abort_the_completion', async () => {
  const port = createPtyPort({})
  const got = []
  portAddMailboxSender(port, (item) => got.push(item))
  const pid = forkDefault(port, 'pty-hup')
  await portSend(port, pid, signalOf('HUP'))
  portComplete(port, pid, { ok: true, value: 'closed' })
  assert.equal(got[0].kind, 'PtyExited')
})

// ── Read plumbing ────────────────────────────────────────────────────────────

test('WHAT[PROC-001] PORT_read_unknown_id_is_an_error', async () => {
  assert.deepEqual(await portRead(createPtyPort({}), id('pty-ru')), failure('Unknown PTY id: pty-ru'))
})

test('WHAT[PROC-001] PORT_read_after_close_returns_empty_closed_without_handling', async () => {
  const seen = []
  const port = createPtyPort({ handler: async (_pid, command) => { if (command.kind !== 'Spawn') seen.push(command.kind); return success } })
  const pid = forkDefault(port, 'pty-rc')
  portComplete(port, pid, { ok: true, value: 'done' })
  assert.deepEqual(await portRead(port, pid), { ok: true, value: { output: '', closed: true } })
  assert.deepEqual(seen, [])
})

test('WHAT[PROC-001] PORT_read_parks_waiter_and_read_result_resolves_it', async () => {
  const seen = []
  const port = createPtyPort({ handler: async (_pid, command) => { if (command.kind !== 'Spawn') seen.push(command.kind); return success } })
  const pid = forkDefault(port, 'pty-pr')
  const read = portRead(port, pid)
  assert.deepEqual(seen, ['Read'])
  portReadResult(port, pid, 'buffered', false)
  assert.deepEqual(await read, { ok: true, value: { output: 'buffered', closed: false } })
})

test('WHAT[PROC-001] PORT_read_result_can_report_closed_and_reparks_after_resolution', async () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-pr2')
  const first = portRead(port, pid)
  portReadResult(port, pid, 'tail', true)
  assert.deepEqual(await first, { ok: true, value: { output: 'tail', closed: true } })

  const second = portRead(port, pid)
  portReadResult(port, pid, 'again', false)
  assert.deepEqual(await second, { ok: true, value: { output: 'again', closed: false } })
})

test('WHAT[PROC-001] PORT_concurrent_read_fails_fast_without_unparking', async () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-cc')
  const first = portRead(port, pid)
  assert.deepEqual(await portRead(port, pid), failure('PTY read already in progress'))
  portReadResult(port, pid, 'kept', false)
  assert.deepEqual(await first, { ok: true, value: { output: 'kept', closed: false } })
})

test('WHAT[PROC-001] PORT_fail_read_resolves_parked_reader_with_error', async () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-fr')
  const read = portRead(port, pid)
  portFailRead(port, pid, 'backend died')
  assert.deepEqual(await read, failure('backend died'))
})

test('WHAT[PROC-001] PORT_read_result_and_fail_read_without_waiter_are_noops', () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-nw')
  portReadResult(port, pid, 'orphan', false)
  portFailRead(port, pid, 'orphan')
})

// ── Complete ─────────────────────────────────────────────────────────────────

const completedItem = (port, pid, outcome) => {
  const got = []
  portAddMailboxSender(port, (item) => got.push(item))
  portComplete(port, pid, outcome)
  return got[0]
}

test('WHAT[PROC-003] PORT_complete_default_publishes_pty_exited_closed', () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-cd')
  const item = completedItem(port, pid, undefined)
  assert.equal(item.kind, 'PtyExited')
  assert.deepEqual([item.ptyId, item.outcome, item.closed], ['pty-cd', 'closed', true])
})

test('WHAT[PROC-003] PORT_complete_ok_publishes_exited_with_outcome_text', () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-ok')
  const item = completedItem(port, pid, { ok: true, value: 'script output' })
  assert.equal(item.kind, 'PtyExited')
  assert.equal(item.outcome, 'script output')
})

test('WHAT[PROC-003] PORT_complete_error_publishes_failed_with_code_and_message', () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-err')
  const item = completedItem(port, pid, failure('PTY spawn failed: boom'))
  assert.equal(item.kind, 'PtyFailed')
  assert.equal(item.code, 'ERROR')
  assert.equal(item.message, 'PTY spawn failed: boom')
  assert.equal(item.closed, true)
})

test('WHAT[PROC-003] PORT_complete_after_terminate_publishes_aborted', async () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-ab')
  await portSend(port, pid, signalOf('TERM'))
  const item = completedItem(port, pid, undefined)
  assert.equal(item.kind, 'PtyAborted')
  assert.deepEqual([item.code, item.message, item.closed], ['PTY_ABORTED', 'PTY aborted', true])
})

test('WHAT[PROC-003] PORT_complete_abort_with_error_outcome_carries_the_error_text', async () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-ab2')
  await portSend(port, pid, signalOf('KILL'))
  const item = completedItem(port, pid, failure('owner SIGKILLed'))
  assert.equal(item.kind, 'PtyAborted')
  assert.equal(item.message, 'owner SIGKILLed')
})

test('WHAT[PROC-003] PORT_complete_on_inactive_id_publishes_nothing', () => {
  const port = createPtyPort({})
  const got = []
  portAddMailboxSender(port, (item) => got.push(item))
  portComplete(port, id('pty-ghost'), { ok: true, value: 'x' })
  assert.equal(got.length, 0)
})

test('WHAT[PROC-003] PORT_complete_isolates_failing_senders', () => {
  const port = createPtyPort({})
  const got = []
  portAddMailboxSender(port, () => { throw new Error('sender exploded') })
  portAddMailboxSender(port, (item) => got.push(item))
  const pid = forkDefault(port, 'pty-th')
  portComplete(port, pid, { ok: true, value: 'done' })
  assert.deepEqual(got.map((item) => item.kind), ['PtyExited'])
})

test('WHAT[PROC-003] PORT_complete_removes_the_exit_task_entry', async () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-et')
  const exit = exitSignal()
  portRegisterExitTask(port, pid, exit.promise)
  portComplete(port, pid, { ok: true, value: 'done' })
  await portCloseAll(port, 0)
})

// ── CompleteAborted ──────────────────────────────────────────────────────────

test('WHAT[PROC-003] PORT_complete_aborted_forces_abort_without_terminate_mark', () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-cab')
  const got = []
  portAddMailboxSender(port, (item) => got.push(item))
  portCompleteAborted(port, pid, 'owner interrupt')
  assert.equal(got[0].kind, 'PtyAborted')
  assert.equal(got[0].message, 'owner interrupt')
  assert.equal(portExists(port, pid), false)
})

test('WHAT[PROC-003] PORT_complete_aborted_defaults_message_and_clears_active', () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-cab2')
  const got = []
  portAddMailboxSender(port, (item) => got.push(item))
  portCompleteAborted(port, pid, undefined)
  assert.equal(got[0].message, 'PTY aborted')
  assert.equal(portExists(port, pid), false)
})

// ── Close / CloseAll ─────────────────────────────────────────────────────────

test('WHAT[PROC-007] PORT_close_requests_terminate_but_keeps_the_session_live', async () => {
  const seen = []
  const port = createPtyPort({ handler: async (_pid, command) => { if (command.kind !== 'Spawn') seen.push(command.signal); return success } })
  const pid = forkDefault(port, 'pty-cs')
  portClose(port, pid)
  assert.deepEqual(seen, ['SIGTERM'])
  assert.equal(portExists(port, pid), true)
})

test('WHAT[PROC-007] PORT_close_all_with_no_sessions_resolves', async () => {
  await portCloseAll(createPtyPort({}), 0)
})

test('WHAT[PROC-007] PORT_close_all_awaits_exit_task_when_it_resolves_in_grace', async () => {
  const seen = []
  const exit = exitSignal()
  const port = createPtyPort({ handler: async (_pid, command) => { if (command.kind !== 'Spawn') seen.push(command.kind); return success } })
  const pid = forkDefault(port, 'pty-cg')
  portRegisterExitTask(port, pid, exit.promise)
  const closing = portCloseAll(port, 1000)
  exit.resolve()
  await closing
  assert.deepEqual(seen, ['Signal'])
})

test('WHAT[PROC-007] PORT_close_all_escalates_to_kill_after_grace', async () => {
  const seen = []
  const exit = exitSignal()
  const port = createPtyPort({ handler: async (_pid, command) => {
    if (command.kind !== 'Spawn') seen.push(command.signal)
    if (command.kind === 'Signal' && command.signal === 'SIGKILL') exit.resolve()
    return success
  } })
  const pid = forkDefault(port, 'pty-ck')
  portRegisterExitTask(port, pid, exit.promise)
  await portCloseAll(port, 0)
  assert.deepEqual(seen, ['SIGTERM', 'SIGKILL'])
})

test('WHAT[PROC-007] PORT_close_all_kill_failure_propagates', async () => {
  const exit = exitSignal()
  const port = createPtyPort({ handler: async (_pid, command) => {
    if (command.kind === 'Signal' && command.signal === 'SIGKILL') return failure('no such process')
    return success
  } })
  const pid = forkDefault(port, 'pty-cf')
  portRegisterExitTask(port, pid, exit.promise)
  await assert.rejects(portCloseAll(port, 0), /PTY kill failed for pty-cf: no such process/)
})

test('WHAT[PROC-007] PORT_close_all_skips_ids_without_exit_task', async () => {
  const seen = []
  const port = createPtyPort({ handler: async (_pid, command) => { if (command.kind !== 'Spawn') seen.push(command.kind); return success } })
  forkDefault(port, 'pty-cn')
  await portCloseAll(port, 0)
  assert.deepEqual(seen, ['Signal'])
})

// ── List ─────────────────────────────────────────────────────────────────────

test('WHAT[PROC-007] PORT_list_reports_active_handles', () => {
  const port = createPtyPort({})
  const pid = forkDefault(port, 'pty-ls', 'tail -f')
  const listed = portList(port)
  assert.equal(listed.ptys.length, 1)
  assert.equal(listed.ptys[0].command, 'tail -f')
  assert.equal(listed.ptys[0].id, 'pty-ls')
  assert.equal(listed.ptys[0].agent, 'distiller')
  assert.ok(typeof listed.ptys[0].startedAt === 'string')

  portComplete(port, pid, { ok: true, value: 'done' })
  assert.equal(portList(port).ptys.length, 0)
})

test('WHAT[PROC-007] PORT_list_without_provider_returns_empty_agents', () => {
  const port = createPtyPort({})
  forkDefault(port, 'pty-le')
  assert.equal(portList(port).agents.length, 0)
})
