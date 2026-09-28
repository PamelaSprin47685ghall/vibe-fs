import assert from 'node:assert/strict'
import test from 'node:test'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'

test('WHAT[capability-enforcement-012] registered programming tools admit their own role and reject cross-role calls', () => {
  for (const owner of ['engineer', 'devops']) {
    const tool = `js-${owner}`
    assert.equal(rolePredicate(tool, owner), true, `${owner} can use ${tool}`)
    for (const caller of allRoleLabels.filter((role) => role !== owner)) {
      assert.equal(rolePredicate(tool, caller), false, `${caller} cannot use ${tool}`)
    }
    assert.equal(rolePredicate(tool, 'unknown'), false)
  }
})
