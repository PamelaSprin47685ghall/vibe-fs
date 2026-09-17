import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const BASE = 'base blogger system prompt'

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
