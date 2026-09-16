import assert from 'node:assert/strict'
import test from 'node:test'
import * as tg from '../../../dist/Enforcer/TipGuidanceDeliverySurface.js'
import * as tipSurface from '../../../dist/Enforcer/Guidance/TipSurface.js'

test('WHAT[GD-002] ENFORCER_TIP_DELIVERY_001_first_resolve_is_full_main_md', () => {
  const state = tg.createDeliveryState()
  const res = tg.resolveTip(state, 'ses-1', 'tip-sample')
  assert.equal(res.kind, 'Full')
  assert.match(res.text, /# Enforcer Tip/)
})

test('WHAT[GD-002] ENFORCER_PROMPT_017_full_tip_guidance_uses_owner_session_zh_cn_rulebook', () => {
  const state = tg.createDeliveryState({ language: 'SimplifiedChinese' })
  const res = tg.resolveTip(state, 'ses-1', 'tip-sample')
  assert.equal(res.kind, 'Full')
  assert.match(res.text, /规则提示/)
})
