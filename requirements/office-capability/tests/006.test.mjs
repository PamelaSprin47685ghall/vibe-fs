import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, 'resources/provider', rel), 'utf8')

test('WHAT[OFF-006] OFF_006_offices_are_not_interchangeable_general_purpose_agents', () => {
  const managerEn = read('role/manager/en.md')
  const managerZh = read('role/manager/zh-CN.md')
  assert.match(managerEn, /Do not treat these offices as interchangeable/i)
  assert.match(managerEn, /A Coder is not an Operator/i)
  assert.match(managerZh, /可互换|碰巧没有 shell/)
  assert.match(managerZh, /Coder 不是碰巧没有 shell 的 Operator/)
  assert.doesNotMatch(read('tool/fork/description/en.md'), /Commission another witness/i)
})
