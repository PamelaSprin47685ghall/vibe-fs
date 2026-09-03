// Process owner API: PtySupervisor registry, pending commands and attach lifecycle.

import assert from 'node:assert/strict'
import test from 'node:test'
import { spawn } from 'node:child_process'

const {
  supervisorCreate,
  supervisorAdd,
  supervisorTryGet,
  supervisorGet,
  supervisorRemove,
  supervisorList,
  supervisorSignalName,
  supervisorEnsureSpawn,
  supervisorSpawnSync,
  supervisorSetSpawn,
  supervisorFailPending,
  supervisorTakePending,
  supervisorDropPending,
  supervisorApplyLive,
  supervisorAttach,
  supervisorCancelAll,
  supervisorPendingEntries,
  pendingEntryView,
  pendingResolve,
  sessionCreate,
  sessionView,
  sessionSetClosed,
  sessionAppendOutput,
  sessionPushPending,
  sessionPushPendingTask,
  createPtyPort,
  portFork,
  portRead,
  portReadResult,
  portAddMailboxSender,
  ptyId,
  ptyIdView,
  ptyCommandRead,
  ptyCommandWrite,
  ptyCommandSignal,
  ptyCommandResize,
  ptyCommandSpawn,
} = await import('../../../dist/Process/Surface.js')

const id = (value) => ptyId(value)
const port = () => createPtyPort({})
const resultError = (reason) => ({ ok: false, error: reason })
const resultOk = { ok: true, value: undefined }
const settle = () => new Promise((resolve) => setImmediate(resolve))

const child = () => spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'])
const killChild = (value) => {
  if (value && value.exitCode === null && value.signalCode === null) value.kill('SIGKILL')
}
const died = (value) => value.exitCode !== null || value.signalCode !== null
const waitExit = (value) =>
  Promise.race([
    new Promise((resolve) => value.once('exit', resolve)),
    new Promise((resolve) => setTimeout(resolve, 2_000)),
  ])
const fakeTerm = (pid) => {
  const term = {
    pid,
    dataCb: null,
    exitCb: null,
    writes: [],
    resizes: [],
    onData: (cb) => { term.dataCb = cb },
    onExit: (cb) => { term.exitCb = cb },
    write: (text) => term.writes.push(text),
    resize: (width, height) => term.resizes.push([width, height]),
  }
  return term
}
const portWith = (value) => {
  const p = port()
  portFork(p, 'echo hi', 'distiller', id(value), undefined)
  return p
}

// ── session registry ─────────────────────────────────────────────────────────

test('WHAT[PROC-007] SUPERVISOR_add_tryGet_get_roundtrip', () => {
  const supervisor = supervisorCreate()
  const session = sessionCreate('pty-a', null)
  supervisorAdd(supervisor, id('pty-a'), session)
  assert.notEqual(supervisorTryGet(supervisor, id('pty-a')), null)
  assert.equal(sessionView(supervisorGet(supervisor, id('pty-a'))).ptyId, 'pty-a')
})

test('WHAT[PROC-007] SUPERVISOR_tryGet_missing_returns_none_and_get_throws', () => {
  const supervisor = supervisorCreate()
  assert.equal(supervisorTryGet(supervisor, id('pty-missing')), null)
  assert.throws(() => supervisorGet(supervisor, id('pty-missing')), /Unknown PTY id: pty-missing/)
})

test('WHAT[PROC-007] SUPERVISOR_remove_drops_the_session', () => {
  const supervisor = supervisorCreate()
  supervisorAdd(supervisor, id('pty-r'), sessionCreate('pty-r', null))
  supervisorRemove(supervisor, id('pty-r'))
  assert.equal(supervisorTryGet(supervisor, id('pty-r')), null)
})

test('WHAT[PROC-007] SUPERVISOR_list_returns_added_ids_only', () => {
  const supervisor = supervisorCreate()
  supervisorAdd(supervisor, id('pty-1'), sessionCreate('pty-1', null))
  supervisorAdd(supervisor, id('pty-2'), sessionCreate('pty-2', null))
  assert.deepEqual(supervisorList(supervisor).sort(), ['pty-1', 'pty-2'])
  supervisorRemove(supervisor, id('pty-1'))
  assert.deepEqual(supervisorList(supervisor), ['pty-2'])
})

