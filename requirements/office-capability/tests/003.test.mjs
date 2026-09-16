import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, 'resources/provider', rel), 'utf8')

test('WHAT[OFF-003] OFF_003_two_calling_names_differ_in_persona_and_depth_not_authority', () => {
  assert.match(
    read('tool/fork/description/en.md'),
    /differ in persona and reasoning depth,\s*not in the office's authority/i,
  )
  assert.match(
    read('tool/fork/description/zh-CN.md'),
    /区别在 persona 与 reasoning depth，不改变该 Office 的 authority/,
  )
})
