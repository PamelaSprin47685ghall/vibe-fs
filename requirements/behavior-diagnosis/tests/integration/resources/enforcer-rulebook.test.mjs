// Folder rulebook load contract through the registered Enforcer owner surface.
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as enforcer from '../../../../../dist/Enforcer/Surface.js'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../../..')
const enforcerRoot = path.join(repoRoot, 'resources', 'enforcer')

test('WHAT[BD-001] ENFORCER_resource_folder_rulebook_loads_with_contiguous_ordinals', () => {
  const rules = enforcer.rules()
  assert.ok(Array.isArray(rules))
  assert.equal(rules.length, 120)
  assert.deepEqual(rules.map((r) => r.lexicalOrder), Array.from({ length: rules.length }, (_, i) => i + 1))
  assert.equal(enforcer.validate(1, rules).ok, true)
  assert.equal(new Set(rules.map((r) => r.name)).size, rules.length)
  for (const rule of rules) {
    assert.equal(rule.name, rule.ruleId)
    assert.equal(rule.name, rule.fieldName)
    assert.ok(rule.enforcerText.trim().length > 0)
    assert.ok(rule.mainText.trim().length > 0)
    assert.equal(rule.scoreWhen, undefined)
    assert.equal(rule.nudge, undefined)
    assert.equal(rule.family, undefined)
    assert.equal(rule.catalogOrdinal, undefined)
  }

  const dirs = fs.readdirSync(enforcerRoot, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name).sort()
  assert.deepEqual(rules.map((r) => r.name), dirs)
})

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

test('WHAT[BD-002] ENFORCER_resource_catalog_json_is_not_runtime_ssot', () => {
  assert.equal(fs.existsSync(path.join(enforcerRoot, 'catalog.json')), false)
})

test('WHAT[BD-002] ENFORCER_resource_rulebook_load_is_independent_of_process_cwd', () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    const rules = enforcer.rules()
    assert.equal(rules.length, 120)
    assert.deepEqual(rules.map((r) => r.lexicalOrder), Array.from({ length: rules.length }, (_, i) => i + 1))
  } finally {
    process.chdir(previous)
  }
})

test('WHAT[BD-004] ENFORCER_resource_effective_blogger_prompt_includes_all_enforcer_texts', () => {
  const rules = enforcer.rules()
  const composed = enforcer.composeBloggerSystemPrompt('base', 'en')
  assert.match(composed, /# Enforcer Rulebook/)
  for (const rule of rules) {
    assert.match(composed, new RegExp(`## ${rule.name}`))
    assert.ok(composed.includes(rule.enforcerText.trim()))
  }
})
