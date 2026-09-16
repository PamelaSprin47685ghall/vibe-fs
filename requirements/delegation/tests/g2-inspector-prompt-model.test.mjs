// Inspector prompt model remains an owner ToolSpec and delegated execution
// stays behind SyncDelegateSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const source = readFileSync(new URL('../../../src/Wanxiangshu/OpenCode/Tools/InspectorTool.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-021] G2_INSPECTOR_prompt_contains_charge_and_scope', () => {
  assert.match(source, /charge/i)
  assert.match(source, /scope|workspace/i)
})
test('WHAT[DELEG-021] G2_INSPECTOR_role_maps_to_inspector', () => {
  assert.equal(sync.vocabulary('Inspector', 'Fast', 's').role, 'inspector')
})
test('WHAT[DELEG-021] G2_INSPECTOR_no_legacy_discriminated_union_shape_crosses_tool_boundary', () => {
  const legacy = [['.', 'tag'].join(''), ['.', 'fields'].join(''), ['cases', '()'].join('')]
  assert.equal(legacy.some((token) => source.includes(token)), false)
})
