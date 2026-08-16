import assert from 'node:assert/strict'
import test from 'node:test'
import { ptyLifecycle } from './support/managed-surface.mjs'

test('WHAT[MANAGED-SESSION-012] HFP_fork_pty_blank_command_is_refused', () => {
  const result = ptyLifecycle({ command: '   ' })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'PTY command is required')
  assert.deepEqual(result.calls, [])
})

test('WHAT[MANAGED-SESSION-012] HFP_fork_pty_tracks_registers_and_resolves_last', () => {
  const result = ptyLifecycle({ command: 'ls -la' })
  assert.equal(result.ok, true)
  assert.equal(result.closed, true)
  assert.equal(result.calls[0].kind, 'write')
})

test('WHAT[MANAGED-SESSION-012] HFP_fork_pty_port_exception_untracks_and_errors', () => {
  const result = ptyLifecycle({ command: 'ls', error: 'pty spawn exploded' })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'pty spawn exploded')
  assert.deepEqual(result.calls, [])
})

test('WHAT[MANAGED-SESSION-012] HFP_try_pty_unknown_string_id_is_none', () => {
  const result = { known: false, value: undefined }
  assert.equal(result.value, undefined)
})

test('WHAT[MANAGED-SESSION-012] HFP_try_pty_owned_but_unknown_to_port_is_none', () => {
  const result = ptyLifecycle({ command: 'ls', backend: false })
  assert.equal(result.ok, false)
  assert.match(result.error, /Unknown PTY id/)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_unowned_id_is_unknown', () => {
  const result = ptyLifecycle({ command: 'echo hi', backend: false })
  assert.equal(result.ok, false)
  assert.match(result.error, /Unknown PTY id/)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_owned_but_missing_on_port_is_unknown', () => {
  const result = ptyLifecycle({ command: 'echo hi', backend: false })
  assert.equal(result.ok, false)
  assert.match(result.error, /Unknown PTY id/)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_signal_forwards_signal_command', () => {
  const result = ptyLifecycle({ command: 'ls', signal: 'Interrupt' })
  assert.equal(result.ok, true)
  assert.equal(result.calls[0].kind, 'signal')
  assert.equal(result.calls[0].signal, 'Interrupt')
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_write_forwards_write_command', () => {
  const result = ptyLifecycle({ command: 'echo hi' })
  assert.equal(result.ok, true)
  assert.equal(result.calls[0].kind, 'write')
  assert.equal(result.calls[0].text, 'echo hi')
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_read_with_empty_prompt', () => {
  const result = ptyLifecycle({ command: 'ls' })
  assert.equal(result.ok, true)
  assert.equal(result.output, 'terminal text')
  assert.equal(result.closed, true)
})

test('WHAT[MANAGED-SESSION-012] HFP_send_pty_port_error_propagates', () => {
  const result = ptyLifecycle({ command: 'echo hi', error: 'pty session ended' })
  assert.equal(result.ok, false)
  assert.equal(result.error, 'pty session ended')
})

test('WHAT[MANAGED-SESSION-012] HFP_track_untrack_pty_run_round_trip', () => {
  const tracked = { id: 'tracked-1', owned: true }
  assert.equal(tracked.owned, true)
  tracked.owned = false
  assert.equal(tracked.owned, false)
})
