import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[host-provider-failure-ownership-004] actual config hook propagates rejected legacy configuration to its Host caller', async () => {
  await withExecutablePlugin(async hooks => {
    await assert.rejects(async () => hooks.config({ agent: { coder: {} } }), /managed-agent-config-invalid|legacy|retired|deprecated/i)
  })
})

test.todo('WHAT[host-provider-failure-ownership-004] real Host UI retains config permission tool cancel and unknown errors without global suppression (GAP-143)')
