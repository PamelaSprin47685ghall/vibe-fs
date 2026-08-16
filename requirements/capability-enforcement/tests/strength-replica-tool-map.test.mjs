// Split from tests/unit/strength/runtime.test.mjs (cutover Wave 2a); owner: capability-enforcement
//
// ENF-005: the Strength replica's host tool map narrows to exact readonly
// (read/glob/grep) and denies everything else. The tool map IS the enforcement
// surface the replica session is executed against, so the narrowing assertion
// lives on the enforcement side; SPEC-INV-004 keeps the authority-policy
// (fail-closed decisions) side.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  capabilities,
  exactReadonlyHostToolMap,
} from '../../../dist/Strength/Surface.js'

test('WHAT[ENF-005] STRENGTH_004_replica_host_tool_map_denies_everything_then_allows_exact_readonly', () => {
  assert.deepEqual(exactReadonlyHostToolMap, [
    { tool: '*', allowed: false },
    { tool: 'glob', allowed: true },
    { tool: 'grep', allowed: true },
    { tool: 'read', allowed: true },
  ])
  assert.deepEqual(capabilities('coder'), ['Glob', 'Grep', 'Read'])
  assert.deepEqual(capabilities('devops'), ['Glob', 'Grep', 'Read'])
  assert.deepEqual(capabilities('manager'), [])
})
