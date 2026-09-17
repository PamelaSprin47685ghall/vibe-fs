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

test('WHAT[ACTION-AFFORDANCE-014] AA_assume_contract_is_update_then_query_over_one_free_form_canvas', () => {
  for (const locale of LOCALES) {
    const description = readTool('assume', locale)
    const update = read(`resources/provider/tool/assume/arg-update/${locale}.md`)
    const query = read(`resources/provider/tool/assume/arg-query/${locale}.md`)

    assert.match(description, /唯一.*画板|one.*workspace/is)
    assert.match(description, /两个.*必填|two required/i)
    assert.match(description, /update.*恰好一个|update.*exactly one/is)
    assert.match(description, /update\s*=\s*["“]?\.["”]?|update.*`\.`/is)
    assert.match(description, /query.*零.*一个.*多个|query.*zero.*one.*multiple/is)
    assert.match(description, /query.*失败.*不.*回滚|query.*fail.*does not roll back/is)
    assert.match(description, /jq.*先验|jq.*prior/i)
    assert.doesNotMatch(update + query, /assumption/i)
  }
})
