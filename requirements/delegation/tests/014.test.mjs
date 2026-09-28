import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const join = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");


test('WHAT[delegation-014] JOIN_COMPLETION_batch_preserves_order_and_bounded_work_records', () => {
  const wire = join.renderBatch('english', [
    { kind: 'completed', agentId: 'a1', agentName: 'Ada', role: 'Coder', runId: 'run-a1', workRecord: 'First evidence.' },
    { kind: 'completed', agentId: 'a2', agentName: 'Bob', role: 'DevOps', runId: 'run-a2', workRecord: 'Second evidence.' }
  ])
  assert.match(wire, /Ada has returned/)
  assert.match(wire, /Bob has returned/)
  assert.ok(wire.indexOf('Ada') < wire.indexOf('Bob'))
  assert.match(wire, /First evidence/)
  assert.match(wire, /Second evidence/)
  assert.doesNotMatch(wire, /\[\[result\]\]|\[error\]|work_record\s*=/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");
const join = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]|work_record\s*=/
const completed = (id, name, record = '') => ({ kind: 'completed', agentId: id, agentName: name, role: 'Coder', runId: `run-${id}`, workRecord: record })

test('WHAT[delegation-014] JOIN_V2_abandoned_agent_is_natural_language', () => {
  const wire = join.renderBatch('english', [{ kind: 'abandoned', agentId: 'a2', agentName: 'engineer', reason: 'abandoned' }])
  assert.match(wire, /did not return/)
  assert.ok(!LEGACY_DTO.test(wire))
})
}

{
const change = await import('../../../dist/Change/Surface.js')
const assert = (await import('node:assert/strict')).default

test('WHAT[delegation-014] commission verdict mailbox drains FIFO batches up to the global cap with exact remainder', async () => {
  const cap = change.verdictMaxBatch()
  assert.ok(Number.isInteger(cap) && cap > 0)
  const mailbox = change.createVerdictMailbox()
  const total = cap + 3
  for (let i = 0; i < total; i += 1) {
    change.verdictMailboxStartJob(mailbox)
    change.verdictMailboxPublish(mailbox, { kind: 'Published', jobId: `job-${i}`, head: `head-${i}` })
  }
  assert.equal(change.verdictMailboxPendingCount(mailbox), total)
  const first = await change.verdictMailboxJoinAvailable(mailbox, 1000, change.createVerdictInterrupt())
  assert.equal(first.kind, 'ResultsAvailable')
  assert.equal(first.count, cap)
  assert.deepEqual(first.verdicts.map(v => v.detail), Array.from({ length: cap }, (_, i) => `head-${i}`))
  assert.equal(change.verdictMailboxPendingCount(mailbox), 3)
  const second = await change.verdictMailboxJoinAvailable(mailbox, 1000, change.createVerdictInterrupt())
  assert.equal(second.kind, 'ResultsAvailable')
  assert.equal(second.count, 3)
  assert.deepEqual(second.verdicts.map(v => v.detail), [`head-${cap}`, `head-${cap + 1}`, `head-${cap + 2}`])
  assert.equal(change.verdictMailboxPendingCount(mailbox), 0)
})

test('WHAT[delegation-014] commission verdict mailbox reports Empty when idle with no pending verdicts', async () => {
  const mailbox = change.createVerdictMailbox()
  const out = await change.verdictMailboxJoinAvailable(mailbox, 8, change.createVerdictInterrupt())
  assert.equal(out.kind, 'ResultsAvailable')
  assert.equal(out.count, 1)
  assert.equal(out.verdicts[0].kind, 'Empty')
  assert.equal(change.verdictMailboxPendingCount(mailbox), 0)
})
}
