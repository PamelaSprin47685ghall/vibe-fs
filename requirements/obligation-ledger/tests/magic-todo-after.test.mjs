import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { caseOf } from '../../verification-system/tests/support/domain.mjs'
import {
  assignmentDelivery,
  AssignmentDelivery,
} from '../../../dist/Mission/Obligation/Todo/After.js'

const here = dirname(fileURLToPath(import.meta.url))
const runtimeSource = join(
  here,
  '../../../src/Wanxiangshu/Mission/Review/DedicatedTodoRuntime.fs',
)

test('HOST-021 first accepted checkpoint reviewer assignment is AgentOwnerRoot, independent of plan commitment', () => {
  assert.equal(caseOf(assignmentDelivery(false)), 'OwnerRoot')
  assert.equal(assignmentDelivery(false), AssignmentDelivery.OwnerRoot)
})

test('HOST-021 later checkpoint assignment continues the dedicated reviewer', () => {
  assert.equal(caseOf(assignmentDelivery(true)), 'Continuation')
})

test('HOST-021 reentry decides resend from durable dispatch evidence, never an XTrace head watermark', () => {
  // AwaitHead is gone: XTrace append order is not request causal order, so a
  // head watermark can neither prove nor disprove that THIS assignment was
  // delivered (REVIEW-018). The runtime must admit the physical send from
  // PromptAuthority dispatch evidence instead.
  const source = readFileSync(runtimeSource, 'utf8')
  assert.equal(source.includes('waitHeadAdvanced'), false, 'head-wait must not exist')
  assert.equal(source.includes('AwaitHead'), false, 'AwaitHead delivery mode must not exist')
  assert.match(source, /DispatchStatus/, 'resend admission reads dispatch evidence')
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

test('HOST-021 the assignment is durable before the physical send freezes the reviewer frontier', () => {
  const source = readFileSync(runtimeSource, 'utf8')
  // The CALL passes the pre-send frozen head; the definition's parameter is
  // `reviewWorkStart`, so this pattern only matches the call site.
  const durableCall = source.search(/appendAssigned\s*\n\s*journal[\s\S]{0,400}reviewerHead journal enlisted\.ReviewerSessionId/)
  const sendCall = source.indexOf('sendFirstPrompt')
  assert.ok(durableCall > 0, 'the durable TodoProcessReviewAssigned append call must exist')
  assert.ok(sendCall > 0, 'the physical assignment send must exist')
  assert.ok(
    durableCall < sendCall,
    'TodoProcessReviewAssigned must be appended before the assignment prompt is sent, freezing the pre-dispatch frontier',
  )
})

test('OBLIGATION-LEDGER-020 a new checkpoint reactivates a retired reviewer work-unit without replacing the logical reviewer', () => {
  const source = readFileSync(runtimeSource, 'utf8')
  assert.match(source, /ensureReusableReviewerWorkUnit/, 'runtime must name the work-unit reactivation promise')
  assert.match(source, /HandleLifecycle\.Retired/, 'retired previous work-unit is a handled reuse case')
  assert.match(source, /HandleLinked/, 'reactivation must be durable, not only AdoptChild process memory')
  assert.match(source, /checkpoint\.Assignment[\s\S]*tryConclude/, 'already-assigned checkpoints must converge; they must not be reopened')
})
