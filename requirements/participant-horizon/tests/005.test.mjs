import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]/

const completed = (over = {}) => ({ kind: 'completed', agentId: 'a1', agentName: 'coder', role: 'Coder', runId: 'run-a1', workRecord: '', ...over })

const failed = (over = {}) => ({ kind: 'failed', agentId: 'a1', agentName: 'engineer', role: 'Engineer', runId: 'run-a1', code: 'E1', message: 'boom', ...over })

const pty = (kind, over = {}) => ({ kind, ptyId: 'pty-1', terminalLabel: 'npm test', outcome: 'exit 0', code: '', message: '', ...over, ...(kind ? { kind } : {}) })

const assertClean = (wire, label) => assert.ok(!LEGACY_DTO.test(wire), `${label}: ${wire}`)

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
