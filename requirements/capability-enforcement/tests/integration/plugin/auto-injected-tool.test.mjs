// requirements/capability-enforcement/tests/integration/plugin/auto-injected-tool.test.mjs — HOST-013: synthetic pair wire borrows Host-owned skill.
//
// Layer 3: `skill` is not plugin-owned; legacy `auto-injected` is not a real Tool.Def.

import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[ENF-006] HOST_013_skill_stays_host_owned_and_legacy_marker_is_not_plugin_registered', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'coder-auto-injected', 'fast-coder')
    assert.equal(hooks.tool['auto-injected'], undefined, 'legacy auto-injected must not be in hooks.tool')
    assert.equal(hooks.tool.skill, undefined, 'skill remains Host-owned rather than plugin-registered')
  })
})
