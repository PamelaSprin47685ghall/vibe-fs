// tests/integration/resources/enforcer-catalog.test.mjs — package catalog load contract.
//
// EnforcerCatalogResource.load reads resources/enforcer/catalog.json via
// PackageResources (import.meta.url → package root). Missing path throws.
//
// Not discovered by tests/unit/runner.mjs. Run standalone:
//   node --test tests/integration/resources/enforcer-catalog.test.mjs
// (requires dist/ built; import through tests/unit/domain.mjs facade)

import assert from 'node:assert/strict'
import test from 'node:test'
import { enforcerCatalogResource, packageResources, enforcerCatalog } from '../../unit/domain.mjs'

test('ENFORCER_resource_packaged_catalog_loads_with_contiguous_ordinals', () => {
  const rules = enforcerCatalogResource.load()
  assert.ok(Array.isArray(rules))
  assert.ok(rules.length > 0, 'catalog must contain at least one rule')
  const ordinals = rules.map((r) => r.CatalogOrdinal).sort((a, b) => a - b)
  assert.deepEqual(
    ordinals,
    Array.from({ length: rules.length }, (_, i) => i + 1),
  )
  // Domain validate must accept the same list (schemaVersion 1).
  const validated = enforcerCatalog.validate(1, rules)
  assert.equal(validated.ok, true)
  assert.equal(validated.value.length, rules.length)
  const ids = new Set(rules.map((r) => r.RuleId))
  const fields = new Set(rules.map((r) => r.FieldName))
  assert.equal(ids.size, rules.length)
  assert.equal(fields.size, rules.length)
})

test('ENFORCER_resource_missing_package_path_throws', () => {
  assert.throws(
    () => packageResources.readText('enforcer/does-not-exist-catalog.json'),
    (err) => {
      const message = String(err?.message ?? err)
      assert.match(message, /package resource missing/)
      assert.match(message, /does-not-exist-catalog\.json/)
      return true
    },
  )
})

test('ENFORCER_resource_catalog_load_independent_of_process_cwd', () => {
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
