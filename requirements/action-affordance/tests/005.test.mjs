import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-005] AA_prompt_020_establish_behavior_contract_separates_mutation_from_execution', () => {
  for (const locale of LOCALES) {
    const text = readTool('establish-behavior', locale)
    assert.match(text, /Coder writes source|Coder[^。]{0,24}(?:写入|修改|写|改变) source|托付 Coder/i)
    assert.match(text, /not execution evidence|不是执行证据|不运行这些测试/i)
  }
})
