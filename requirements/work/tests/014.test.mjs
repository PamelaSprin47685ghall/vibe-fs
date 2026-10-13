import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const opening = () => surface.encodeOpening('plan', 'work-1', '/repo', 7n)

const incumbencyIdOf = (text) =>
  JSON.parse(text.split('\n').find((line) => line.includes('IncumbencyOpened'))).payload.incumbencyId

test('WHAT[work-014] 两段式第一次确认落地前不得返回已确认，同一对重放不得第二次结束', () => {
  const s4 = surface.encodeOpening('S4', 'work-e', '/repo', 4n)
  const first = surface.deliverProbe(s4, 'run-a', 'call-a', 5n)
  assert.equal(first.decision, 'Done')
  assert.equal(first.detail, 'assessment required')
  assert.notEqual(first.detail, 'confirmed')

  const reviewed = surface.reviewProbe(
    s4,
    JSON.parse(s4.split('\n').find((line) => line.includes('IncumbencyOpened'))).payload.incumbencyId,
    'Stayed',
  )
  assert.equal(reviewed.decision, 'Write')
  const afterReview = s4 + reviewed.bytes
  const confirm = surface.deliverProbe(afterReview, 'run-a', 'call-a', 5n)
  assert.equal(confirm.decision, 'Write')
  assert.equal(confirm.names, 'ConfirmationCommitted')
  const replay = surface.replayProbe(afterReview + confirm.bytes, 'run-a', 'call-a')
  assert.equal(replay.decision, 'Done')
  assert.equal(replay.names, '')
  assert.equal(replay.detail.includes('delivered') || replay.detail.includes('relayed'), false)
})
