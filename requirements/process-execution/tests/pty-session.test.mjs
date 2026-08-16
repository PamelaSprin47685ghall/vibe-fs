// tests/unit/process/pty-session.test.mjs — PtySession aggregate defaults.

import assert from 'node:assert/strict'
import test from 'node:test'

const { PtySessionModule_create } = await import('../../../dist/Process/PtySession.js')

test('WHAT[PROC-009] PTY_SESSION_create_sets_id_and_backend', () => {
  const backend = { pid: 1234 }
  const s = PtySessionModule_create('pty-abc', backend)
  assert.equal(s.PtyId, 'pty-abc')
  assert.equal(s.Backend, backend)
})

test('WHAT[PROC-009] PTY_SESSION_create_defaults_open_empty_and_pending', () => {
  const s = PtySessionModule_create('pty-def', null)
  assert.equal(s.Backend, null)
  assert.equal(s.Closed, false)
  assert.equal(s.OutputBuffer.toString(), '')
  assert.equal(s.Pending.length, 0)
})

test('WHAT[PROC-009] PTY_SESSION_exit_completion_starts_unresolved', async () => {
  const s = PtySessionModule_create('pty-ghi', null)
  const winner = await Promise.race([
    s.ExitCompletion.get_Task().then(() => 'exit'),
    Promise.resolve('still-pending'),
  ])
  assert.equal(winner, 'still-pending')
})

test('WHAT[PROC-009] PTY_SESSION_mutable_state_roundtrips', () => {
  const s = PtySessionModule_create('pty-jkl', null)
  assert.equal(s.OutputBuffer.toString(), '')

  s.Closed = true
  assert.equal(s.Closed, true)

  const backend = { pid: 42 }
  s.Backend = backend
  assert.equal(s.Backend, backend)

  s.Pending.push(['cmd', null])
  assert.equal(s.Pending.length, 1)

  s.ExitCompletion.SetResult(undefined)
  return s.ExitCompletion.get_Task()
})
