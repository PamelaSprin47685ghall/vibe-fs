import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

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
const agent = { Name: 'devops' }
const bytes = new TextEncoder().encode('hi')
const write = ptyCommandWrite(bytes)
const signalOf = (name) => ptyCommandSignal(name)
const id = (value) => ptyId(value)
const forkDefault = (port, value, command = 'echo hi') =>
  portFork(port, command, 'devops', value === undefined ? undefined : id(value), undefined)
const success = { ok: true, value: undefined }
const failure = (error) => ({ ok: false, error })
const exitSignal = () => {
  let resolve
  const promise = new Promise((r) => { resolve = r })
  return { promise, resolve }
}
const completedItem = (port, pid, outcome) => {
  const got = []
  portAddMailboxSender(port, (item) => got.push(item))
  portComplete(port, pid, outcome)
  return got[0]
}

test('WHAT[process-execution-002] PORT_send_term_kill_int_marks_abort_for_the_next_completion', async () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { spawn } = await import("node:child_process");

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
  portFork(p, 'echo hi', 'devops', id(value), undefined)
  return p
}

test('WHAT[process-execution-002] SUPERVISOR_applyLive_signal_kills_the_real_process_group_or_process', async () => {
  const process = child()
  try {
    const supervisor = supervisorCreate()
    supervisorAdd(supervisor, id('pty-k'), sessionCreate('pty-k', { pid: process.pid }))
    assert.deepEqual(await supervisorApplyLive(supervisor, port(), id('pty-k'), ptyCommandSignal('KILL')), resultOk)
    await waitExit(process)
    assert.ok(died(process), 'child was killed')
  } finally { killChild(process) }
})
test('WHAT[process-execution-002] SUPERVISOR_applyLive_signal_unknown_pid_becomes_error', async () => {
  const supervisor = supervisorCreate()
  supervisorAdd(supervisor, id('pty-ku'), sessionCreate('pty-ku', { pid: 2_147_483_647 }))
  const result = await supervisorApplyLive(supervisor, port(), id('pty-ku'), ptyCommandSignal('TERM'))
  assert.equal(result.ok, false)
  assert.match(result.error, /ESRCH/)
})
}
