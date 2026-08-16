// Split from tests/unit/strength/host-canary-k0.test.mjs (cutover Wave 2a); owner: capability-enforcement
//
// ENF-005: replica 收窄 — the Strength replica's execution gate denies
// write/edit/executor/fork/join/network tools, and the replica host tool map
// denies unknown tools instead of raising a permission-ask surface. Same
// capability set the schema is built from: forged mutating/network/session
// tools stay outside the replica.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  capabilities,
  exactReadonlyHostToolMap,
  isAllowedTool,
} from '../../../dist/Strength/Surface.js'

test('WHAT[ENF-005] STRENGTH_004_005_policy_execution_gate_denies_write_edit_executor_fork_join_network', () => {
  // Not the live Host execution-gate canary. Same capability set the schema is
  // built from: forged mutating/network/session tools stay outside the replica.
  const allowed = new Set(capabilities('coder'))
  const denied = ['Write', 'Edit', 'Exec', 'Fork', 'Join', 'Horizon', 'Network', 'Pty']
  for (const permission of denied) {
    assert.equal(allowed.has(permission), false, permission)
  }

  for (const tool of ['write', 'edit', 'run', 'fork', 'join', 'network', 'bash', 'horizon']) {
    assert.equal(isAllowedTool(tool), false, tool)
  }
  assert.equal(isAllowedTool('read'), true)
  assert.equal(isAllowedTool('glob'), true)
  assert.equal(isAllowedTool('grep'), true)
})

test('WHAT[ENF-005] STRENGTH_004_006_policy_replica_host_tool_map_denies_unknown_tools_instead_of_asking', () => {
  const rules = exactReadonlyHostToolMap
  assert.equal(rules.length, 4)
  assert.deepEqual(rules[0], { tool: '*', allowed: false })
  assert.deepEqual(rules[1], { tool: 'glob', allowed: true })
  assert.deepEqual(rules[2], { tool: 'grep', allowed: true })
  assert.deepEqual(rules[3], { tool: 'read', allowed: true })
})
