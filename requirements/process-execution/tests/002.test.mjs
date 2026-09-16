import assert from 'node:assert/strict'
import test from 'node:test'
import { spawn } from 'node:child_process'

const {
  createPtyPort,
  portAddMailboxSender,
  portComplete,
  portFork,
  portSend,
  ptyCommandSignal,
  ptyId,
  sessionCreate,
  supervisorAdd,
  supervisorApplyLive,
  supervisorCreate,
} = await import('../../../dist/Process/Surface.js')

const id = (value) => ptyId(value)
const signalOf = (name) => ptyCommandSignal(name)
const forkDefault = (port, value, command = 'echo hi') =>
  portFork(port, command, 'distiller', value === undefined ? undefined : id(value), undefined)
const success = { ok: true, value: undefined }
const port = () => createPtyPort({})
const resultOk = { ok: true, value: undefined }

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
