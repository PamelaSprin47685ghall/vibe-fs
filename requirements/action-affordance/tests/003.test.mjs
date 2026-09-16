import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-003] AA_prompt_020_inspect_contract_names_the_not_performed_act', () => {
  for (const locale of LOCALES) {
    const text = readTool('inspect', locale)
    assert.match(text, /read-only in the causal sense|因果意义上是只读的/i, 'causal read-only must be explicit')
    assert.match(
      text,
      /does not implement or repair code|不会实现或修复代码|不实现或修复代码/i,
      'the tempting adjacent act must be named and refused',
    )
  }
})
