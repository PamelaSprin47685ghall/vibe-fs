// tests/unit/process/pty-supervisor.test.mjs — PtySupervisor state machine:
// session registry, spawn loader, pending-command queue, applyLive dispatch,
// attach/onData/onExit lifecycle, cancelAll. Driven with a fake term object
// (bun-pty cannot load under plain node).

import assert from 'node:assert/strict'
import test from 'node:test'
import { spawn } from 'node:child_process'

import { caseOf, lib, payloadOf, resultOf, okResult, errorResult } from '../../verification-system/tests/support/domain.mjs'

const { StringBuilder__Append_Z721C83C5 } = await lib('System.Text.js')

const {
  PtySupervisorModule_create,
  PtySupervisorModule_add,
  PtySupervisorModule_tryGet,
  PtySupervisorModule_get,
  PtySupervisorModule_remove,
  PtySupervisorModule_list,
  PtySupervisorModule_signalName,
  PtySupervisorModule_ensureSpawn,
  PtySupervisorModule_spawnSync,
  PtySupervisorModule_failPending,
  PtySupervisorModule_takePending,
  PtySupervisorModule_drop,
  PtySupervisorModule_applyLive,
  PtySupervisorModule_attach,
  PtySupervisorModule_cancelAll,
} = await import('../../../dist/Process/PtySupervisor.js')

const { PtySessionModule_create } = await import('../../../dist/Process/PtySession.js')
const { PtyId_Create_Z721C83C5, PtyId__get_Value, PtyCommand, PtySignal } = await import(
  '../../../dist/Process/PtyTypes.js'
)
const {
  PtyPort,
  PtyPort__Fork_515E235E,
  PtyPort__Read_Z33F80F6F,
  PtyPort__ReadResult_3DD67D20,
  PtyPort__AddMailboxSender_6A484C48,
} = await import('../../../dist/Process/Pty.js')

const id = (v) => PtyId_Create_Z721C83C5(v)
// A real PtyPort is required: attach/applyLive call module-level PtyPort
// functions that reach into gate/active/readWaiters.
const port = () => new PtyPort()

// A task-completion-source stand-in with the fable surface the supervisor uses.
const tcs = () => {
  let resolve
  const task = new Promise((r) => {
    resolve = r
  })
  return {
    get_Task: () => task,
    SetResult: (v) => resolve(v),
  }
}

// Spawn a throwaway child so killProcessTree has a real pid to signal.
const child = () => spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'])
const killChild = (c) => {
  if (c && c.exitCode === null && c.signalCode === null) c.kill('SIGKILL')
}
// SIGKILL death reports exitCode null + signalCode 'SIGKILL'.
const died = (c) => c.exitCode !== null || c.signalCode !== null
const waitExit = (c) =>
  Promise.race([
    new Promise((r) => c.once('exit', r)),
    new Promise((r) => setTimeout(r, 2000)),
  ])

// ── session registry ─────────────────────────────────────────────────────────

test('WHAT[PROC-007] SUPERVISOR_add_tryGet_get_roundtrip', () => {
  const s = PtySupervisorModule_create()
  const session = PtySessionModule_create('pty-a', null)
  PtySupervisorModule_add(s, id('pty-a'), session)
  assert.equal(PtySupervisorModule_tryGet(s, id('pty-a')), session)
  assert.equal(PtySupervisorModule_get(s, id('pty-a')), session)
})

test('WHAT[PROC-007] SUPERVISOR_tryGet_missing_returns_none_and_get_throws', () => {
  const s = PtySupervisorModule_create()
  assert.equal(PtySupervisorModule_tryGet(s, id('pty-missing')), undefined)
  assert.throws(() => PtySupervisorModule_get(s, id('pty-missing')), /Unknown PTY id: pty-missing/)
})

test('WHAT[PROC-007] SUPERVISOR_remove_drops_the_session', () => {
  const s = PtySupervisorModule_create()
  const session = PtySessionModule_create('pty-r', null)
  PtySupervisorModule_add(s, id('pty-r'), session)
  PtySupervisorModule_remove(s, id('pty-r'))
  assert.equal(PtySupervisorModule_tryGet(s, id('pty-r')), undefined)
})

