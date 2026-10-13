import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

const opening = () => surface.encodeOpening('plan', 'work-1', '/repo', 7n)

const incumbencyIdOf = (text) =>
  JSON.parse(text.split('\n').find((line) => line.includes('IncumbencyOpened'))).payload.incumbencyId

test('WHAT[work-012] 同一 workId 的第二笔 WorkOpened 不得被折叠成已开立', () => {
  const first = opening()
  assert.equal(first.includes('confirmationCount'), false)
  assert.equal(first.includes('"spawn"'), false)
  const opened = surface.readProbe(first)
  assert.equal(opened.position, 'ActiveRebound')
  assert.equal(opened.sequence, 'S1,S2,S3')
  assert.equal(opened.names.includes('WorkOpened'), true)
  assert.equal(opened.names.includes('IncumbencyOpened'), true)

  const second = first + first
  const again = surface.readProbe(second)
  assert.notEqual(again.position, 'ActiveRebound')
  assert.equal(again.detail.includes('second WorkOpened'), true)
})
test('WHAT[work-012] 开立载荷出现确认次数或 spawn 字段时，真实折叠拒绝', () => {
  const bytes = opening()
  const mutated = bytes.replace(
    '{"authorityInitial"',
    '{"confirmationCount":1,"spawn":true,"authorityInitial"',
  )
  const read = surface.readProbe(mutated)
  assert.equal(read.position, 'StorageInvalid')
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
