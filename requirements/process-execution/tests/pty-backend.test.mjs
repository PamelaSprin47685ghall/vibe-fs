// Process owner API: the backend port's spawn-failure contract.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  backendCreatePort,
  ptyId,
  ptyIdView,
  ptyCommandWrite,
  ptyCommandSignal,
  portAddMailboxSender,
  portFork,
  portExists,
  portKnown,
  portSend,
  portRead,
  portCloseAll,
} = await import('../../../dist/Process/Surface.js')

const write = ptyCommandWrite(new TextEncoder().encode('x'))
const signal = ptyCommandSignal('KILL')
const settle = () => new Promise((resolve) => setImmediate(resolve))
const failure = (error) => ({ ok: false, error })

test('WHAT[PROC-001] BACKEND_createPort_returns_a_working_port', () => {
  const port = backendCreatePort()
  assert.ok(port, 'port exists')
  assert.equal(portExists(port, ptyId('pty-b')), false)
})

test('WHAT[PROC-003] BACKEND_fork_without_bun_pty_fails_spawn_and_publishes_failed', async () => {
  const port = backendCreatePort()
  const got = []
  portAddMailboxSender(port, (item) => got.push(item))
  const pid = portFork(port, 'echo hi', 'fast-distiller', ptyId('pty-sf'), undefined)
  await settle()

  assert.equal(ptyIdView(pid), 'pty-sf')
  assert.equal(got.length, 1, 'one completion published')
  const item = got[0]
  assert.equal(item.kind, 'PtyFailed')
  assert.equal(item.ptyId, 'pty-sf')
  assert.equal(item.code, 'ERROR')
  assert.match(item.message, /^PTY spawn failed: /)
  assert.equal(item.closed, true)
})

test('WHAT[PROC-001] BACKEND_failed_fork_leaves_unknown_active_but_known_closed', async () => {
  const port = backendCreatePort()
  const pid = portFork(port, 'echo hi', 'fast-distiller', ptyId('pty-fa'), undefined)
  await settle()
  assert.equal(portExists(port, pid), false)
  assert.equal(portKnown(port, pid), true)
})

test('WHAT[PROC-001] BACKEND_generated_id_also_fails_cleanly', async () => {
  const port = backendCreatePort()
  const got = []
  portAddMailboxSender(port, (item) => got.push(item))
  const pid = portFork(port, 'echo hi', 'fast-distiller', undefined, undefined)
  await settle()
  assert.match(ptyIdView(pid), /^pty-[0-9a-f]{8}$/)
  assert.equal(got[0].ptyId, ptyIdView(pid))
})

test('WHAT[PROC-001] BACKEND_send_after_failed_fork_reports_closed', async () => {
  const port = backendCreatePort()
  const pid = portFork(port, 'echo hi', 'fast-distiller', ptyId('pty-wc'), undefined)
  await settle()
  assert.deepEqual(await portSend(port, pid, write), failure('PTY closed'))
})

test('WHAT[PROC-001] BACKEND_send_on_never_forked_id_is_unknown', async () => {
  const port = backendCreatePort()
  assert.deepEqual(await portSend(port, ptyId('pty-uk'), write), failure('Unknown PTY id: pty-uk'))
})

test('WHAT[PROC-001] BACKEND_read_after_failed_fork_returns_empty_closed', async () => {
  const port = backendCreatePort()
  const pid = portFork(port, 'echo hi', 'fast-distiller', ptyId('pty-rf'), undefined)
  await settle()
  assert.deepEqual(await portRead(port, pid), { ok: true, value: { output: '', closed: true } })
})

test('WHAT[PROC-001] BACKEND_read_never_forked_is_an_error', async () => {
  const port = backendCreatePort()
  assert.deepEqual(await portRead(port, ptyId('pty-rn')), failure('Unknown PTY id: pty-rn'))
})

test('WHAT[PROC-001] BACKEND_close_all_with_nothing_active_resolves', async () => {
  await portCloseAll(backendCreatePort(), 0)
})

test('WHAT[PROC-001] BACKEND_ports_are_isolated_from_each_other', async () => {
  const a = backendCreatePort()
  const b = backendCreatePort()
  const pid = portFork(a, 'echo hi', 'fast-distiller', ptyId('pty-iso'), undefined)
  await settle()
  assert.equal(portKnown(a, pid), true)
  assert.equal(portKnown(b, pid), false)
  assert.deepEqual(await portSend(b, pid, write), failure('Unknown PTY id: pty-iso'))
})

test('WHAT[PROC-003] BACKEND_concurrent_failed_forks_each_publish_one_completion', async () => {
  const port = backendCreatePort()
  const got = []
  portAddMailboxSender(port, (item) => got.push(item))
  portFork(port, 'cmd one', 'fast-distiller', ptyId('pty-f1'), undefined)
  portFork(port, 'cmd two', 'fast-distiller', ptyId('pty-f2'), undefined)
  await settle()
  await settle()
  assert.deepEqual(got.map((item) => item.ptyId).sort(), ['pty-f1', 'pty-f2'])
})

test('WHAT[PROC-001] BACKEND_signal_on_failed_id_is_rejected_as_closed', async () => {
  const port = backendCreatePort()
  const pid = portFork(port, 'echo hi', 'fast-distiller', ptyId('pty-sg'), undefined)
  await settle()
  assert.deepEqual(await portSend(port, pid, signal), failure('PTY closed'))
})
