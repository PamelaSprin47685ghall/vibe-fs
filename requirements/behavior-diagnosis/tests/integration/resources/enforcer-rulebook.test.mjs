// tests/integration/resources/enforcer-rulebook.test.mjs — folder rulebook load contract.
//
// EnforcerCatalogResource.load scans resources/enforcer/* directories
// (enforcer.md + main.md) via PackageResources. catalog.json is not read.
//
// Not discovered by tests/unit/runner.mjs. Run standalone:
//   node --test tests/integration/resources/enforcer-rulebook.test.mjs
// (requires dist/ built; import through tests/unit/support/domain.mjs facade)

import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  enforcerCatalogResource,
  packageResources,
  enforcerCatalog,
  promptResources,
  providerLanguage,
  runtimeResources,
} from '../../unit/support/domain.mjs'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
const enforcerRoot = path.join(repoRoot, 'resources', 'enforcer')

test('ENFORCER_resource_folder_rulebook_loads_with_contiguous_ordinals', () => {
  const rules = enforcerCatalogResource.load()
  assert.ok(Array.isArray(rules))
  assert.ok(rules.length > 0, 'rulebook must contain at least one rule')
  assert.equal(rules.length, 120)

  const orders = rules.map((r) => r.LexicalOrder).sort((a, b) => a - b)
  assert.deepEqual(
    orders,
    Array.from({ length: rules.length }, (_, i) => i + 1),
  )

  const validated = enforcerCatalog.validate(1, rules)
  assert.equal(validated.ok, true)
  assert.equal(validated.value.length, rules.length)

  const names = new Set(rules.map((r) => r.Name))
  const ids = new Set(rules.map((r) => r.RuleId))
  const fields = new Set(rules.map((r) => r.FieldName))
  assert.equal(names.size, rules.length)
  assert.equal(ids.size, rules.length)
  assert.equal(fields.size, rules.length)

  for (const rule of rules) {
    assert.equal(rule.Name, rule.RuleId)
    assert.equal(rule.Name, rule.FieldName)
    assert.ok(rule.EnforcerText.trim().length > 0)
    assert.ok(rule.MainText.trim().length > 0)
    assert.equal(rule.ScoreWhen, undefined)
    assert.equal(rule.Nudge, undefined)
    assert.equal(rule.Family, undefined)
    assert.equal(rule.CatalogOrdinal, undefined)
  }

  // Lexical directory order drives LexicalOrder.
  const dirs = fs
    .readdirSync(enforcerRoot, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => d.name)
    .sort()
  assert.deepEqual(
    rules.map((r) => r.Name),
    dirs,
  )
})

test('ENFORCER_PROMPT_017_rulebook_loads_authored_zh_cn_without_fallback', () => {
  const en = enforcerCatalogResource.loadFor(providerLanguage.english)
  const zh = enforcerCatalogResource.loadFor(providerLanguage.simplifiedChinese)
  assert.equal(en.length, 120)
  assert.equal(zh.length, 120)
  assert.deepEqual(zh.map((rule) => rule.Name), en.map((rule) => rule.Name))

  for (let index = 0; index < zh.length; index += 1) {
    const zhRule = zh[index]
    const enRule = en[index]
    assert.notEqual(zhRule.EnforcerText, enRule.EnforcerText, `${zhRule.Name}: detection locale`)
    assert.notEqual(zhRule.MainText, enRule.MainText, `${zhRule.Name}: remediation locale`)
    assert.match(zhRule.EnforcerText, /[\u3400-\u9fff]/, `${zhRule.Name}: detection Chinese`)
    assert.match(zhRule.MainText, /[\u3400-\u9fff]/, `${zhRule.Name}: remediation Chinese`)
    assert.equal(
      zhRule.EnforcerText.trim(),
      fs.readFileSync(path.join(enforcerRoot, zhRule.Name, 'enforcer.zh-CN.md'), 'utf8').trim(),
    )
    assert.equal(
      zhRule.MainText.trim(),
      fs.readFileSync(path.join(enforcerRoot, zhRule.Name, 'main.zh-CN.md'), 'utf8').trim(),
    )
  }

  const zhBase = promptResources.loadForLanguage(providerLanguage.simplifiedChinese).BloggerSystemPrompt
  const composed = enforcerCatalogResource.composeBloggerSystemPromptFor(
    providerLanguage.simplifiedChinese,
    zhBase,
    zh,
  )
  assert.match(composed, /# Enforcer RuleBook（规则书）/)
  assert.match(composed, /[\u3400-\u9fff]/)
  assert.ok(composed.includes(zh[0].EnforcerText.trim()))
})

test('ENFORCER_PROMPT_017_runtime_preloads_both_rulebook_locales', () => {
  const resources = runtimeResources.loadFor(providerLanguage.simplifiedChinese)
  runtimeResources.install(resources)
  const en = runtimeResources.enforcerRulesFor(providerLanguage.english)
  const zh = runtimeResources.enforcerRulesFor(providerLanguage.simplifiedChinese)
  assert.equal(en.length, 120)
  assert.equal(zh.length, 120)
  assert.doesNotMatch(en[0].EnforcerText, /中文版/)
  assert.match(zh[0].EnforcerText, /[\u3400-\u9fff]/)
  assert.match(resources.Prompts.BloggerSystemPrompt, /# Enforcer RuleBook（规则书）/)
})

test('ENFORCER_resource_catalog_json_is_not_runtime_ssot', () => {
  assert.equal(
    fs.existsSync(path.join(enforcerRoot, 'catalog.json')),
    false,
    'resources/enforcer/catalog.json must be removed after folder cutover',
  )
})

test('ENFORCER_resource_missing_package_path_throws', () => {
  assert.throws(
    () => packageResources.readText('enforcer/does-not-exist-rule/enforcer.md'),
    (err) => {
      const message = String(err?.message ?? err)
      assert.match(message, /package resource missing/)
      assert.match(message, /does-not-exist-rule/)
      return true
    },
  )
})

test('ENFORCER_resource_rulebook_load_independent_of_process_cwd', () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    const rules = enforcerCatalogResource.load()
    assert.ok(rules.length > 0)
    const orders = rules.map((r) => r.LexicalOrder)
    assert.deepEqual(
      [...orders].sort((a, b) => a - b),
      Array.from({ length: rules.length }, (_, i) => i + 1),
    )
  } finally {
    process.chdir(previous)
  }
})

test('ENFORCER_resource_effective_blogger_prompt_includes_all_enforcer_texts', () => {
  const rules = enforcerCatalogResource.load()
  const base = promptResources.load().BloggerSystemPrompt
  const composed = enforcerCatalogResource.composeBloggerSystemPrompt(base, rules)
  assert.match(composed, /# Enforcer Rulebook/)
  for (const rule of rules) {
    assert.match(composed, new RegExp(`## ${rule.Name}`))
    assert.ok(composed.includes(rule.EnforcerText.trim()), `missing enforcer body for ${rule.Name}`)
  }
})
