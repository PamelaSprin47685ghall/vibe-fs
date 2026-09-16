// The internal Engineer charge stays an owner surface and delegated execution
// stays behind SyncDelegateSurface; no tool-module prompt crosses the boundary.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as sync from '../../../dist/Execution/Delegation/SyncDelegate/Surface.js'

const source = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/SyncDelegate/Surface.fs', import.meta.url), 'utf8')
test('WHAT[DELEG-021] G2_ENGINEER_prompt_contains_charge_and_scope', () => {
  assert.match(source, /charge/i)
  assert.match(source, /scope|workspace|owner/i)
})
test('WHAT[DELEG-021] G2_ENGINEER_role_maps_to_engineer', () => {
  assert.equal(sync.vocabulary('Engineer', 'Fast', 's').role, 'engineer')
})
test('WHAT[DELEG-021] G2_ENGINEER_no_legacy_discriminated_union_shape_crosses_tool_boundary', () => {
  const legacy = [['.', 'tag'].join(''), ['.', 'fields'].join(''), ['cases', '()'].join('')]
  assert.equal(legacy.some((token) => source.includes(token)), false)
})
