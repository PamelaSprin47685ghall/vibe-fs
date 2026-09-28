import assert from 'node:assert/strict'
import test from 'node:test'
import { record, commitFrame, withReopenableJournal } from './support/record.mjs'

test('WHAT[work-record-005] actual Recent work advances by RecordCoverage within a turn while PrefixCoverage remains zero', async () => {
  await withReopenableJournal(async handle => {
    const session = 'record-mid-turn'
    await record.captureOpening(handle, session, 'task', [])
    await record.captureProjection(handle, session, { messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'task' }] },
      { role: 'assistant', parts: [
        { kind: 'reasoning', text: 'earlier analysis' },
        { kind: 'text', text: 'last formal statement' },
      ] },
    ] })
    const before = await record.lifecycleWorkRecord(handle, session, false)
    assert.ok(before.includes('earlier analysis'))
    assert.ok(before.includes('last formal statement'))
    await commitFrame(handle, session, { from: 0, through: 3, body: 'recorded investigation', id: 'partial' })
    const after = await record.lifecycleWorkRecord(handle, session, false)
    assert.equal(after, 'Chronicle\nrecorded investigation\n\nRecent work\nassistant: last formal statement')
  })
})
