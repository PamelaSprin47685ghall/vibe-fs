import assert from 'node:assert/strict'
import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

integrationTest('WHAT[action-affordance-001] actual registered tool descriptors are nonempty and assume arguments carry descriptions', async () => {
  await withPlugin(async (hooks) => {
    assert.ok(Object.keys(hooks.tool).length > 0)
    for (const [name, tool] of Object.entries(hooks.tool)) {
      assert.equal(typeof tool.description, 'string', name)
      assert.ok(tool.description.trim().length > 0, name)
    }
    for (const argument of ['assumption']) {
      assert.ok(hooks.tool.assume.args[argument].description?.trim().length > 0, argument)
    }
  })
})

test.todo('WHAT[action-affordance-001] every nontrivial action description answers all five semantic questions; nonempty descriptions are only a prerequisite (GAP-078)')
