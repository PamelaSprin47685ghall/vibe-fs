import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const BASE = 'base blogger system prompt'

test('WHAT[behavior-diagnosis-005] BEHAVIOR_DIAGNOSIS_SYSTEM_003_zh_cn_leaf_load_is_complete_and_nonempty', () => {
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

integrationTest('WHAT[behavior-diagnosis-005] ENFORCER_PROMPT_017_rulebook_loads_authored_zh_cn_without_fallback', () => {
  const en = enforcer.rules()
  const zh = enforcer.loadFor('zh-CN')
  assert.equal(en.length, 120)
  assert.equal(zh.length, 120)
  assert.deepEqual(zh.map((rule) => rule.name), en.map((rule) => rule.name))
  for (let index = 0; index < zh.length; index += 1) {
    assert.notEqual(zh[index].enforcerText, en[index].enforcerText)
    assert.notEqual(zh[index].mainText, en[index].mainText)
    assert.match(zh[index].enforcerText, /[\u3400-\u9fff]/)
    assert.match(zh[index].mainText, /[\u3400-\u9fff]/)
  }
  const composed = enforcer.composeBloggerSystemPrompt('基础 Blogger 系统提示', 'zh-CN')
  assert.match(composed, /# Enforcer RuleBook（规则书）/)
  assert.match(composed, /[\u3400-\u9fff]/)
})
