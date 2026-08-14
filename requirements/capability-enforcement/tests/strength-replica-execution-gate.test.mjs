// Split from tests/unit/strength/host-canary-k0.test.mjs (cutover Wave 2a); owner: capability-enforcement
//
// ENF-005: replica 收窄 — the Strength replica's execution gate denies
// write/edit/executor/fork/join/network tools, and the replica host tool map
// denies unknown tools instead of raising a permission-ask surface. Same
// capability set the schema is built from: forged mutating/network/session
// tools stay outside the replica.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as Frame from '../../../dist/Strength/Frame.js'
import { toolCapabilitiesFor } from '../../../dist/Interaction/Authority/Model.js'
import { ProviderRequestKind } from '../../../dist/Context/Prefix/Candidate.js'
import * as Runtime from '../../../dist/Strength/Runtime.js'
import { Role, ToolPermission } from '../../../dist/Foundation/Roles.js'
import { mapEntries } from '../../verification-system/tests/support/domain.mjs'

const caseOf = (value) => value.cases()[value.tag]
const permissionNames = (set) => [...set].map(caseOf).sort()

test('STRENGTH_004_005_policy_execution_gate_denies_write_edit_executor_fork_join_network', () => {
  // Not the live Host execution-gate canary. Same capability set the schema is
  // built from: forged mutating/network/session tools stay outside the replica.
  const capabilities = toolCapabilitiesFor(Role.Coder, ProviderRequestKind.StrengthReplica)
  const allowed = new Set(permissionNames(capabilities))
  const denied = [
    ToolPermission.Write,
    ToolPermission.Edit,
    ToolPermission.Exec,
    ToolPermission.Fork,
    ToolPermission.Join,
    ToolPermission.Horizon,
    ToolPermission.Network,
    ToolPermission.Pty,
  ]
  for (const permission of denied) {
    assert.equal(allowed.has(caseOf(permission)), false, caseOf(permission))
  }

  for (const tool of ['write', 'edit', 'run', 'fork', 'join', 'network', 'bash', 'horizon']) {
    assert.equal(Frame.StrengthFrame_isAllowedTool(tool), false, tool)
  }
  assert.equal(Frame.StrengthFrame_isAllowedTool('read'), true)
  assert.equal(Frame.StrengthFrame_isAllowedTool('glob'), true)
  assert.equal(Frame.StrengthFrame_isAllowedTool('grep'), true)
})

test('STRENGTH_004_006_policy_replica_host_tool_map_denies_unknown_tools_instead_of_asking', () => {
  // Not the live Host permission-popup canary. `* = false` is the unit stand-in:
  // unknown tools are denied, so there is no permission-ask surface to raise.
  const entries = Object.fromEntries(mapEntries(Runtime.StrengthReplicaTools_exactReadonlyHostToolMap))
  assert.equal(entries['*'], false)
  assert.equal(entries.read, true)
  assert.equal(entries.glob, true)
  assert.equal(entries.grep, true)
})