// ── signal name codec ─────────────────────────────────────────────────────────

test('WHAT[PROC-001] SUPERVISOR_signalName_maps_every_signal_to_a_kill_name', () => {
  for (const [wire, expected] of [
    ['TERM', 'SIGTERM'], ['KILL', 'SIGKILL'], ['INT', 'SIGINT'], ['HUP', 'SIGHUP'],
    ['QUIT', 'SIGQUIT'], ['USR1', 'SIGUSR1'], ['USR2', 'SIGUSR2'],
  ]) assert.equal(supervisorSignalName(wire), expected)
})

// ── spawn loader ─────────────────────────────────────────────────────────────

test('WHAT[PROC-001] SUPERVISOR_ensureSpawn_reuses_one_loader_and_faults_without_bun_pty', async () => {
  const supervisor = supervisorCreate()
  const first = supervisorEnsureSpawn(supervisor)
  const second = supervisorEnsureSpawn(supervisor)
  assert.equal(first, second, 'loader task is cached')
  await assert.rejects(first)
  assert.throws(() => supervisorSpawnSync(supervisor, 'echo hi', ''), /bun-pty is not loaded/)
})

test('WHAT[PROC-001] SUPERVISOR_spawnSync_fails_fast_when_loader_never_ran', () => {
  assert.throws(() => supervisorSpawnSync(supervisorCreate(), 'echo hi', ''), /bun-pty is not loaded/)
})

test('WHAT[PROC-001] SUPERVISOR_spawnSync_invokes_sh_lc_with_fixed_options', () => {
  const supervisor = supervisorCreate()
  let seen
  supervisorSetSpawn(supervisor, (shell, args, options) => {
    seen = [shell, args, options]
    return { pid: 4242 }
  })
  const term = supervisorSpawnSync(supervisor, 'echo hi', '/tmp/work')
  assert.equal(term.pid, 4242)
  assert.equal(seen[0], 'sh')
  assert.deepEqual(seen[1], ['-lc', 'echo hi'])
  assert.equal(seen[2].name, 'xterm-256color')
  assert.equal(seen[2].cols, 80)
  assert.equal(seen[2].rows, 24)
  assert.equal(seen[2].cwd, '/tmp/work')
})

test('WHAT[PROC-001] SUPERVISOR_spawnSync_defaults_cwd_to_process_cwd', () => {
  const supervisor = supervisorCreate()
  let seenCwd
  supervisorSetSpawn(supervisor, (_shell, _args, options) => {
    seenCwd = options.cwd
    return {}
  })
  supervisorSpawnSync(supervisor, 'ls', '')
  assert.equal(seenCwd, process.cwd())
})

// ── pending queue ────────────────────────────────────────────────────────────

test('WHAT[PROC-007] SUPERVISOR_takePending_returns_and_clears_the_queue', () => {
  const supervisor = supervisorCreate()
  const session = sessionCreate('pty-q', null)
  sessionPushPending(session, ptyCommandWrite('first'))
  sessionPushPending(session, ptyCommandWrite('second'))
  supervisorAdd(supervisor, id('pty-q'), session)

  const pending = supervisorTakePending(supervisor, id('pty-q'))
  assert.deepEqual(supervisorPendingEntries(pending).map(pendingEntryView).map((entry) => entry.command.kind), ['Write', 'Write'])
  assert.equal(sessionView(session).pendingCount, 0)
  assert.deepEqual(supervisorPendingEntries(supervisorTakePending(supervisor, id('pty-q'))), [])
})

test('WHAT[PROC-007] SUPERVISOR_takePending_unknown_id_is_empty', () => {
  const supervisor = supervisorCreate()
  assert.deepEqual(supervisorPendingEntries(supervisorTakePending(supervisor, id('pty-nope'))), [])
})

test('WHAT[PROC-007] SUPERVISOR_drop_removes_session_and_returns_pending', () => {
  const supervisor = supervisorCreate()
  const session = sessionCreate('pty-d', null)
  sessionPushPending(session, ptyCommandWrite('queued'))
  supervisorAdd(supervisor, id('pty-d'), session)

  const dropped = supervisorDropPending(supervisor, id('pty-d'))
  assert.equal(supervisorPendingEntries(dropped).length, 1)
  assert.equal(supervisorTryGet(supervisor, id('pty-d')), null)
  assert.deepEqual(supervisorPendingEntries(supervisorDropPending(supervisor, id('pty-d'))), [])
})

