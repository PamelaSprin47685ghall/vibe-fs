import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const readRole = (role, locale) => readFileSync(join(ROOT, 'resources/provider/role', role, locale), 'utf8')

test('WHAT[OFF-014] inquiry_consequence_is_semantic_understanding_not_evidence_minting', () => {
  const en = readRole('inquiry', 'en.md')
  const zh = readRole('inquiry', 'zh-CN.md')
  assert.match(en, /semantic intelligence/i)
  assert.match(en, /A thought does not become an observation by being thought twice/i)
  assert.match(zh, /语义智能/)
  assert.match(zh, /一个想法不会因为被想了两次，就变成 observation/)
})
