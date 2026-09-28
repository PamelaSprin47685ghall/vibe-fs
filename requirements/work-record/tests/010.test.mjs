import assert from 'node:assert/strict'
import test from 'node:test'
import { record, withReopenableJournal } from './support/record.mjs'

test('WHAT[work-record-010] repeated bounded materialization of the same durable facts is deterministic', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-repeat'
    await record.captureOpening(handle, session, 'task', [])
    await record.captureProjection(handle, session, { messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'task' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'bounded work\r\n中文' }] },
    ] })
    const range = { StartInclusive: { Sequence: 1 }, EndExclusive: { Sequence: 3 } }
    const first = await record.lifecycleWorkRecordBounded(handle, session, range)
    assert.equal(typeof first, 'string')
    assert.ok(first.includes('bounded work'))
    assert.equal(await record.lifecycleWorkRecordBounded(handle, session, range), first)
    assert.equal(await record.lifecycleWorkRecordBounded(await reopen(), session, range), first)
  })
})

test.todo('WHAT[work-record-010] actual inspect and fork/join return the same bounded record for the same invocation facts; materializer determinism alone does not prove both consumers; GAP-109')
