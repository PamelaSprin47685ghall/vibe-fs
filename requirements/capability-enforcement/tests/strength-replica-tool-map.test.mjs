// Split from tests/unit/strength/runtime.test.mjs (cutover Wave 2a); owner: capability-enforcement
//
// ENF-005: the Strength replica's host tool map narrows to exact readonly
// (read/glob/grep) and denies everything else. The tool map IS the enforcement
// surface the replica session is executed against, so the narrowing assertion
// lives on the enforcement side; SPEC-INV-004 keeps the authority-policy
// (fail-closed decisions) side.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as Runtime from '../../../dist/Strength/Runtime.js'
import { Role } from '../../../dist/Foundation/Roles.js'
import { mapEntries } from '../../verification-system/tests/support/domain.mjs'

const caseOf = (value) => value.cases()[value.tag]
const setNames = (set) => [...set].map(caseOf).sort()

test('WHAT[ENF-005] STRENGTH_004_replica_host_tool_map_denies_everything_then_allows_exact_readonly', () => {
  const entries = Object.fromEntries(mapEntries(Runtime.StrengthReplicaTools_exactReadonlyHostToolMap))
  assert.deepEqual(entries, { '*': false, glob: true, grep: true, read: true })
  assert.deepEqual(setNames(Runtime.StrengthReplicaTools_capabilities(Role.Coder)), ['Glob', 'Grep', 'Read'])
  assert.deepEqual(setNames(Runtime.StrengthReplicaTools_capabilities(Role.DevOps)), ['Glob', 'Grep', 'Read'])
  assert.equal(Runtime.StrengthReplicaTools_capabilities(Role.Manager).size, 0)
})
