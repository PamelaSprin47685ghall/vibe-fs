import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle
const incumbencyIdOf = (text) =>
  JSON.parse(text.split('\n').find((line) => line.includes('IncumbencyOpened'))).payload.incumbencyId

test('S4 的 Relayed 后继进入 Continue 派发，S1、S2、S3 停在等待用户', () => {
  const opened = surface.encodeOpening('S4', 'work-s4', '/repo', 4n)
  const reviewed = surface.reviewProbe(opened, incumbencyIdOf(opened), 'Stayed')
  const confirmed = surface.deliverProbe(`${opened}${reviewed.bytes}`, 'run-1', 'call-1', 5n)
  const retired = surface.deliverProbe(`${opened}${reviewed.bytes}${confirmed.bytes}`, 'run-1', 'call-2', 6n)
  const execution = surface.readProbe(`${opened}${reviewed.bytes}${confirmed.bytes}${retired.bytes}`)

  const planOpened = surface.encodeOpening('plan', 'work-plan', '/repo', 4n)
  const planReviewed = surface.reviewProbe(planOpened, incumbencyIdOf(planOpened), 'Stayed')
  const planRetired = surface.deliverProbe(`${planOpened}${planReviewed.bytes}`, 'run-1', 'call-1', 5n)
  const plan = surface.readProbe(`${planOpened}${planReviewed.bytes}${planRetired.bytes}`)

  assert.equal(execution.position, 'ActiveRebound')
  assert.equal(execution.next, 'EnterSuccessorPrompt')
  assert.equal(plan.position, 'ActiveRebound')
  assert.equal(plan.next, 'WaitForUserPrompt')
  assert.notEqual(execution.next, plan.next)
})
