// Split from tests/unit/host/terminal-policy.test.mjs (cutover Wave 2a);
// owner: session-ontology. SESSION-ONTOLOGY-013 canonical durable role label：
// roleName 小写化 Role 并处理 None（null/undefined → undefined）。
// tryLinkedChild/mainSealedForBlogger/outstandingBackground 断言归
// managed-session-lifecycle；isTopLevelManager 归 interaction-authority。

import assert from 'node:assert/strict'
import test from 'node:test'

const { Role } = await import('../../../dist/Kernel/Roles.js')
const { roleName } = await import('../../../dist/Infrastructure/OpenCode/Host/TerminalPolicy.js')

test('TPOL_roleName_lowercases_roles_and_handles_none', () => {
  assert.equal(roleName(Role.Manager), 'manager')
  assert.equal(roleName(Role.Coder), 'coder')
  assert.equal(roleName(Role.Orchestrator), 'orchestrator')
  assert.equal(roleName(null), undefined)
  assert.equal(roleName(undefined), undefined)
})
