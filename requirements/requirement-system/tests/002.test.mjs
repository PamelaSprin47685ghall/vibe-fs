import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const REQUIREMENTS = join(ROOT, 'requirements')

// 新 [006] 只强制 {WHY,WHAT}.md 与 tests/ 目录，对齐两文档规范
const REQUIRED_DOCS = ['WHY.md', 'WHAT.md']

test('WHAT[requirement-system-002] package identity is the name, not the physical layout', () => {
  assert.deepEqual(REQUIRED_DOCS, ['WHY.md', 'WHAT.md'])
  assert.ok(!existsSync(join(REQUIREMENTS, 'requirement-system/package.toml')), 'no manifest format may enter the tree contract')
})
