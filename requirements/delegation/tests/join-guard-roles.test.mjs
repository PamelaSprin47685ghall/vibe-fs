// Split from tests/unit/execution/join-guard.test.mjs (cutover Wave 2a);
// owner: delegation. EXEC-016 outstandingBackground 的 join 义务谓词：join-less
// 角色永不触发 guard；DevOps 的 live PTY 单独即 outstanding；Manager 无 journal
// 不 outstanding（listable 判定 → managed-session-lifecycle）。

import assert from 'node:assert/strict'
import test from 'node:test'
import { sessionId } from '../../verification-system/tests/support/domain.mjs'
import * as TerminalPolicyModule from '../../../dist/OpenCode/Host/TerminalPolicy.js'
import * as ForkTypesModule from '../../../dist/Execution/Delegation/Fork/Model.js'
import * as RolesModule from '../../../dist/Foundation/Roles.js'

const outstandingBackground = (() => {
  const names = Object.keys(TerminalPolicyModule)
  const key =
    names.find((n) => n === 'TerminalPolicy_outstandingBackground') ||
    names.find((n) => n.endsWith('_outstandingBackground') || n === 'outstandingBackground')
  if (!key || typeof TerminalPolicyModule[key] !== 'function') {
    throw new Error(
      `TerminalPolicy.outstandingBackground missing. Near: ${names.filter((n) => /outstanding|Terminal/.test(n)).join(', ')}`,
    )
  }
  return TerminalPolicyModule[key]
})()

const agentRole = (name) => {
  const role = ForkTypesModule.AgentRole ?? RolesModule.Role
  const value = role?.[name]
  if (value === undefined) throw new Error(`unknown Role '${name}'`)
  return value
}

test('WHAT[DELEG-013] EXEC_016_outstandingBackground_false_for_roles_without_join', () => {
  // No journal, no live PTY: join-less roles must never trip the guard.
  for (const name of ['Coder', 'Reviewer', 'Inspector', 'Browser', 'Inquiry', 'Distiller', 'Blogger']) {
    assert.equal(
      outstandingBackground(undefined, () => true, agentRole(name), sessionId('ses_x')),
      false,
      `${name} has no join duty`,
    )
  }
})

test('WHAT[DELEG-013] EXEC_016_devops_live_pty_alone_is_outstanding', () => {
  assert.equal(
    outstandingBackground(undefined, () => true, agentRole('DevOps'), sessionId('ses_devops')),
    true,
  )
  assert.equal(
    outstandingBackground(undefined, () => false, agentRole('DevOps'), sessionId('ses_devops')),
    false,
  )
})

test('WHAT[DELEG-013] EXEC_016_manager_without_journal_is_not_outstanding', () => {
  assert.equal(
    outstandingBackground(undefined, () => true, agentRole('Manager'), sessionId('ses_mgr')),
    false,
  )
})
