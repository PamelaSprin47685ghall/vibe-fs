import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

test('S4 的 Promoted 不得点亮交付授权，S1、S2、S3 的终元素 Promoted 必须点亮', () => {
  const execution = surface.encodeOpening('S4', 'work-s4', '/repo', 3n)
  const executionPromotion = surface.finalPromotionProbe(execution)
  assert.equal(executionPromotion.authorized, false)

  const plan = surface.encodeOpening('plan', 'work-plan', '/repo', 7n)
  const planPromotion = surface.finalPromotionProbe(plan)
  assert.equal(planPromotion.authorized, true)
  assert.equal(planPromotion.detail, 'S3')
})
