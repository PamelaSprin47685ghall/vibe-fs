// Process owner wire surface: PTY completion is natural language plus exit code.

import assert from 'node:assert/strict'
import test from 'node:test'

const { renderPtyCompletion } = await import('../../../dist/Process/Surface.js')

test('WHAT[PROC-010] EXEC_004_pty_completion_is_natural_language_plus_exit_code', () => {
  const wire = renderPtyCompletion('shell', 'pty-9', 'ended', 0)
  assert.match(wire, /# shell has ended\./)
  assert.match(wire, /exit_code = 0/)
  assert.ok(!wire.includes('pty_id'))
})
