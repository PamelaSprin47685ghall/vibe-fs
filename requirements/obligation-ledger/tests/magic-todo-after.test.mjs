import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

const here = dirname(fileURLToPath(import.meta.url))
const runtimeSource = join(
  here,
  '../../../src/Wanxiangshu/Mission/Review/DedicatedTodoRuntime.fs',
)

test('WHAT[OBLIGATION-LEDGER-026] first accepted checkpoint reviewer assignment is AgentOwnerRoot, independent of plan commitment', () => {
  assert.equal(todo.assignmentDelivery(false), 'OwnerRoot')
})

test('WHAT[OBLIGATION-LEDGER-020] later checkpoint assignment continues the dedicated reviewer', () => {
  assert.equal(todo.assignmentDelivery(true), 'Continuation')
})

test('WHAT[OBLIGATION-LEDGER-026] reentry decides resend from durable dispatch evidence, never an XTrace head watermark', () => {
  const source = readFileSync(runtimeSource, 'utf8')
  assert.equal(source.includes('waitHeadAdvanced'), false, 'head-wait must not exist')
  assert.equal(source.includes('AwaitHead'), false, 'AwaitHead delivery mode must not exist')
  assert.match(source, /DispatchStatus/, 'resend admission reads dispatch evidence')
})

test('WHAT[OBLIGATION-LEDGER-020] first assignment must not second-Fork onto a deferSend pending run', () => {
  const source = readFileSync(runtimeSource, 'utf8')
  assert.equal(
    source.includes('renderedPrompt = assignmentText'),
    false,
    'a pending first assignment must not be sent through the second-Fork busy-nudge path',
  )
})

test('WHAT[OBLIGATION-LEDGER-026] the assignment is durable before the physical send freezes the reviewer frontier', () => {
  const source = readFileSync(runtimeSource, 'utf8')
  const durableCall = source.search(/appendAssigned\s*\n\s*journal[\s\S]{0,400}reviewerHead journal enlisted\.ReviewerSessionId/)
  const sendCall = source.indexOf('sendFirstPrompt')
  assert.ok(durableCall > 0, 'the durable TodoProcessReviewAssigned append call must exist')
  assert.ok(sendCall > 0, 'the physical assignment send must exist')
  assert.ok(
    durableCall < sendCall,
    'TodoProcessReviewAssigned must be appended before the assignment prompt is sent, freezing the pre-dispatch frontier',
  )
})

test('WHAT[OBLIGATION-LEDGER-020] a new checkpoint reactivates a retired reviewer work-unit without replacing the logical reviewer', () => {
  const source = readFileSync(runtimeSource, 'utf8')
  assert.match(source, /ensureReusableReviewerWorkUnit/, 'runtime must name the work-unit reactivation promise')
  assert.match(source, /HandleLifecycle\.Retired/, 'retired previous work-unit is a handled reuse case')
  assert.match(source, /HandleLinked/, 'reactivation must be durable, not only AdoptChild process memory')
})
