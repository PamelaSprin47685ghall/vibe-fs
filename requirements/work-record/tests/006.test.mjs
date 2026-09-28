import assert from 'node:assert/strict'
import test from 'node:test'
import { record, withReopenableJournal } from './support/record.mjs'

test('WHAT[work-record-006] omitting Opening never erases the canonical fact, including after journal reopen', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-opening-preserved'
    assert.equal(await record.lifecycleWorkRecord(handle, session, false), null)
    await record.captureOpening(handle, session, 'original\r\ncharge', ['original requirement'])
    const withOpening = await record.lifecycleWorkRecord(handle, session, true)
    const withoutOpening = await record.lifecycleWorkRecord(handle, session, false)
    assert.ok(withOpening.includes('original\r\ncharge'))
    assert.equal(withoutOpening, '')
    assert.equal(await record.lifecycleWorkRecord(await reopen(), session, true), withOpening)
  })
})
