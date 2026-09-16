import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const LOCALES = ['en', 'zh-CN']

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

test('WHAT[ACTION-AFFORDANCE-006] AA_prompt_020_run_contract_grounds_command_as_act_with_bounded_consequence', () => {
  for (const locale of LOCALES) {
    const text = readTool('run', locale)
    assert.match(text, /command is an act|命令是一种行动|command 是一次行动|命令是一次行动/i)
    assert.match(text, /economic commitments|经济承诺|不是运行时预测/i)
    const queryShell = readTool('query-shell', locale)
    assert.match(queryShell, /This is observation, not execution|这是观察，不是执行/i)
    assert.match(queryShell, /Not appropriate:|不适宜：/i)
  }
})
