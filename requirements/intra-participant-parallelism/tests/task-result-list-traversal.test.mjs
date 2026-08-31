import assert from 'node:assert/strict'
import test from 'node:test'

import { TaskResultListSurface_traverseM as traverseM } from '../../../dist/Foundation/FsToolkitFableCompat.js'

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-016] TASK_RESULT_LIST_traverseM_calls_mapper_once_per_input_in_order_stops_at_first_Error_and_skips_empty', async () => {
  const completedCalls = []
  const completed = await traverseM(async (item) => {
    completedCalls.push(item)
    return true
  }, ['first', 'second', 'third'])

  assert.deepEqual(completedCalls, ['first', 'second', 'third'])
  assert.deepEqual(completed, ['Ok', 'first', 'second', 'third'])

  const failedCalls = []
  const failed = await traverseM(async (item) => {
    failedCalls.push(item)
    return item !== 'second'
  }, ['first', 'second', 'never-called'])

  assert.deepEqual(failedCalls, ['first', 'second'])
  assert.deepEqual(failed, ['Error', 'second'])

  let emptyCalls = 0
  const empty = await traverseM(async () => {
    emptyCalls += 1
    return true
  }, [])

  assert.equal(emptyCalls, 0)
  assert.deepEqual(empty, ['Ok'])
})
