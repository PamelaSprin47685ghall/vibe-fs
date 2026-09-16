// ENF-012: Single write entry point CanonicalRole -> permission
import assert from 'node:assert/strict'
import test from 'node:test'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'

test('WHAT[ENF-012] registered_js_tools_reject_cross_role_execution', () => {
  for (const caller of allRoleLabels) {
    for (const target of allRoleLabels) {
      if (caller === target) continue
      const toolName = `js-${target}`
      assert.equal(
        rolePredicate(toolName, caller),
        false,
        `ToolRegistry must deny cross-role execution: ${caller} calling ${toolName}`,
      )
    }
  }
})
