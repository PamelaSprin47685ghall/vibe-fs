import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const TESTS_DIR = join(ROOT, 'requirements/requirement-system/tests')

const read = (path) => readFileSync(path, 'utf8')

test('WHAT[requirement-system-004] every test belongs to exactly one clause', () => {
  // 新 [004]：任何一个测试恰好属于某个条款。如果服务于多个包或条款，以主服务对象为准。
  // 对本包全部测试文件进行断言：每个测试文件里的用例标题锚点必须对应到且仅对应到一个主条款
  const files = readdirSync(TESTS_DIR).filter((f) => /^\d{3}\.test\.mjs$/.test(f))
  assert.ok(files.length > 0, 'tests directory must contain test files')

  for (const file of files) {
    const clauseNum = file.slice(0, 3)
    const expectedAnchor = `WHAT[requirement-system-${clauseNum}]`
    const content = read(join(TESTS_DIR, file))

    // 提取所有 test(...) 或 it(...) 中的 WHAT[...] 锚点
    const testCaseMatches = [...content.matchAll(/^\s*(?:test|it)\s*\(\s*['"`]([^'"`]*WHAT\[([^\]]+)\][^'"`]*)/gm)]
    assert.ok(
      testCaseMatches.length > 0,
      `${file}: must define at least one test case with a WHAT[...] anchor`,
    )

    for (const m of testCaseMatches) {
      const anchor = `WHAT[${m[2]}]`
      assert.equal(
        anchor,
        expectedAnchor,
        `${file}: test case anchor ${anchor} must match the primary clause ${expectedAnchor}`,
      )
    }
  }
})
