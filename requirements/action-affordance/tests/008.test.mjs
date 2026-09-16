import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import {
  extractToolSpecNames,
  scanEntries,
} from '../../../scripts/checks/tool-referential-integrity.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const readTool = (tool, locale) => read(`resources/provider/tool/${tool}/description/${locale}.md`)

const GOOD_FORK = `
module ForkTool =
    let managerSpec factory scope =
        { Name = "fork"
          Description = "fork"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }
`

const DUPLICATE_OWNERS = `
module AlphaTool =
    let spec scope =
        { Name = "join"
          Description = "alpha"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }

module BetaTool =
    let spec scope =
        { Name = "join"
          Description = "beta"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }
`

test('WHAT[ACTION-AFFORDANCE-008] AA_arch_007_same_tool_name_means_same_contract', () => {
  const commission = readTool('commission', 'en')
  assert.match(commission, /This is not fork/i, 'commission must name the confusable nearby act it does NOT perform')
  assert.match(commission, /not position in a\s*lifecycle/i, 'commission is not a lifecycle stage')
  assert.match(commission, /not size of labor/i)
})

test('WHAT[ACTION-AFFORDANCE-008] gate_a_extracts_tool_spec_record_names', () => {
  const names = extractToolSpecNames('ForkTool.fs', GOOD_FORK)
  assert.deepEqual(names.map((n) => n.name), ['fork'])
})

test('WHAT[ACTION-AFFORDANCE-008] gate_a_duplicate_tool_name_is_red', () => {
  const entries = [
    { file: 'src/Wanxiangshu/OpenCode/Tools/AlphaTool.fs', text: DUPLICATE_OWNERS.split('\n\n')[0] + '\n' },
    { file: 'src/Wanxiangshu/OpenCode/Tools/BetaTool.fs', text: DUPLICATE_OWNERS.split('\n\n')[1] + '\n' },
  ]
  const violations = scanEntries(entries)
  assert.ok(violations.some((v) => v.code === 'duplicate-tool-owner' && v.detail?.includes("'join'")))
})
