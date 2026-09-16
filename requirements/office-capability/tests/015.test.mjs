import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, 'resources/provider', rel), 'utf8')

test('WHAT[OFF-015] predictor_is_internal_mechanism_role_not_forkable_or_scheduled', () => {
  const forkEn = read('tool/fork/description/en.md')
  const forkZh = read('tool/fork/description/zh-CN.md')
  assert.doesNotMatch(forkEn, /\bpredictor\b/i)
  assert.doesNotMatch(forkZh, /predictor/)
})