test('WHAT[PROC-007] SUPERVISOR_list_returns_added_ids_only', () => {
  const s = PtySupervisorModule_create()
  PtySupervisorModule_add(s, id('pty-1'), PtySessionModule_create('pty-1', null))
  PtySupervisorModule_add(s, id('pty-2'), PtySessionModule_create('pty-2', null))
  const listed = [...PtySupervisorModule_list(s)].map((x) => PtyId__get_Value(x)).sort()
  assert.deepEqual(listed, ['pty-1', 'pty-2'])
  PtySupervisorModule_remove(s, id('pty-1'))
  assert.equal([...PtySupervisorModule_list(s)].length, 1)
})

// ── signal name codec ────────────────────────────────────────────────────────

test('WHAT[PROC-001] SUPERVISOR_signalName_maps_every_signal_to_a_kill_name', () => {
  assert.equal(PtySupervisorModule_signalName(PtySignal.Terminate), 'SIGTERM')
  assert.equal(PtySupervisorModule_signalName(PtySignal.Kill), 'SIGKILL')
  assert.equal(PtySupervisorModule_signalName(PtySignal.Interrupt), 'SIGINT')
  assert.equal(PtySupervisorModule_signalName(PtySignal.Hangup), 'SIGHUP')
  assert.equal(PtySupervisorModule_signalName(PtySignal.Quit), 'SIGQUIT')
  assert.equal(PtySupervisorModule_signalName(PtySignal.User1), 'SIGUSR1')
  assert.equal(PtySupervisorModule_signalName(PtySignal.User2), 'SIGUSR2')
})

// ── spawn loader ─────────────────────────────────────────────────────────────

test('WHAT[PROC-001] SUPERVISOR_ensureSpawn_reuses_one_loader_and_faults_without_bun_pty', async () => {
  const s = PtySupervisorModule_create()
  const first = PtySupervisorModule_ensureSpawn(s)
  const second = PtySupervisorModule_ensureSpawn(s)
  assert.equal(first, second, 'loader task is cached')
  await assert.rejects(first) // bun-pty cannot load under plain node
  // A faulted load never installs SpawnFn, so spawnSync stays unavailable.
  assert.throws(() => PtySupervisorModule_spawnSync(s, 'echo hi', ''), /bun-pty is not loaded/)
})

test('WHAT[PROC-001] SUPERVISOR_spawnSync_fails_fast_when_loader_never_ran', () => {
  const s = PtySupervisorModule_create()
  assert.throws(() => PtySupervisorModule_spawnSync(s, 'echo hi', ''), /bun-pty is not loaded/)
})

test('WHAT[PROC-001] SUPERVISOR_spawnSync_invokes_sh_lc_with_fixed_options', () => {
  const s = PtySupervisorModule_create()
  let seen
  s.SpawnFn = (sh, args, options) => {
    seen = [sh, args, options]
    return { pid: 4242 }
  }
  const term = PtySupervisorModule_spawnSync(s, 'echo hi', '/tmp/work')
  assert.equal(term.pid, 4242)
  assert.equal(seen[0], 'sh')
  assert.deepEqual(seen[1], ['-lc', 'echo hi'])
  assert.equal(seen[2].name, 'xterm-256color')
  assert.equal(seen[2].cols, 80)
  assert.equal(seen[2].rows, 24)
  assert.equal(seen[2].cwd, '/tmp/work')
})

test('WHAT[PROC-001] SUPERVISOR_spawnSync_defaults_cwd_to_process_cwd', () => {
  const s = PtySupervisorModule_create()
  let seenCwd
  s.SpawnFn = (sh, args, options) => {
    seenCwd = options.cwd
    return {}
  }
  PtySupervisorModule_spawnSync(s, 'ls', '')
  assert.equal(seenCwd, process.cwd())
})

// ── pending queue ────────────────────────────────────────────────────────────

test('WHAT[PROC-007] SUPERVISOR_takePending_returns_and_clears_the_queue', () => {
  const s = PtySupervisorModule_create()
  const session = PtySessionModule_create('pty-q', null)
  session.Pending.push(['first', null])
  session.Pending.push(['second', null])
  PtySupervisorModule_add(s, id('pty-q'), session)

  const taken = [...PtySupervisorModule_takePending(s, id('pty-q'))]
  assert.equal(taken.length, 2)
  assert.equal(taken[0][0], 'first')
  assert.equal(session.Pending.length, 0)
  assert.deepEqual([...PtySupervisorModule_takePending(s, id('pty-q'))], [])
})

