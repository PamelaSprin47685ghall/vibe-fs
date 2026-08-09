// tests/unit/tools/bash-honeypot-tool.test.mjs — VERIFY-009 coverage: bash-honeypot.
//
// No filesystem, no shell: the tool is a fixed denial body with an empty argument list.

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import { listItems } from '../support/domain.mjs'

const { HostToolArguments_$ctor_4E60E31B: makeArgs, HostToolContext } =
  await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec: bashHoneypotSpec } =
  await import('../../../dist/Infrastructure/OpenCode/Tools/BashHoneypotTool.js')

const context = (sessionId) =>
  new HostToolContext(sessionId, undefined, undefined, undefined, undefined, () => () => {})

test('BASHHONEY_spec_is_parameterless_and_named_bash_honeypot', () => {
  const spec = bashHoneypotSpec
  assert.equal(spec.Name, 'bash-honeypot')
  assert.match(spec.Description, /[Hh]oneypot/)
  assert.deepEqual(listItems(spec.Arguments), [])
})

test('BASHHONEY_execute_returns_hard_denial_and_runs_nothing', async () => {
  const raw = await bashHoneypotSpec.Execute(makeArgs({}), context('ses-honey'))
  const result = parseToml(raw)
  assert.match(result.error, /DENIED/)
  assert.match(result.error, /unauthorized privilege-escalation/i)
  assert.match(result.error, /not permitted to execute bash/i)
  assert.match(result.error, /No command ran/i)
  assert.match(result.error, /DevOps/)
})
