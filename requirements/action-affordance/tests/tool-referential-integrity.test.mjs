// Split from tests/unit/verify/tool-referential-integrity.test.mjs (cutover Wave 2a); owner: action-affordance
//
// ARCH-016 Gate A — tool referential integrity (no dist).
// action-affordance 半边：same name = 唯一 semantic contract（AA 008）。schema/name
// 执行面（ENF-009）与 legacy name ratchet 在 capability-enforcement 包测试内。
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  extractToolSpecNames,
  scanEntries,
} from '../../../scripts/checks/tool-referential-integrity.mjs'

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

test('gate_a_extracts_tool_spec_record_names', () => {
  const names = extractToolSpecNames('ForkTool.fs', GOOD_FORK)
  assert.deepEqual(names.map((n) => n.name), ['fork'])
})

test('gate_a_duplicate_tool_name_is_red', () => {
  const entries = [
    { file: 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/AlphaTool.fs', text: DUPLICATE_OWNERS.split('\n\n')[0] + '\n' },
    { file: 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/BetaTool.fs', text: DUPLICATE_OWNERS.split('\n\n')[1] + '\n' },
  ]
  const violations = scanEntries(entries)
  assert.ok(violations.some((v) => v.code === 'duplicate-tool-owner' && v.detail?.includes("'join'")))
})