test('WHAT[PROC-007] SUPERVISOR_takePending_unknown_id_is_empty', () => {
  const s = PtySupervisorModule_create()
  assert.deepEqual([...PtySupervisorModule_takePending(s, id('pty-nope'))], [])
})

test('WHAT[PROC-007] SUPERVISOR_drop_removes_session_and_returns_pending', () => {
  const s = PtySupervisorModule_create()
  const session = PtySessionModule_create('pty-d', null)
  session.Pending.push(['queued', null])
  PtySupervisorModule_add(s, id('pty-d'), session)

  const dropped = [...PtySupervisorModule_drop(s, id('pty-d'))]
  assert.equal(dropped.length, 1)
  assert.equal(PtySupervisorModule_tryGet(s, id('pty-d')), undefined)
  assert.deepEqual([...PtySupervisorModule_drop(s, id('pty-d'))], [])
})

test('WHAT[PROC-003] SUPERVISOR_failPending_resolves_every_tcs_with_the_reason', async () => {
  const a = tcs()
  const entries = [
    ['write', a],
    ['write2', null],
  ]
  PtySupervisorModule_failPending(entries, 'PTY exited before command was applied')
  const result = resultOf(await a.get_Task())
  assert.equal(result.ok, false)
  assert.equal(result.error, 'PTY exited before command was applied')
})

// ── applyLive ────────────────────────────────────────────────────────────────

test('WHAT[PROC-001] SUPERVISOR_applyLive_closed_session_short_circuits_ok', async () => {
  const s = PtySupervisorModule_create()
  const session = PtySessionModule_create('pty-c', null)
  session.Closed = true
  PtySupervisorModule_add(s, id('pty-c'), session)
  const result = resultOf(await PtySupervisorModule_applyLive(s, port(), id('pty-c'), PtyCommand.Read))
  assert.equal(result.ok, true)
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_write_forwards_utf8_to_backend', async () => {
  const s = PtySupervisorModule_create()
  const writes = []
  const backend = { write: (t) => writes.push(t) }
  PtySupervisorModule_add(s, id('pty-w'), PtySessionModule_create('pty-w', backend))

  const bytes = new TextEncoder().encode('héllo')
  const result = resultOf(
    await PtySupervisorModule_applyLive(s, port(), id('pty-w'), new PtyCommand(1, [bytes]))
  )
  assert.equal(result.ok, true)
  assert.deepEqual(writes, ['héllo'])
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_write_backend_error_becomes_error_result', async () => {
  const s = PtySupervisorModule_create()
  const backend = {
    write: () => {
      throw new Error('EPIPE')
    },
  }
  PtySupervisorModule_add(s, id('pty-we'), PtySessionModule_create('pty-we', backend))
  const result = resultOf(
    await PtySupervisorModule_applyLive(s, port(), id('pty-we'), new PtyCommand(1, [new Uint8Array(0)]))
  )
  assert.equal(result.ok, false)
  assert.equal(result.error, 'EPIPE')
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_read_drains_buffer_into_port', async () => {
  const s = PtySupervisorModule_create()
  const p = portWith('pty-re')
  const session = PtySessionModule_create('pty-re', {})
  StringBuilder__Append_Z721C83C5(session.OutputBuffer, 'partial output')
  PtySupervisorModule_add(s, id('pty-re'), session)

  const result = resultOf(await PtySupervisorModule_applyLive(s, p, id('pty-re'), PtyCommand.Read))
  assert.equal(result.ok, true)
  assert.equal(session.OutputBuffer.toString(), '', 'buffer drained')
  // The drained text was handed to the port: a subsequent Read parks, so
  // resolve it through ReadResult and check what came out.
  const read = PtyPort__Read_Z33F80F6F(p, id('pty-re'))
  PtyPort__ReadResult_3DD67D20(p, id('pty-re'), 'after', false)
  const readResult = resultOf(await read)
  assert.deepEqual([readResult.ok, readResult.value[0], readResult.value[1]], [true, 'after', false])
})

