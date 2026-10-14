import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const opening = () => surface.encodeOpening('plan', 'work-1', '/repo', 7n)

const incumbencyIdOf = (text) =>
  [...text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line)).flatMap((frame) => frame.events ?? [])].reverse().find((event) => event.type === 'IncumbencyOpened').incumbencyId

test('WHAT[work-012] 同一 workId 的第二笔 WorkOpened 不得被折叠成已开立', () => {
  const first = opening()
  assert.equal(first.includes('confirmationCount'), false)
  assert.equal(first.includes('"spawn"'), false)
  const opened = surface.readProbe(first)
  assert.equal(opened.next, 'Review')
  assert.equal(opened.sequence, 'S1,S2,S3')
  assert.equal(opened.names.includes('WorkOpened'), true)
  assert.equal(opened.names.includes('IncumbencyOpened'), true)

  // A second opening of the same work under a different request id is a new
  // opening, not a replay: the fold must refuse it.
  const secondFrame = JSON.parse(first)
  secondFrame.requestId = 'open:work-1:second'
  const second = `${canonical(secondFrame)}\n`
  const again = surface.readProbe(`${first}${second}`)
  assert.equal(again.ok, false)
  assert.equal(again.opened, false)
  assert.equal(again.names, '')
  assert.equal(again.reason, 'Work is already open or identity differs')
})
function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(',')}]`
  if (value !== null && typeof value === 'object') {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonical(value[key])}`).join(',')}}`
  }
  return JSON.stringify(value)
}

test('WHAT[work-012] 开立载荷出现确认次数或 spawn 字段时，公开读取拒绝', () => {
  const bytes = opening()
  const frame = JSON.parse(bytes)
  const opened = frame.events.find((event) => event.type === 'WorkOpened')
  opened.confirmationCount = 1
  opened.spawn = true
  const read = surface.readProbe(`${canonical(frame)}\n`)
  assert.equal(read.ok, false)
  assert.equal(read.opened, false)
  assert.equal(read.names, '')
  assert.equal(read.activeId, '')
  assert.match(read.reason, /Unknown field/)
  assert.notEqual(read.next, 'Review')
})
test('WHAT[work-012] S4 的 Promoted 不得置交付授权，S1 的 Promoted 也不得置真', () => {
  const s4 = surface.encodeOpening('S4', 'work-s4', '/repo', 3n)
  const s4Folded = surface.reviewProbe(s4, incumbencyIdOf(s4), 'Promoted')
  assert.equal(s4Folded.decision, 'Write')
  assert.equal(s4Folded.authorized, false)

  const plan = opening()
  const s1 = surface.reviewProbe(plan, incumbencyIdOf(plan), 'Promoted')
  assert.equal(s1.decision, 'Write')
  assert.equal(s1.authorized, false)
  const s3 = surface.finalPromotionProbe(plan)
  assert.equal(s3.authorized, true)
  assert.equal(s3.detail, 'S3')
})

test('WHAT[work-012] S4 的 Promoted 不得点亮交付授权，S1、S2、S3 的终元素 Promoted 必须点亮', () => {
  const execution = surface.encodeOpening('S4', 'work-s4', '/repo', 3n)
  const executionPromotion = surface.finalPromotionProbe(execution)
  assert.equal(executionPromotion.authorized, false)

  const plan = surface.encodeOpening('plan', 'work-plan', '/repo', 7n)
  const planPromotion = surface.finalPromotionProbe(plan)
  assert.equal(planPromotion.authorized, true)
  assert.equal(planPromotion.detail, 'S3')
})
