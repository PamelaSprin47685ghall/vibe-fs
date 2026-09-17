import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as enforcer from '../../../../dist/Enforcer/Surface.js'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../../')

const enforcerRoot = path.join(repoRoot, 'resources', 'enforcer')

test('WHAT[BD-005] ENFORCER_PROMPT_017_rulebook_loads_authored_zh_cn_without_fallback', () => {
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
