import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')

const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const HIGH_RISK_TOOLS = Object.freeze([
  'commission',
  'establish-behavior',
  'fork',
  'inspect',
  'query-shell',
  'repair-behavior',
  'resume',
  'run',
])

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-007] AA_arch_006_007_distinct_semantics_have_distinct_names', () => {
  const fork = readTool('fork', 'en')
  const commission = readTool('commission', 'en')
  assert.match(fork, /another office within this mission/i)
  assert.match(commission, /independent road/i)
})
