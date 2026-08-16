// Split from tests/unit/execution/join-v2-wire.test.mjs (cutover Wave 2a);
// owner: process-execution. PROC-010 terminal/run 完成 = exit_code + 输出：
// PTY completion 的 wire 是自然语言「ended」+ exit_code，无 pty_id/status DTO
// （其余 join wire 断言 → delegation）。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentCompletion,
  joinResultRenderer,
  nonEmptyBatch,
} from '../../verification-system/tests/support/domain.mjs'

test('WHAT[PROC-010] EXEC_004_pty_completion_is_natural_language_plus_exit_code', () => {
  const run = agentCompletion.completedRun({
    runId: 'pty-9',
    agentId: 'pty-9',
    agentName: '',
    role: 'DevOps',
    workRecord: '0',
  })
  const ptyRuntime = joinResultRenderer.stubRuntime({ ptyRunIds: new Set(['pty-9']) })
  const wire = joinResultRenderer.renderCompletedBatch(
    ptyRuntime,
    nonEmptyBatch.ofHeadTail(run),
    (id) => (id === 'pty-9' ? 'shell' : 'Terminal'),
  )

  assert.match(wire, /# shell has ended\./)
  assert.match(wire, /exit_code = 0/)
  assert.ok(!wire.includes('pty_id'))
})
