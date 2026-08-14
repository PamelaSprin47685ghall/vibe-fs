// tests/integration/plugin/auto-injected-tool.test.mjs — HOST-013 entity through hooks.tool.
//
// Layer 3: auto-injected is a real Tool.Def. A Work role live call returns OK;
// unresolved roles are rejected at AGENT-007 layer two.

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../unit/plugin/plugin-fixture.mjs'

test('HOST_013_auto_injected_is_registered_and_coder_receives_OK', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-auto-injected', 'fast-coder')
    assert.ok(hooks.tool['auto-injected'], 'auto-injected must be registered')
    assert.deepEqual(hooks.tool['auto-injected'].args, {})

    const result = await hooks.tool['auto-injected'].execute(
      {},
      { sessionID: 'coder-auto-injected', agent: 'fast-coder' },
    )
    assert.equal(result, 'OK')
  })
})

test('HOST_013_auto_injected_is_denied_when_the_role_is_unresolved', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = parseToml(
      await hooks.tool['auto-injected'].execute(
        {},
        { sessionID: 'unresolved-auto-injected', agent: 'fast-coder' },
      ),
    )
    assert.match(result.error, /no Authority Root fixes this session's role/)
  })
})
