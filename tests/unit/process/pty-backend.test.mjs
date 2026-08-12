// tests/unit/process/pty-backend.test.mjs — PtyBackend.createPort under plain
// node: bun-pty cannot load, so every Fork takes the spawn-failure path
// (register exit, fail parked reads, drop the session, publish PtyFailed).

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf, payloadOf, resultOf } from '../support/domain.mjs'

const { createPort } = await import('../../../dist/Process/PtyBackend.js')

const {
  PtyPort__AddMailboxSender_15902874,
  PtyPort__Fork_515E235E,
  PtyPort__Exists_Z33F80F6F,
  PtyPort__Known_Z33F80F6F,
  PtyPort__Send_Z13021A56,
  PtyPort__Read_Z33F80F6F,
  PtyPort__CloseAll_71136F3F,
} = await import('../../../dist/Process/Pty.js')

const { PtyId_Create_Z721C83C5, PtyId__get_Value, PtyCommand, PtySignal } = await import(
  '../../../dist/Process/PtyTypes.js'
)

const id = (v) => PtyId_Create_Z721C83C5(v)
const agent = { Name: 'fast-distiller' }
const write = new PtyCommand(1, [new TextEncoder().encode('x')])

// The spawn failure is delivered through the async handler; let the task run.
const settle = () => new Promise((r) => setImmediate(r))

test('BACKEND_createPort_returns_a_working_port', () => {
  const port = createPort()
  assert.ok(port, 'port exists')
  assert.equal(PtyPort__Exists_Z33F80F6F(port, id('pty-b')), false)
})

test('BACKEND_fork_without_bun_pty_fails_spawn_and_publishes_failed', async () => {
  const port = createPort()
  const got = []
  PtyPort__AddMailboxSender_15902874(port, (item) => got.push(item))
  const pid = PtyPort__Fork_515E235E(port, 'echo hi', agent, id('pty-sf'))
  await settle()

  assert.equal(PtyId__get_Value(pid), 'pty-sf')
  assert.equal(got.length, 1, 'one completion published')
  const item = got[0]
  assert.equal(caseOf(item), 'PtyFailed')
  const info = payloadOf(item)
  assert.equal(info.PtyId, 'pty-sf')
  assert.equal(info.Code, 'ERROR')
  assert.match(info.Message, /^PTY spawn failed: /)
  assert.equal(info.Closed, true)
})

test('BACKEND_failed_fork_leaves_unknown_active_but_known_closed', async () => {
  const port = createPort()
  const pid = PtyPort__Fork_515E235E(port, 'echo hi', agent, id('pty-fa'))
  await settle()
  assert.equal(PtyPort__Exists_Z33F80F6F(port, pid), false)
  assert.equal(PtyPort__Known_Z33F80F6F(port, pid), true)
})

test('BACKEND_generated_id_also_fails_cleanly', async () => {
  const port = createPort()
  const got = []
  PtyPort__AddMailboxSender_15902874(port, (item) => got.push(item))
  const pid = PtyPort__Fork_515E235E(port, 'echo hi', agent)
  await settle()
  assert.match(PtyId__get_Value(pid), /^pty-[0-9a-f]{8}$/)
  assert.equal(payloadOf(got[0]).PtyId, PtyId__get_Value(pid))
})

test('BACKEND_send_after_failed_fork_reports_closed', async () => {
  const port = createPort()
  const pid = PtyPort__Fork_515E235E(port, 'echo hi', agent, id('pty-wc'))
  await settle()
  const r = resultOf(await PtyPort__Send_Z13021A56(port, pid, write))
  assert.equal(r.ok, false)
  assert.equal(r.error, 'PTY closed')
})

test('BACKEND_send_on_never_forked_id_is_unknown', async () => {
  const port = createPort()
  const r = resultOf(await PtyPort__Send_Z13021A56(port, id('pty-uk'), write))
  assert.equal(r.ok, false)
  assert.equal(r.error, 'Unknown PTY id: pty-uk')
})

test('BACKEND_read_after_failed_fork_returns_empty_closed', async () => {
  const port = createPort()
  const pid = PtyPort__Fork_515E235E(port, 'echo hi', agent, id('pty-rf'))
  await settle()
  const r = resultOf(await PtyPort__Read_Z33F80F6F(port, pid))
  assert.deepEqual([r.ok, r.value], [true, ['', true]])
})

test('BACKEND_read_never_forked_is_an_error', async () => {
  const port = createPort()
  const r = resultOf(await PtyPort__Read_Z33F80F6F(port, id('pty-rn')))
  assert.equal(r.ok, false)
  assert.equal(r.error, 'Unknown PTY id: pty-rn')
})

test('BACKEND_close_all_with_nothing_active_resolves', async () => {
  const port = createPort()
  await PtyPort__CloseAll_71136F3F(port, 0)
})

test('BACKEND_ports_are_isolated_from_each_other', async () => {
  const a = createPort()
  const b = createPort()
  const pid = PtyPort__Fork_515E235E(a, 'echo hi', agent, id('pty-iso'))
  await settle()
  assert.equal(PtyPort__Known_Z33F80F6F(a, pid), true)
  assert.equal(PtyPort__Known_Z33F80F6F(b, pid), false)
  const r = resultOf(await PtyPort__Send_Z13021A56(b, pid, write))
  assert.equal(r.error, 'Unknown PTY id: pty-iso')
})

test('BACKEND_concurrent_failed_forks_each_publish_one_completion', async () => {
  const port = createPort()
  const got = []
  PtyPort__AddMailboxSender_15902874(port, (item) => got.push(item))
  PtyPort__Fork_515E235E(port, 'cmd one', agent, id('pty-f1'))
  PtyPort__Fork_515E235E(port, 'cmd two', agent, id('pty-f2'))
  await settle()
  await settle()
  const messages = got.map(payloadOf).map((x) => x.PtyId).sort()
  assert.deepEqual(messages, ['pty-f1', 'pty-f2'])
})

test('BACKEND_signal_on_failed_id_is_rejected_as_closed', async () => {
  const port = createPort()
  const pid = PtyPort__Fork_515E235E(port, 'echo hi', agent, id('pty-sg'))
  await settle()
  const r = resultOf(await PtyPort__Send_Z13021A56(port, pid, new PtyCommand(3, [PtySignal.Kill])))
  assert.equal(r.ok, false)
  assert.equal(r.error, 'PTY closed')
})
