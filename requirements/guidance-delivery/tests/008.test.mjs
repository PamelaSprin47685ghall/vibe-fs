import assert from 'node:assert/strict'
import test from 'node:test'
import * as audience from '../../../dist/Enforcer/AudienceSeparationSurface.js'
import * as tipV2 from '../../../dist/Enforcer/TipV2DeliverySurface.js'

test('WHAT[GD-008] AUDIENCE_001_main_md_sections_never_enter_blogger_system_prompt', () => {
  const prompt = audience.bloggerSystemPrompt()
  assert.doesNotMatch(prompt, /# Enforcer Tip/)
  assert.doesNotMatch(prompt, /## 现在该做什么/)
})

test('WHAT[GD-008] AUDIENCE_002_corpus_level_detection_and_remediation_do_not_leak', () => {
  const mainDelivery = audience.mainDeliveryText('tip-sample')
  assert.doesNotMatch(mainDelivery, /blogger-system-prompt/)
})

test('WHAT[GD-008] AUDIENCE_003_previous_tip_history_is_not_main_authority', () => {
  assert.equal(audience.isAuthorityInput('previous_enforcer_tip'), false)
})

test('WHAT[GD-008] TIP_V2_enforcer_md_separated_from_main_md', () => {
  assert.equal(tipV2.hasEnforcerText('tip-sample'), true)
  assert.equal(tipV2.hasMainText('tip-sample'), true)
  assert.notEqual(tipV2.getEnforcerText('tip-sample'), tipV2.getMainText('tip-sample'))
})

test('WHAT[GD-008] TIP_V2_delivery_preserves_audience_boundaries', () => {
  const bloggerView = tipV2.renderBloggerView('tip-sample')
  assert.doesNotMatch(bloggerView, /# Enforcer Tip/)
})
