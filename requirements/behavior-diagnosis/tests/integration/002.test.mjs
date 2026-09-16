import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as enforcer from '../../../../dist/Enforcer/Surface.js'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../')
const enforcerRoot = path.join(repoRoot, 'resources', 'enforcer')

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
