import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-004] AA_prompt_020_repair_behavior_contract_defines_mechanical', () => {
  for (const locale of LOCALES) {
    const text = readTool('repair-behavior', locale)
    assert.match(text, /meaning is already decided|含义已经被决定|已经决定.*含义/i, 'mechanical = decided meaning')
    assert.match(
      text,
      /Do not treat the returned WorkRecord as proof that the repair passes|不要把返回的 WorkRecord 当作[^。]*通过的证明/i,
      'returned record must not be claimed as passing proof',
    )
  }
})
