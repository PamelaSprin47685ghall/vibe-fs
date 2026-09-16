// tests/integration/plugin/bash-honeypot-tool.test.mjs — AGENT-023.
//
// Layer 3: bash-honeypot through the real hooks.tool.*.execute gate.
// Coder may call it and receives a hard denial body; other roles and unresolved
// roles are rejected at AGENT-007 layer two. No shell runs in any branch.

import assert from 'node:assert/strict'
import test from 'node:test'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[ENF-010] AGENT_023_coder_receives_hard_denial_and_no_shell', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'coder-bash-honey', 'coder')
    assert.ok(hooks.tool['bash-honeypot'], 'bash-honeypot must be registered')

    const result = await hooks.tool['bash-honeypot'].execute(
      {},
      { sessionID: 'coder-bash-honey', agent: 'coder' },
    )

    assert.match(result, /DENIED/)
    assert.match(result, /unauthorized privilege-escalation|提权/)
    assert.match(result, /No command ran|没有运行任何命令/)
  })
})

test('WHAT[ENF-010] AGENT_023_bash_honeypot_is_denied_for_non_coder_roles', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'manager-bash-honey', 'manager')
    const result = await hooks.tool['bash-honeypot'].execute(
      {},
      { sessionID: 'manager-bash-honey', agent: 'manager' },
    )
    assert.match(result, /not available to Manager|对 Manager 不可用/)
  })
})

test('WHAT[ENF-010] AGENT_023_bash_honeypot_is_denied_when_the_role_is_unresolved', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = await hooks.tool['bash-honeypot'].execute(
      {},
      { sessionID: 'unresolved-bash-honey', agent: 'coder' },
    )
    assert.match(result, /authority is established|权威确立/)
  })
})
