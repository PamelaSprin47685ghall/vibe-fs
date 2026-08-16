// Rulebook → Blogger effective system composition (ENFORCER-030/170).
//
// Detection corpus completeness + determinism: the composed Blogger system
// prompt is a derived artifact over the packaged rulebook — every rule's
// enforcer.md is present under its TipName header exactly once, and the same
// input composes to the same bytes. Also pins the zh-CN leaf loading path
// (enforcer.zh-CN.md + main.zh-CN.md), which has no dedicated unit coverage
// elsewhere.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const BASE = 'base blogger system prompt'

test('WHAT[BD-004] BEHAVIOR_DIAGNOSIS_SYSTEM_001_composed_prompt_contains_every_tip_exactly_once', () => {
  const composed = enforcer.composeBloggerSystemPrompt(BASE, 'en')
  assert.ok(composed.includes(BASE), 'base prompt must be preserved')
  assert.ok(composed.includes('# Enforcer Rulebook'), 'rulebook header must be present')

  const names = enforcer.rules().map((r) => r.name)
  assert.equal(names.length, 120)
  for (const name of names) {
    const occurrences = composed.split(`## ${name}`).length - 1
    assert.equal(occurrences, 1, `TipName ${name} must appear exactly once, got ${occurrences}`)
  }
})

test('WHAT[BD-004] BEHAVIOR_DIAGNOSIS_SYSTEM_002_composition_is_deterministic', () => {
  const a = enforcer.composeBloggerSystemPrompt(BASE, 'en')
  const b = enforcer.composeBloggerSystemPrompt(BASE, 'en')
  assert.equal(a, b, 'same rulebook + base must compose to identical bytes')
})

test('WHAT[BD-005] BEHAVIOR_DIAGNOSIS_SYSTEM_003_zh_cn_leaf_load_is_complete_and_nonempty', () => {
  const zh = enforcer.loadFor('zh-CN')
  assert.equal(zh.length, 120, 'zh-CN rulebook must have 120 rules')
  const names = new Set(zh.map((r) => r.name))
  assert.equal(names.size, 120, 'zh-CN TipNames must be unique')
  for (const rule of zh) {
    assert.ok(rule.enforcerText.trim().length > 0, `zh-CN enforcer.md empty for ${rule.name}`)
    assert.ok(rule.mainText.trim().length > 0, `zh-CN main.md empty for ${rule.name}`)
    assert.equal(rule.name, rule.ruleId, `zh-CN RuleId mismatch for ${rule.name}`)
    assert.equal(rule.name, rule.fieldName, `zh-CN FieldName mismatch for ${rule.name}`)
  }
})

test('WHAT[BD-004] BEHAVIOR_DIAGNOSIS_SYSTEM_004_english_load_matches_packaged_rule_count', () => {
  const en = enforcer.rules()
  assert.equal(en.length, enforcer.ruleCount())
  assert.equal(en.length, 120)
})