test('WHAT[PROC-003] SUPERVISOR_failPending_resolves_every_tcs_with_the_reason', async () => {
  const supervisor = supervisorCreate()
  const session = sessionCreate('pty-fp0', null)
  const parked = sessionPushPendingTask(session, ptyCommandWrite('write'))
  supervisorAdd(supervisor, id('pty-fp0'), session)
  const pending = supervisorTakePending(supervisor, id('pty-fp0'))
  supervisorFailPending(pending, 'PTY exited before command was applied')
  assert.deepEqual(await parked, resultError('PTY exited before command was applied'))
})

// ── applyLive ────────────────────────────────────────────────────────────────

test('WHAT[PROC-001] SUPERVISOR_applyLive_closed_session_short_circuits_ok', async () => {
  const supervisor = supervisorCreate()
  const session = sessionCreate('pty-c', null)
  sessionSetClosed(session, true)
  supervisorAdd(supervisor, id('pty-c'), session)
  assert.deepEqual(await supervisorApplyLive(supervisor, port(), id('pty-c'), ptyCommandRead()), resultOk)
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_write_forwards_utf8_to_backend', async () => {
  const supervisor = supervisorCreate()
  const writes = []
  const backend = { write: (text) => writes.push(text) }
  supervisorAdd(supervisor, id('pty-w'), sessionCreate('pty-w', backend))
  const result = await supervisorApplyLive(supervisor, port(), id('pty-w'), ptyCommandWrite(new TextEncoder().encode('héllo')))
  assert.deepEqual(result, resultOk)
  assert.deepEqual(writes, ['héllo'])
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_write_backend_error_becomes_error_result', async () => {
  const supervisor = supervisorCreate()
  const backend = { write: () => { throw new Error('EPIPE') } }
  supervisorAdd(supervisor, id('pty-we'), sessionCreate('pty-we', backend))
  assert.deepEqual(await supervisorApplyLive(supervisor, port(), id('pty-we'), ptyCommandWrite(new Uint8Array(0))), resultError('EPIPE'))
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_read_drains_buffer_into_port', async () => {
  const supervisor = supervisorCreate()
  const p = portWith('pty-re')
  const session = sessionCreate('pty-re', {})
  sessionAppendOutput(session, 'partial output')
  supervisorAdd(supervisor, id('pty-re'), session)

  assert.deepEqual(await supervisorApplyLive(supervisor, p, id('pty-re'), ptyCommandRead()), resultOk)
  assert.equal(sessionView(session).output, '', 'buffer drained')
  const read = portRead(p, id('pty-re'))
  portReadResult(p, id('pty-re'), 'after', false)
  assert.deepEqual(await read, { ok: true, value: { output: 'after', closed: false } })
})

test('WHAT[PROC-002] SUPERVISOR_applyLive_signal_kills_the_real_process_group_or_process', async () => {
  const process = child()
  try {
    const supervisor = supervisorCreate()
    supervisorAdd(supervisor, id('pty-k'), sessionCreate('pty-k', { pid: process.pid }))
    assert.deepEqual(await supervisorApplyLive(supervisor, port(), id('pty-k'), ptyCommandSignal('KILL')), resultOk)
    await waitExit(process)
    assert.ok(died(process), 'child was killed')
  } finally { killChild(process) }
})

