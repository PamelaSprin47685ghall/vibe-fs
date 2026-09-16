import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-012] AA_prompt_020_inspect_caller_forbidden_charge_is_named', () => {
  for (const locale of LOCALES) {
    const text = readTool('inspect', locale)
    assert.match(text, /Do not use inspect to ask for code changes|不要用 inspect 请求代码修改/i)
  }
})