test('WHAT[PROC-002] SUPERVISOR_applyLive_signal_kills_the_real_process_group_or_process', async () => {
  const c = child()
  try {
    const s = PtySupervisorModule_create()
    PtySupervisorModule_add(s, id('pty-k'), PtySessionModule_create('pty-k', { pid: c.pid }))
    const result = resultOf(
      await PtySupervisorModule_applyLive(s, port(), id('pty-k'), new PtyCommand(3, [PtySignal.Kill]))
    )
    assert.equal(result.ok, true)
    await waitExit(c)
    assert.ok(died(c), 'child was killed')
  } finally {
    killChild(c)
  }
})

test('WHAT[PROC-002] SUPERVISOR_applyLive_signal_unknown_pid_becomes_error', async () => {
  const s = PtySupervisorModule_create()
  PtySupervisorModule_add(s, id('pty-ku'), PtySessionModule_create('pty-ku', { pid: 2147483647 }))
  const result = resultOf(
    await PtySupervisorModule_applyLive(s, port(), id('pty-ku'), new PtyCommand(3, [PtySignal.Terminate]))
  )
  assert.equal(result.ok, false)
  assert.match(result.error, /ESRCH/)
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_resize_swallows_backend_errors', async () => {
  const s = PtySupervisorModule_create()
  const resizes = []
  const backend = {
    resize: (w, h) => {
      resizes.push([w, h])
      throw new Error('nope')
    },
  }
  PtySupervisorModule_add(s, id('pty-z'), PtySessionModule_create('pty-z', backend))
  const result = resultOf(
    await PtySupervisorModule_applyLive(s, port(), id('pty-z'), new PtyCommand(4, [120, 40]))
  )
  assert.equal(result.ok, true, 'resize errors are swallowed')
  assert.deepEqual(resizes, [[120, 40]])
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_spawn_on_live_backend_is_a_noop', async () => {
  const s = PtySupervisorModule_create()
  PtySupervisorModule_add(s, id('pty-sp'), PtySessionModule_create('pty-sp', {}))
  const result = resultOf(
    await PtySupervisorModule_applyLive(s, port(), id('pty-sp'), new PtyCommand(0, ['x', '']))
  )
  assert.equal(result.ok, true)
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_write_without_backend_parks_until_resolved', async () => {
  const s = PtySupervisorModule_create()
  const session = PtySessionModule_create('pty-p', null)
  PtySupervisorModule_add(s, id('pty-p'), session)

  const pending = PtySupervisorModule_applyLive(s, port(), id('pty-p'), new PtyCommand(1, [new Uint8Array(0)]))
  const entries = [...PtySupervisorModule_takePending(s, id('pty-p'))]
  assert.equal(entries.length, 1)
  assert.equal(caseOf(entries[0][0]), 'Write')

  entries[0][1].SetResult(okResult(undefined))
  const result = resultOf(await pending)
  assert.equal(result.ok, true)
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_parked_write_resolves_with_error', async () => {
  const s = PtySupervisorModule_create()
  const session = PtySessionModule_create('pty-pe', null)
  PtySupervisorModule_add(s, id('pty-pe'), session)

  const pending = PtySupervisorModule_applyLive(s, port(), id('pty-pe'), new PtyCommand(1, [new Uint8Array(0)]))
  const entries = [...PtySupervisorModule_takePending(s, id('pty-pe'))]
  entries[0][1].SetResult(errorResult('backend vanished'))
  const result = resultOf(await pending)
  assert.equal(result.ok, false)
  assert.equal(result.error, 'backend vanished')
})

test('WHAT[PROC-001] SUPERVISOR_applyLive_non_write_commands_without_backend_return_ok_immediately', async () => {
  const s = PtySupervisorModule_create()
  const session = PtySessionModule_create('pty-nb', null)
  PtySupervisorModule_add(s, id('pty-nb'), session)

  for (const cmd of [PtyCommand.Read, new PtyCommand(3, [PtySignal.Hangup]), new PtyCommand(4, [10, 10])]) {
    const result = resultOf(await PtySupervisorModule_applyLive(s, port(), id('pty-nb'), cmd))
    assert.equal(result.ok, true)
  }
  assert.equal(session.Pending.length, 3, 'each command queued for post-attach replay')
})

