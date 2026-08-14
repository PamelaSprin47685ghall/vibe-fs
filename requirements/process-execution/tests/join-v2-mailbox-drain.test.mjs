// Split from tests/unit/execution/join-v2-mailbox.test.mjs (cutover Wave 2a);
// owner: process-execution. PROC-008 完成事实双通道：CompletionMailbox 的 drain
// 保持 publish FIFO（PTY 队列完成经 mailbox FIFO 送达；其余 mailbox/中断/批次
// 断言 → delegation）。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentCompletion,
  completionMailbox,
  maxJoinBatch,
} from '../../verification-system/tests/support/domain.mjs'

const run = (id) =>
  agentCompletion.completedRun({
    runId: `run-${id}`,
    agentId: id,
    agentName: `agent-${id}`,
    workRecord: `wr-${id}`,
  })

// ── 4: two completions drained in one batch ──────────────────────────────────

test('EXEC_018_drain_available_returns_two_completions_in_publish_order', () => {
  const box = completionMailbox.create(() => true)
  completionMailbox.publish(box, run('a'))
  completionMailbox.publish(box, run('b'))
  const batch = completionMailbox.drainAvailable(box, maxJoinBatch)
  assert.equal(batch.length, 2)
  assert.equal(batch[0].AgentId, 'a')
  assert.equal(batch[1].AgentId, 'b')
  assert.equal(completionMailbox.pendingCount(box), 0)
})
