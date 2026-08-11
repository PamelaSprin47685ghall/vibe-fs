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
} from '../../unit/support/domain.mjs'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
const enforcerRoot = path.join(repoRoot, 'resources', 'enforcer')

test('ENFORCER_resource_folder_rulebook_loads_with_contiguous_ordinals', () => {
  const rules = enforcerCatalogResource.load()
  assert.ok(Array.isArray(rules))
  assert.ok(rules.length > 0, 'rulebook must contain at least one rule')
  assert.equal(rules.length, 120)

  const ordinals = rules.map((r) => r.CatalogOrdinal).sort((a, b) => a - b)
  assert.deepEqual(
    ordinals,
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
    assert.ok(rule.Nudge.trim().length > 0)
    assert.ok(rule.ScoreWhen.trim().length > 0)
  }

  // Lexical directory order drives CatalogOrdinal.
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
    const ordinals = rules.map((r) => r.CatalogOrdinal)
    assert.deepEqual(
      [...ordinals].sort((a, b) => a - b),
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