// ── attach / onData / onExit ─────────────────────────────────────────────────

const fakeTerm = (pid) => {
  const term = {
    pid,
    dataCb: null,
    exitCb: null,
    writes: [],
    resizes: [],
    onData: (cb) => {
      term.dataCb = cb
    },
    onExit: (cb) => {
      term.exitCb = cb
    },
    write: (t) => term.writes.push(t),
    resize: (w, h) => term.resizes.push([w, h]),
  }
  return term
}

// A port that already knows the id (Forked) so attach skips the SIGKILL sweep.
const portWith = (pid) => {
  const p = new PtyPort()
  PtyPort__Fork_515E235E(p, 'echo hi', { Name: 'fast-distiller' }, id(pid))
  return p
}

test('WHAT[PROC-007] SUPERVISOR_attach_registers_live_session_and_forwards_onData_to_buffer', async () => {
  const c = child()
  try {
    const s = PtySupervisorModule_create()
    const term = fakeTerm(c.pid)
    const exit = tcs()
    PtySupervisorModule_attach(s, portWith('pty-at'), id('pty-at'), term, exit)

    const session = PtySupervisorModule_get(s, id('pty-at'))
    assert.equal(session.Backend, term)
    assert.equal(session.Closed, false)

    term.dataCb('hello ')
    term.dataCb('world')
    assert.equal(session.OutputBuffer.toString(), 'hello world')
  } finally {
    killChild(c)
  }
})

test('WHAT[PROC-007] SUPERVISOR_attach_onData_ignored_after_session_closed', async () => {
  const s = PtySupervisorModule_create()
  const term = fakeTerm(9999)
  PtySupervisorModule_attach(s, portWith('pty-ic'), id('pty-ic'), term, tcs())
  const session = PtySupervisorModule_get(s, id('pty-ic'))
  session.Closed = true
  term.dataCb('late data')
  assert.equal(session.OutputBuffer.toString(), '')
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_completes_exit_publishes_closed_and_drops_session', async () => {
  const s = PtySupervisorModule_create()
  const term = fakeTerm(9999)
  const exit = tcs()
  const completions = []
  const p = portWith('pty-ex')
  PtyPort__AddMailboxSender_6A484C48(p, (item) => completions.push(item))
  PtySupervisorModule_attach(s, p, id('pty-ex'), term, exit)

  term.exitCb({ exitCode: 0 })
  await exit.get_Task()

  assert.equal(PtySupervisorModule_tryGet(s, id('pty-ex')), undefined, 'session dropped')
  assert.equal(completions.length, 1)
  const item = completions[0]
  assert.equal(caseOf(item), 'PtyExited')
  const exitInfo = payloadOf(item)
  assert.equal(exitInfo.PtyId, 'pty-ex')
  assert.equal(exitInfo.Outcome, 'closed', 'empty buffer completes as PtyOutcome.Closed')
  assert.equal(exitInfo.Closed, true)
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_publishes_residual_output', async () => {
  const s = PtySupervisorModule_create()
  const term = fakeTerm(9999)
  const exit = tcs()
  const completions = []
  const p = portWith('pty-ro')
  PtyPort__AddMailboxSender_6A484C48(p, (item) => completions.push(item))
  PtySupervisorModule_attach(s, p, id('pty-ro'), term, exit)
  term.dataCb('final words')
  term.exitCb({ exitCode: 1 })
  await exit.get_Task()

  assert.equal(caseOf(completions[0]), 'PtyExited')
  assert.equal(payloadOf(completions[0]).Outcome, 'final words')
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_fails_pending_writes_and_parked_read', async () => {
  const s = PtySupervisorModule_create()
  const parkedWrite = tcs()
  const term = fakeTerm(9999)
  const exit = tcs()
  const p = portWith('pty-fp')
  PtySupervisorModule_attach(s, p, id('pty-fp'), term, exit)

  // Park a read and a write on the live session, then kill the session:
  // the exit sweep must fail both with the canonical reasons.
  const parkedRead = PtyPort__Read_Z33F80F6F(p, id('pty-fp'))
  PtySupervisorModule_get(s, id('pty-fp')).Pending.push([
    new PtyCommand(1, [new Uint8Array(0)]),
    parkedWrite,
  ])
  term.exitCb({ exitCode: 0 })

  const readResult = resultOf(await parkedRead)
  assert.equal(readResult.ok, false)
  assert.equal(readResult.error, 'PTY exited before read completed')

  const writeResult = resultOf(await parkedWrite.get_Task())
  assert.equal(writeResult.ok, false)
  assert.equal(writeResult.error, 'PTY exited before command was applied')
})

