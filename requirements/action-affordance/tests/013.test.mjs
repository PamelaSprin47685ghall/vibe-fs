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

test('WHAT[ACTION-AFFORDANCE-013] AA_prompt_020_success_returns_establish_bounded_consequence', () => {
  const commissionEn = readTool('commission', 'en')
  assert.match(commissionEn, /A successful return establishes that the named road has taken the charge\./)
  assert.match(commissionEn, /It does not establish that the destination has been reached\./)
})
