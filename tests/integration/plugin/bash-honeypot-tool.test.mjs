// tests/integration/plugin/bash-honeypot-tool.test.mjs — AGENT-023.
//
// Layer 3: bash-honeypot through the real hooks.tool.*.execute gate.
// Coder may call it and receives a hard denial body; other roles and unresolved
// roles are rejected at AGENT-007 layer two. No shell runs in any branch.

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../unit/plugin/plugin-fixture.mjs'

test('AGENT_023_coder_receives_hard_denial_and_no_shell', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'coder-bash-honey', 'fast-coder')
    assert.ok(hooks.tool['bash-honeypot'], 'bash-honeypot must be registered')

    const result = parseToml(
      await hooks.tool['bash-honeypot'].execute({}, { sessionID: 'coder-bash-honey', agent: 'fast-coder' }),
    )

    assert.match(result.error, /DENIED/)
    assert.match(result.error, /unauthorized privilege-escalation/i)
    assert.match(result.error, /No command ran/i)
  })
})

test('AGENT_023_bash_honeypot_is_denied_for_non_coder_roles', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    acceptAuthorityRoot(runtime, 'manager-bash-honey', 'fast-manager')
    const result = parseToml(
      await hooks.tool['bash-honeypot'].execute(
        {},
        { sessionID: 'manager-bash-honey', agent: 'fast-manager' },
      ),
    )
    assert.match(result.error, /not permitted for role/)
  })
})

test('AGENT_023_bash_honeypot_is_denied_when_the_role_is_unresolved', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = parseToml(
      await hooks.tool['bash-honeypot'].execute(
        {},
        { sessionID: 'unresolved-bash-honey', agent: 'fast-coder' },
      ),
    )
    assert.match(result.error, /no Authority Root fixes this session's role/)
  })
})