test('WHAT[PROC-006] SUPERVISOR_attach_without_port_entry_kills_the_term', async () => {
  const c = child()
  try {
    const s = PtySupervisorModule_create()
    const term = fakeTerm(c.pid)
    const exit = tcs()
    PtySupervisorModule_attach(s, new PtyPort(), id('pty-nk'), term, exit)
    await waitExit(c)
    assert.ok(died(c), 'unregistered attach SIGKILLs the process tree')
  } finally {
    killChild(c)
  }
})

test('WHAT[PROC-007] SUPERVISOR_attach_replays_pending_writes_onto_the_live_backend', async () => {
  const s = PtySupervisorModule_create()
  const session = PtySessionModule_create('pty-rp', null)
  PtySupervisorModule_add(s, id('pty-rp'), session)

  const parked = PtySupervisorModule_applyLive(
    s,
    new PtyPort(),
    id('pty-rp'),
    new PtyCommand(1, [new TextEncoder().encode('early')])
  )
  const term = fakeTerm(9999)
  PtySupervisorModule_attach(s, portWith('pty-rp'), id('pty-rp'), term, tcs())

  const result = resultOf(await parked)
  assert.equal(result.ok, true)
  assert.deepEqual(term.writes, ['early'])
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_is_idempotent_for_already_dropped_session', async () => {
  const s = PtySupervisorModule_create()
  const term = fakeTerm(9999)
  const exit = tcs()
  const completions = []
  const p = portWith('pty-idem')
  PtyPort__AddMailboxSender_6A484C48(p, (item) => completions.push(item))
  PtySupervisorModule_attach(s, p, id('pty-idem'), term, exit)
  term.exitCb({ exitCode: 0 })
  await exit.get_Task()
  term.exitCb({ exitCode: 0 }) // second exit event: session already dropped
  assert.equal(completions.length, 1, 'no duplicate completion')
})

test('WHAT[PROC-003] SUPERVISOR_attach_onExit_noop_when_session_already_closed', async () => {
  const s = PtySupervisorModule_create()
  const term = fakeTerm(9999)
  const completions = []
  const p = portWith('pty-ec')
  PtyPort__AddMailboxSender_6A484C48(p, (item) => completions.push(item))
  PtySupervisorModule_attach(s, p, id('pty-ec'), term, tcs())
  PtySupervisorModule_get(s, id('pty-ec')).Closed = true
  term.exitCb({ exitCode: 0 })
  assert.equal(completions.length, 0, 'closed session ignores exit events')
  assert.notEqual(PtySupervisorModule_tryGet(s, id('pty-ec')), undefined, 'session kept')
})

// ── cancelAll ────────────────────────────────────────────────────────────────

test('WHAT[PROC-006] SUPERVISOR_cancelAll_kills_live_sessions_and_skips_closed_or_null_backends', async () => {
  const c = child()
  try {
    const s = PtySupervisorModule_create()
    const live = PtySessionModule_create('pty-l1', { pid: c.pid })
    const closed = PtySessionModule_create('pty-l2', { pid: 2147483647 })
    closed.Closed = true
    const nullBackend = PtySessionModule_create('pty-l3', null)
    PtySupervisorModule_add(s, id('pty-l1'), live)
    PtySupervisorModule_add(s, id('pty-l2'), closed)
    PtySupervisorModule_add(s, id('pty-l3'), nullBackend)

    PtySupervisorModule_cancelAll(s) // must not throw on any session
    await waitExit(c)
    assert.ok(died(c), 'live session killed')
  } finally {
    killChild(c)
  }
})
