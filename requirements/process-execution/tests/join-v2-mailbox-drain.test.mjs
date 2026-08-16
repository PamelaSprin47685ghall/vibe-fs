// Process owner mailbox surface: PTY completions drain FIFO and bounded.

import assert from 'node:assert/strict'
import test from 'node:test'

const {
  completionMailboxCreate,
  completionMailboxPublishPty,
  completionMailboxDrainPty,
  completionMailboxPendingCount,
  maxJoinBatch,
  ptyExited,
} = await import('../../../dist/Process/Surface.js')

test('WHAT[PROC-008] EXEC_018_drain_available_returns_two_completions_in_publish_order', () => {
  const mailbox = completionMailboxCreate()
  completionMailboxPublishPty(mailbox, ptyExited('a', 'closed'))
  completionMailboxPublishPty(mailbox, ptyExited('b', 'closed'))
  const batch = completionMailboxDrainPty(mailbox, maxJoinBatch)
  assert.equal(batch.length, 2)
  assert.equal(batch[0].ptyId, 'a')
  assert.equal(batch[1].ptyId, 'b')
  assert.equal(completionMailboxPendingCount(mailbox), 0)
})
