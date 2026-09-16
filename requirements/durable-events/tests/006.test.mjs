import assert from 'node:assert/strict'
import test from 'node:test'
import * as jw from '../../../dist/Persistence/Journal/EventStoreJournalWriterSurface.js'

test('WHAT[DURABLE-EVENTS-006] append_adds_one_local_line_and_Current_is_already_integrated', () => {
  const writer = jw.createJournalWriter()
  const res = jw.appendJournalFact(writer, { factType: 'F1', data: 'hello' })
  assert.equal(res.ok, true)
  assert.equal(jw.currentIntegrated(writer).length, 1)
  assert.equal(jw.currentIntegrated(writer)[0].factType, 'F1')
})
