import assert from 'node:assert/strict'
import { existsSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const REQUIREMENTS = join(ROOT, 'requirements')

// 新 [006]：所有合法包必须完整包含 {WHY,WHAT}.md 与 tests/ 目录
const REQUIRED_DOCS = ['WHY.md', 'WHAT.md']

test('WHAT[requirement-system-006] package completeness requires WHY.md, WHAT.md, and tests/ directory', () => {
  // 最小语义验证：requirement-system 自身必须严格满足包完备性要求
  // 全树层级的完备性机械扫描由 017 meta-verifier 集中执行
  const pkgDir = join(REQUIREMENTS, 'requirement-system')
  for (const doc of REQUIRED_DOCS) {
    assert.ok(existsSync(join(pkgDir, doc)), `requirement-system must contain ${doc}`)
  }
  const testsDir = join(pkgDir, 'tests')
  assert.ok(existsSync(testsDir) && statSync(testsDir).isDirectory(), 'requirement-system must contain tests/ directory')
})
