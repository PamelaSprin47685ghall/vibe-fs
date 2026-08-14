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

test('HOST-021 first accepted checkpoint reviewer assignment is AgentOwnerRoot, independent of plan commitment', () => {
  assert.equal(caseOf(assignmentDelivery(false, true)), 'OwnerRoot')
  assert.equal(caseOf(assignmentDelivery(false, false)), 'OwnerRoot')
  assert.equal(assignmentDelivery(false, true), AssignmentDelivery.OwnerRoot)
})

test('HOST-021 first reviewer assignment retry after claim waits for XTrace head instead of sending twice', () => {
  assert.equal(caseOf(assignmentDelivery(true, true)), 'AwaitHead')
})

test('HOST-021 later checkpoint assignment continues the dedicated reviewer', () => {
  assert.equal(caseOf(assignmentDelivery(true, false)), 'Continuation')
})

test('HOST-021 first assignment must not second-Fork onto a deferSend pending run', () => {
  const source = readFileSync(runtimeSource, 'utf8')
  assert.equal(
    source.includes('renderedPrompt = assignmentText'),
    false,
    'second Fork of assignmentText hits sendToExistingChild busy-nudge (no ActiveLogicalRun)',
  )
  assert.match(source, /DiscardDeferredFirstPrompt/)
  assert.match(source, /assignmentDelivery/)
})

test('OBLIGATION-LEDGER-020 a new checkpoint reactivates a retired reviewer work-unit without replacing the logical reviewer', () => {
  const source = readFileSync(runtimeSource, 'utf8')
  assert.match(source, /ensureReusableReviewerWorkUnit/, 'runtime must name the work-unit reactivation promise')
  assert.match(source, /HandleLifecycle\.Retired/, 'retired previous work-unit is a handled reuse case')
  assert.match(source, /HandleLinked/, 'reactivation must be durable, not only AdoptChild process memory')
  assert.match(source, /checkpoint\.Assignment[\s\S]*Some _[\s\S]*tryConclude/, 'already-assigned checkpoints must converge; they must not be reopened')
})
