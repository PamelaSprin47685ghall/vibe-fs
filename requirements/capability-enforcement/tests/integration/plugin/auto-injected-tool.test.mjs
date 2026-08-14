// requirements/capability-enforcement/tests/integration/plugin/auto-injected-tool.test.mjs — HOST-013: placeholder not registered in hooks.tool.
//
// Layer 3: `-` and `auto-injected` are NOT real Tool.Defs; they must not exist in hooks.tool.

import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../../../verification-system/tests/support/plugin-fixture.mjs'

test('HOST_013_auto_injected_and_hyphen_are_not_registered_in_hooks_tool', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-auto-injected', 'fast-coder')
    assert.equal(hooks.tool['auto-injected'], undefined, 'auto-injected must not be in hooks.tool')
    assert.equal(hooks.tool['-'], undefined, '- must not be in hooks.tool')
  })
})
