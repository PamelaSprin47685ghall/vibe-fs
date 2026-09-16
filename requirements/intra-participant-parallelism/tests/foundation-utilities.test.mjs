import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-001] TaskResult and Parallel helpers are pure utilities with no authority side effects', () => {
  const taskResult = read('src/Wanxiangshu/Foundation/TaskResult.fs')
  const parallel = read('src/Wanxiangshu/Foundation/Parallel.fs')
  assert.match(taskResult, /taskResult\b/)
  assert.match(parallel, /mapBounded\b/)
  assert.doesNotMatch(taskResult, /AgentJournal|appendAgent|AbortSession|Obligation/)
  assert.doesNotMatch(parallel, /AgentJournal|appendAgent|AbortSession|Obligation/)
})
