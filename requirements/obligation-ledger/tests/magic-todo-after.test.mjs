import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { caseOf } from '../../verification-system/tests/support/domain.mjs'
import {
  assignmentDelivery,
  AssignmentDelivery,
} from '../../../dist/Domain/MagicTodoAfter.js'

const here = dirname(fileURLToPath(import.meta.url))
const runtimeSource = join(
  here,
  '../../../src/Wanxiangshu/Application/Review/DedicatedTodoReviewerRuntime.fs',
)

test('HOST-021 T1 assignment is AgentOwnerRoot, never a busy nudge', () => {
  assert.equal(caseOf(assignmentDelivery(false, true)), 'OwnerRoot')
  assert.equal(caseOf(assignmentDelivery(false, false)), 'OwnerRoot')
  assert.equal(assignmentDelivery(false, true), AssignmentDelivery.OwnerRoot)
})

test('HOST-021 T1 retry after claim waits for XTrace head instead of sending twice', () => {
  assert.equal(caseOf(assignmentDelivery(true, true)), 'AwaitHead')
})

test('HOST-021 T2+ assignment continues the dedicated reviewer', () => {
  assert.equal(caseOf(assignmentDelivery(true, false)), 'Continuation')
})

test('HOST-021 T1 must not second-Fork assignment onto a deferSend pending run', () => {
  const source = readFileSync(runtimeSource, 'utf8')
  assert.equal(
    source.includes('renderedPrompt = assignmentText'),
    false,
    'second Fork of assignmentText hits sendToExistingChild busy-nudge (no ActiveLogicalRun)',
  )
  assert.match(source, /DiscardDeferredFirstPrompt/)
  assert.match(source, /assignmentDelivery/)
})
