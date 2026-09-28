import assert from 'node:assert/strict'
import { readFileSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { REPO_ROOT, deriveExpectedClosure } from '../../../scripts/verify-package.mjs'

test('WHAT[distribution-001] the expected package closure contains compiled entry and both-language resources', () => {
  const closure = deriveExpectedClosure()
  for (const relative of ['dist/OpenCode/Plugin/Plugin.js', 'resources/provider/role/manager/en.md', 'resources/provider/role/manager/zh-CN.md']) {
    assert.ok(closure.has(relative), relative)
    assert.ok(existsSync(join(REPO_ROOT, relative)))
    assert.ok(readFileSync(join(REPO_ROOT, relative)).length > 0)
  }
})

test.todo('WHAT[distribution-001] GAP-210: actual packed artifact and independent installed consumer pass the complete release proof')
