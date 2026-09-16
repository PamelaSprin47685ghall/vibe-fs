import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const pty = (kind, over = {}) => ({ kind, ptyId: 'pty-1', terminalLabel: 'npm test', outcome: 'exit 0', code: '', message: '', ...over, ...(kind ? { kind } : {}) })

test('WHAT[PARTICIPANT-HORIZON-005] MISC_join_render_batch_pty_exit_code_observation', () => {
  const wire = join.renderBatch('english', [pty('pty-exited')])
  assert.match(wire, /# npm test has ended\./)
  assert.match(wire, /exit_code = 0/)
  assert.ok(!wire.includes('pty_id'))
})

test('WHAT[PARTICIPANT-HORIZON-005] MISC_join_render_batch_pty_failure_output_observation', () => {
  const wire = join.renderBatch('english', [pty('pty-failed', { ptyId: 'pty-2', outcome: 'crash', code: 'RC', message: 'kaboom' })])
  assert.match(wire, /# npm test has ended\./)
  assert.match(wire, /output = "kaboom"/)
  assert.ok(!wire.includes('code ='))
})

test('WHAT[PARTICIPANT-HORIZON-005] MISC_join_render_completed_pty_exit_observation', () => {
  const wire = join.renderBatch('english', [pty('pty-exited', { ptyId: 'pty-9', terminalLabel: 'shell' })])
  assert.match(wire, /# shell has ended\./)
  assert.ok(!wire.includes('pty_id'))
})
