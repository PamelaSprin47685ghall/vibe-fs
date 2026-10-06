import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const scores = ['PERFECT', 'REVISE', 'PERFECT', 'REVISE', 'PERFECT', 'PERFECT', 'REVISE', 'PERFECT']

const open = (state, snapshot = 'snapshot-1') =>
  relay.openIncumbency(state, 'road-1', 'inc-1', snapshot, 'authority-1')

test('WHAT[relay-assessment-002] second assessment in one iteration is rejected without overwriting the first', () => {
  const opened = open(relay.empty())
  const assessed = relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-1', ...scores)
  assert.equal(assessed.ok, true)

  const replayed = relay.assess(
    assessed.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...scores,
  )
  assert.equal(replayed.ok, true)

  const conflicted = relay.assess(
    assessed.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill('PERFECT'),
  )
  assert.deepEqual(conflicted, { ok: false, error: 'AssessmentReplayConflict' })

  const second = relay.assess(
    assessed.state,
    'road-1',
    'inc-1',
    'assessment-2',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill('PERFECT'),
  )
  assert.deepEqual(second, { ok: false, error: 'AssessmentAlreadySubmitted' })
})

test('WHAT[relay-assessment-002] cross-iteration replay of another iteration assessment is rejected', () => {
  const opened = open(relay.empty())
  const assessed = relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-1', ...scores)
  assert.equal(assessed.ok, true)
  const retired = relay.retireContinue(assessed.state, 'road-1', 'inc-1', 'ret-1', 'run-1', 'tool-1', 'snapshot-1')
  assert.equal(retired.ok, true)
  const next = relay.openIncumbency(retired.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(next.ok, true)
  const replayed = relay.assess(
    next.state,
    'road-1',
    'inc-2',
    'assessment-1',
    'snapshot-2',
    'authority-1',
    ...scores,
  )
  assert.deepEqual(replayed, { ok: false, error: 'AssessmentReplayConflict' })
})

const {withReview, scores: reviewScores} = await import('./support/plugin.mjs')
const {withSuccessor} = await import('../../relay-context-projection/tests/support/cut.mjs')

test('WHAT[relay-assessment-002] actual tool exact replay returns the accepted result', async () => {
  await withReview(async ({execute, hooks, session}) => {
    const input = reviewScores('REVISE')
    const first = await execute(input)
    assert.match(first, /recorded = true/)
    const replay = await hooks.tool.review.execute(input, {sessionID: session, callID: 'review-call', messageID: 'review-run', agent: 'manager'})
    assert.equal(replay, first)
  })
})

test('WHAT[relay-assessment-002] same identity with changed input is a replay conflict and the accepted result stays', async () => {
  await withReview(async ({execute, hooks, session}) => {
    const input = reviewScores('REVISE')
    const first = await execute(input)
    assert.match(first, /recorded = true/)
    const changed = await hooks.tool.review.execute(reviewScores('PERFECT'), {sessionID: session, callID: 'review-call', messageID: 'review-run', agent: 'manager'})
    assert.match(changed, /recorded = false/)
    const exact = await hooks.tool.review.execute(input, {sessionID: session, callID: 'review-call', messageID: 'review-run', agent: 'manager'})
    assert.equal(exact, first)
  })
})

test('WHAT[relay-assessment-002] same input under a new call id is already-submitted', async () => {
  await withReview(async ({execute, hooks, session}) => {
    const input = reviewScores('REVISE')
    const first = await execute(input)
    assert.match(first, /recorded = true/)
    const second = await execute(input, {call: 'review-call-2', run: 'review-run-2'})
    assert.match(second, /recorded = false/)
    const exact = await hooks.tool.review.execute(input, {sessionID: session, callID: 'review-call', messageID: 'review-run', agent: 'manager'})
    assert.equal(exact, first)
  })
})

test('WHAT[relay-assessment-002] cross-incumbency replay of a retired review call is rejected', async () => {
  await withSuccessor(async ({execute, hooks, session}) => {
    const input = reviewScores('REVISE')
    // withSuccessor already recorded this exact call in the first incumbency and
    // drove the real suicide retirement chain; the successor manager prompt is the
    // owner-dispatched gate, so the next incumbency is open in AuditPending.
    const retired = await hooks.tool.review.execute(input, {sessionID: session, callID: 'review-call', messageID: 'review-run', agent: 'manager'})
    assert.match(retired, /recorded = false/)
    // The gate binds the retired call id, not the successor's review right: a fresh
    // call id in the next incumbency records its own independent assessment.
    const successor = await execute(input, {call: 'review-call-2', run: 'review-run-2'})
    assert.match(successor, /recorded = true/)
    // With the successor assessment accepted, the retired call id still cannot
    // replay: it never becomes an idempotent hit on the successor's result.
    const replayed = await hooks.tool.review.execute(input, {sessionID: session, callID: 'review-call', messageID: 'review-run', agent: 'manager'})
    assert.match(replayed, /recorded = false/)
  })
})