test('WHAT[PROC-002] SUPERVISOR_applyLive_signal_unknown_pid_becomes_error', async () => {
  const supervisor = supervisorCreate()
  supervisorAdd(supervisor, id('pty-ku'), sessionCreate('pty-ku', { pid: 2_147_483_647 }))
  const result = await supervisorApplyLive(supervisor, port(), id('pty-ku'), ptyCommandSignal('TERM'))
  assert.equal(result.ok, false)
  assert.match(result.error, /ESRCH/)
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_resize_swallows_backend_errors', async () => {
  const supervisor = supervisorCreate()
  const resizes = []
  const backend = { resize: (width, height) => { resizes.push([width, height]); throw new Error('nope') } }
  supervisorAdd(supervisor, id('pty-z'), sessionCreate('pty-z', backend))
  assert.deepEqual(await supervisorApplyLive(supervisor, port(), id('pty-z'), ptyCommandResize(120, 40)), resultOk)
  assert.deepEqual(resizes, [[120, 40]])
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_spawn_on_live_backend_is_a_noop', async () => {
  const supervisor = supervisorCreate()
  supervisorAdd(supervisor, id('pty-sp'), sessionCreate('pty-sp', {}))
  assert.deepEqual(await supervisorApplyLive(supervisor, port(), id('pty-sp'), ptyCommandSpawn('x', '')), resultOk)
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_write_without_backend_parks_until_resolved', async () => {
  const supervisor = supervisorCreate()
  const session = sessionCreate('pty-p', null)
  supervisorAdd(supervisor, id('pty-p'), session)
  const pending = supervisorApplyLive(supervisor, port(), id('pty-p'), ptyCommandWrite(new Uint8Array(0)))
  const entries = supervisorTakePending(supervisor, id('pty-p'))
  assert.equal(pendingEntryView(supervisorPendingEntries(entries)[0]).command.kind, 'Write')
  pendingResolve(entries, 0, resultOk)
  assert.deepEqual(await pending, resultOk)
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_parked_write_resolves_with_error', async () => {
  const supervisor = supervisorCreate()
  const session = sessionCreate('pty-pe', null)
  supervisorAdd(supervisor, id('pty-pe'), session)
  const pending = supervisorApplyLive(supervisor, port(), id('pty-pe'), ptyCommandWrite(new Uint8Array(0)))
  const entries = supervisorTakePending(supervisor, id('pty-pe'))
  pendingResolve(entries, 0, resultError('backend vanished'))
  assert.deepEqual(await pending, resultError('backend vanished'))
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_non_write_commands_without_backend_return_ok_immediately', async () => {
  const supervisor = supervisorCreate()
  const session = sessionCreate('pty-nb', null)
  supervisorAdd(supervisor, id('pty-nb'), session)
  for (const command of [ptyCommandRead(), ptyCommandSignal('HUP'), ptyCommandResize(10, 10)]) {
    assert.deepEqual(await supervisorApplyLive(supervisor, port(), id('pty-nb'), command), resultOk)
  }
  assert.equal(sessionView(session).pendingCount, 3)
})

// ── attach / onData / onExit ─────────────────────────────────────────────────

test('WHAT[PROC-007] SUPERVISOR_attach_registers_live_session_and_forwards_onData_to_buffer', () => {
  const supervisor = supervisorCreate()
  const term = fakeTerm(9999)
  supervisorAttach(supervisor, portWith('pty-at'), id('pty-at'), term)
  const session = supervisorGet(supervisor, id('pty-at'))
  assert.equal(sessionView(session).backend, term)
  assert.equal(sessionView(session).closed, false)
  term.dataCb('hello ')
  term.dataCb('world')
  assert.equal(sessionView(session).output, 'hello world')
})

test('WHAT[PROC-007] SUPERVISOR_attach_onData_ignored_after_session_closed', () => {
  const supervisor = supervisorCreate()
  const term = fakeTerm(9999)
  supervisorAttach(supervisor, portWith('pty-ic'), id('pty-ic'), term)
  const session = supervisorGet(supervisor, id('pty-ic'))
  sessionSetClosed(session, true)
  term.dataCb('late data')
  assert.equal(sessionView(session).output, '')
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_completes_exit_publishes_closed_and_drops_session', async () => {
  const supervisor = supervisorCreate()
  const term = fakeTerm(9999)
  const completions = []
  const p = portWith('pty-ex')
  portAddMailboxSender(p, (item) => completions.push(item))
  supervisorAttach(supervisor, p, id('pty-ex'), term)
  term.exitCb({ exitCode: 0 })
  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(supervisorTryGet(supervisor, id('pty-ex')), null, 'session dropped')
  assert.equal(completions.length, 1)
  assert.equal(completions[0].kind, 'PtyExited')
  assert.equal(completions[0].ptyId, 'pty-ex')
  assert.equal(completions[0].outcome, 'closed')
  assert.equal(completions[0].closed, true)
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_publishes_residual_output', async () => {
  const supervisor = supervisorCreate()
  const term = fakeTerm(9999)
  const completions = []
  const p = portWith('pty-ro')
  portAddMailboxSender(p, (item) => completions.push(item))
  supervisorAttach(supervisor, p, id('pty-ro'), term)
  term.dataCb('final words')
  term.exitCb({ exitCode: 1 })
  await new Promise((resolve) => setImmediate(resolve))
  assert.equal(completions[0].kind, 'PtyExited')
  assert.equal(completions[0].outcome, 'final words')
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_fails_pending_writes_and_parked_read', async () => {
  const supervisor = supervisorCreate()
  const term = fakeTerm(9999)
  const p = portWith('pty-fp')
  supervisorAttach(supervisor, p, id('pty-fp'), term)
  const parkedRead = portRead(p, id('pty-fp'))
  const session = supervisorGet(supervisor, id('pty-fp'))
  const parkedWrite = sessionPushPendingTask(session, ptyCommandWrite(new Uint8Array(0)))
  term.exitCb({ exitCode: 0 })
  assert.deepEqual(await parkedRead, resultError('PTY exited before read completed'))
  assert.deepEqual(await parkedWrite, resultError('PTY exited before command was applied'))
})

