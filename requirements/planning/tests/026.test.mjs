import assert from 'node:assert/strict'
import test from 'node:test'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import {
  assistantText,
  cursors,
  ids,
  userText,
} from './support/plan-messages.mjs'

const openWork = (workId) => surface.PlanningSurface.workOpened(workId, '/tmp/plan-test')
const openInc = (workId, incId, stage, cursor) =>
  surface.PlanningSurface.incumbencyOpened(workId, incId, stage, cursor)
const retireInc = (incId, outcome, cursor) =>
  surface.PlanningSurface.incumbencyRetired(incId, outcome, cursor)

const firstStateOf = (events) => {
  let st = surface.PlanningSurface.empty()
  for (const ev of events) {
    const res = surface.PlanningSurface.applyWorkEvent(ev, st)
    assert.equal(res.ok, true, JSON.stringify(res))
    st = res.state
  }
  return st
}

const tenureFor = (overrides) => ({
  workId: 'w1',
  incumbencyId: 'inc-2',
  stage: 'S2',
  openingCursor: 20n,
  previousRange: [7n, 25n],
  isFreshHandover: false,
  ...overrides,
})

test('WHAT[planning-017] S1 with no retirement carries no previous range', () => {
  const st = firstStateOf([openWork('w1'), openInc('w1', 'inc-1', 'S1', 0n)])
  assert.equal(surface.PlanningSurface.latestRetirementRange(st), undefined)
})

test('WHAT[planning-017] S1 retirement preserves its opening range for the next tenure', () => {
  const st = firstStateOf([
    openWork('w1'),
    openInc('w1', 'inc-1', 'S1', 7n),
    retireInc('inc-1', 'Continue', 25n),
  ])
  assert.deepEqual(surface.PlanningSurface.latestRetirementRange(st), [7n, 25n])
})

test('WHAT[planning-017] tenure assembly consumes PreviousRange into LWR_prev', () => {
  const rawMessages = [
    userText('u1', 'go'),
    assistantText('a-old', 'old work', 'u1'),
    assistantText('a-new', 'new work', 'u1'),
  ]
  let seen = null
  const result = surface.PlanningSurface.assembleTenureMessages(
    rawMessages,
    tenureFor(),
    (range) => {
      seen = range
      return 'prev record'
    },
    cursors({ 'a-old': 9n, 'a-new': 30n }),
  )
  assert.equal(seen.start, 7n)
  assert.equal(seen.end, 25n)
  const assembledIds = ids(result.messages)
  assert.ok(!assembledIds.includes('a-old'))
  assert.ok(assembledIds.includes('a-new'))
  assert.ok(assembledIds.some((id) => id.startsWith('lwr-prev-')))
  // LWR_prev is injected before the first current interaction (a-new).
  assert.deepEqual(assembledIds, ['u1', 'lwr-prev-inc-2', 'a-new'])
})