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

test('WHAT[relay-assessment-002] cross-incumbency replay of a retired review call is rejected', {todo: 'requires a real retirement-chain fixture to open the next incumbency before replaying the retired call; the fold-level AssessmentReplayToolCall gate is in place'}, async () => {
  await withReview(async ({execute, hooks, session}) => {
    const input = reviewScores('REVISE')
    const first = await execute(input)
    assert.match(first, /recorded = true/)
    // TODO: retire this incumbency through the real suicide chain, open the next
    // incumbency, then replay the retired call; the fold-level seen-tool-call gate
    // must reject the append and the accepted assessment must stay unchanged.
  })
})
