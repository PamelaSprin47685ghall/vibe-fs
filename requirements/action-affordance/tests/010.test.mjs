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

test('WHAT[ACTION-AFFORDANCE-010] AA_prompt_020_fork_contract_answers_whom_work_is_entrusted_to', () => {
  const fork = readTool('fork', 'en')
  assert.match(fork, /Choose the office by the consequence you need/i)
  assert.match(fork, /Engineer/i, 'fork presents Engineer')
})