test('WHAT[PROC-006] SUPERVISOR_attach_without_port_entry_kills_the_term', async () => {
  const process = child()
  try {
    const supervisor = supervisorCreate()
    supervisorAttach(supervisor, createPtyPort({}), id('pty-nk'), fakeTerm(process.pid))
    await waitExit(process)
    assert.ok(died(process), 'unregistered attach SIGKILLs the process tree')
  } finally { killChild(process) }
})

test('WHAT[PROC-007] SUPERVISOR_attach_replays_pending_writes_onto_the_live_backend', async () => {
  const supervisor = supervisorCreate()
  const session = sessionCreate('pty-rp', null)
  supervisorAdd(supervisor, id('pty-rp'), session)
  const parked = supervisorApplyLive(supervisor, port(), id('pty-rp'), ptyCommandWrite(new TextEncoder().encode('early')))
  const term = fakeTerm(9999)
  supervisorAttach(supervisor, portWith('pty-rp'), id('pty-rp'), term)
  assert.deepEqual(await parked, resultOk)
  assert.deepEqual(term.writes, ['early'])
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_is_idempotent_for_already_dropped_session', async () => {
  const supervisor = supervisorCreate()
  const term = fakeTerm(9999)
  const completions = []
  const p = portWith('pty-idem')
  portAddMailboxSender(p, (item) => completions.push(item))
  supervisorAttach(supervisor, p, id('pty-idem'), term)
  term.exitCb({ exitCode: 0 })
  await new Promise((resolve) => setImmediate(resolve))
  term.exitCb({ exitCode: 0 })
  assert.equal(completions.length, 1)
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_noop_when_session_already_closed', () => {
  const supervisor = supervisorCreate()
  const term = fakeTerm(9999)
  const completions = []
  const p = portWith('pty-ec')
  portAddMailboxSender(p, (item) => completions.push(item))
  supervisorAttach(supervisor, p, id('pty-ec'), term)
  sessionSetClosed(supervisorGet(supervisor, id('pty-ec')), true)
  term.exitCb({ exitCode: 0 })
  assert.equal(completions.length, 0)
  assert.notEqual(supervisorTryGet(supervisor, id('pty-ec')), null)
})

// ── cancelAll ────────────────────────────────────────────────────────────────

test('WHAT[PROC-006] SUPERVISOR_cancelAll_kills_live_sessions_and_skips_closed_or_null_backends', async () => {
  const process = child()
  try {
    const supervisor = supervisorCreate()
    const live = sessionCreate('pty-l1', { pid: process.pid })
    const closed = sessionCreate('pty-l2', { pid: 2_147_483_647 })
    sessionSetClosed(closed, true)
    supervisorAdd(supervisor, id('pty-l1'), live)
    supervisorAdd(supervisor, id('pty-l2'), closed)
    supervisorAdd(supervisor, id('pty-l3'), sessionCreate('pty-l3', null))
    supervisorCancelAll(supervisor)
    await waitExit(process)
    assert.ok(died(process), 'live session killed')
  } finally { killChild(process) }
})
