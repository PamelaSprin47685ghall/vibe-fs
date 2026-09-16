import assert from 'node:assert/strict'
import test from 'node:test'
import * as log from '../../../dist/Persistence/LocalProcessEventLogSurface.js'

test('WHAT[DURABLE-EVENTS-005] DURABLE_EVENTS_005_one_process_is_one_unbounded_writer_file_with_no_segments', () => {
  const writer = log.createProcessWriter('writer-1')
  assert.equal(log.writerFilePath(writer), '.git/wanxiang/events/writer-1.ndjson')
  assert.equal(log.hasSegments(writer), false)
})

test('WHAT[DURABLE-EVENTS-005] DURABLE_EVENTS_005_each_process_writer_id_names_a_distinct_file_without_machine_identity', () => {
  const w1 = log.createProcessWriter('w1')
  const w2 = log.createProcessWriter('w2')
  assert.notEqual(log.writerFilePath(w1), log.writerFilePath(w2))
  assert.doesNotMatch(log.writerFilePath(w1), /hostname|machine|ip/)
})
