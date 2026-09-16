import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { scanText } from '../../../scripts/checks/provider-leak-gate.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const LOCALES = ['en', 'zh-CN']

const CLEAN_HORIZON = `
module HorizonTool =
    let private lineForHandle handle _ =
        sprintf "# %s is still away." "Coder"

    let spec scope =
        { Name = "horizon"
          Description = "Orient to what remains at your horizon."
          Arguments = []
          Execute = fun _ _ _ -> task { return ToolHostCodec.tomlObjectWithInstructions ["# Nothing"] [] } }
`

test('WHAT[PARTICIPANT-HORIZON-001] PH_exec_005_horizon_description_declares_pull_only_and_hides_machinery', () => {
  for (const locale of LOCALES) {
    const text = read(`resources/provider/tool/horizon/description/${locale}.md`)
    assert.match(text, /pull-only|只在调用时主动读取一次|不?轮询|do not poll/i)
    assert.match(text, /hidden machinery|隐藏机|hidden machinery|不 dump 隐藏/i)
  }
})

test('WHAT[PARTICIPANT-HORIZON-001] gate_b_clean_horizon_fixture_is_green', () => {
  assert.equal(scanText('HorizonTool.fs', CLEAN_HORIZON).length, 0)
})
