import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const SMALL_FIX = /普通小型修复[、,].{0,40}不要求创建 Change/

test('WHAT[requirement-system-015] AGENTS.md keeps the small-fix exemption', () => {
  const agents = read('AGENTS.md')
  assert.match(agents, SMALL_FIX)
  const dropped = agents.replace(SMALL_FIX, '')
  assert.doesNotMatch(dropped, SMALL_FIX)
})
