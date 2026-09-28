import test from 'node:test'
import assert from 'node:assert/strict'
import { record, withReopenableJournal } from './support/record.mjs'

test('WHAT[work-record-015] actual materializer retains work after Opening while later tool turns and trace head advance', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-structural-floor'
    await record.captureOpening(handle, session, 'original charge', [])
    const messages = [
      { role: 'user', parts: [{ kind: 'text', text: 'original charge' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'early investigation' }] },
    ]
    await record.captureProjection(handle, session, { messages })
    assert.equal(await record.lifecycleWorkRecord(handle, session, false), 'Recent work\nassistant: early investigation')
    await record.captureProjection(handle, session, { messages: [
      ...messages,
      { role: 'assistant', parts: [{ kind: 'tool-call', callId: 'phase-call', name: 'assume', args: '{"update":".","todos":[]}' }] },
      { role: 'tool', parts: [{ kind: 'tool-result', callId: 'phase-call', result: 'accepted' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'later investigation' }] },
    ] })
    const result = await record.lifecycleWorkRecord(handle, session, false)
    assert.ok(result.includes('early investigation'))
    assert.ok(result.includes('later investigation'))
    assert.equal(result.includes('original charge'), false)
    assert.equal(result.includes('[tool call]'), false)
    assert.equal(await record.lifecycleWorkRecord(await reopen(), session, false), result)
  })
})
