import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

test('WHAT[delegation-014] commission renderer preserves supplied verdict order and renders no field DTO', () => {
  for (const language of ['english', 'zh-CN']) {
    const published = join.renderOrchestratorBatch(language, ['Published']).trim()
    const review = join.renderOrchestratorBatch(language, ['NeedsReview']).trim()
    assert.notEqual(published, review)
    const wire = join.renderOrchestratorBatch(language, ['Published', 'NeedsReview'])
    assert.ok(wire.indexOf(published) >= 0)
    assert.ok(wire.indexOf(review) > wire.indexOf(published))
    assert.deepEqual(parseToml(wire), {})
    assert.equal(join.renderOrchestratorBatch(language, []), '')
  }
})

test.todo('WHAT[delegation-014] actual commission entry wires job completion into the proven FIFO mailbox and durable result consumption (GAP-153)')

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
