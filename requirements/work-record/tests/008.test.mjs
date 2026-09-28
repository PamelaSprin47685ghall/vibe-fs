import assert from 'node:assert/strict'
import test from 'node:test'
import { record, withReopenableJournal } from './support/record.mjs'

test('WHAT[work-record-008] actual Opening rendering preserves assignment bytes and existing requirement numbers', async () => {
  await withReopenableJournal(async (handle, reopen) => {
    const session = 'record-raw-opening'
    const assignment = '  Original task.\r\nKeep these bytes.  '
    const requirements = ['7. Keep the original identifier.', '42. 不重编号。']
    await record.captureOpening(handle, session, assignment, requirements)
    const expected = `Opening\n${assignment}\n${requirements.join('\n')}`
    assert.equal(await record.lifecycleWorkRecord(handle, session, true), expected)
    assert.equal(await record.lifecycleWorkRecord(await reopen(), session, true), expected)
  })
})

test.todo('WHAT[work-record-008] actual committed Opening is materialized from its complete original XTrace interval, including accepted planning and clarifications; separate opening fields alone do not prove provenance; GAP-109')
