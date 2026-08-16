// HOST-013 / ENF-010: the bash honeypot is a real parameterless tool with a
// hard denial body. Static identity comes from the tool owner surface; execution
// goes through the plugin's actual registration and role gate.

import assert from 'node:assert/strict'
import test from 'node:test'

import { bashHoneypotContract } from '../../../dist/OpenCode/Tools/ToolSurface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[ENF-010] BASHHONEY_spec_is_parameterless_and_named_bash_honeypot', () => {
  const contract = bashHoneypotContract()
  assert.equal(contract.name, 'bash-honeypot')
  assert.match(contract.description, /[Hh]oneypot/)
  assert.deepEqual(contract.argumentNames, [])
})

test('WHAT[ENF-010] BASHHONEY_execute_returns_hard_denial_and_runs_nothing', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    await acceptAuthorityRoot(runtime, 'ses-honey', 'fast-coder')
    const result = await hooks.tool['bash-honeypot'].execute({}, { sessionID: 'ses-honey', agent: 'fast-coder' })
    assert.doesNotMatch(result, /\berror\s*=/)
    assert.match(result, /DENIED/)
    assert.match(result, /unauthorized privilege-escalation/i)
    assert.match(result, /not permitted to execute bash/i)
    assert.match(result, /No command ran/i)
    assert.match(result, /DevOps/)
  })
})
