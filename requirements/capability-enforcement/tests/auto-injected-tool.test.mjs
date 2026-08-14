// tests/unit/tools/auto-injected-tool.test.mjs — HOST-013 entity: auto-injected.
//
// Empty arguments; execute always returns OK. Work roles are allowed;
// Blogger and Distiller are denied at the role predicate.

import assert from 'node:assert/strict'
import test from 'node:test'
import { listItems } from '../../verification-system/tests/support/domain.mjs'

const { HostToolArguments_$ctor_4E60E31B: makeArgs, HostToolContext } =
  await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec: autoInjectedSpec } =
  await import('../../../dist/Infrastructure/OpenCode/Tools/AutoInjectedTool.js')
const { ToolRegistry_rolePredicate: rolePredicate } =
  await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRegistry.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')

const context = (sessionId) =>
  new HostToolContext(sessionId, undefined, undefined, undefined, undefined, () => () => {})

const caseName = (value) => value.cases()[value.tag]

test('AUTOINJ_spec_is_parameterless_and_named_auto_injected', () => {
  const spec = autoInjectedSpec
  assert.equal(spec.Name, 'auto-injected')
  assert.equal(typeof spec.Description, 'string')
  assert.ok(spec.Description.length > 0)
  assert.deepEqual(listItems(spec.Arguments), [])
})

test('AUTOINJ_execute_returns_OK', async () => {
  const result = await autoInjectedSpec.Execute(makeArgs({}), context('ses-auto'))
  assert.equal(result, 'OK')
})

test('AUTOINJ_rolePredicate_allows_work_roles_and_denies_blogger_distiller', () => {
  const allowed = rolePredicate('auto-injected', undefined, 'ses-auto')
  const work = [
    Role.Manager,
    Role.Orchestrator,
    Role.Coder,
    Role.Inspector,
    Role.Browser,
    Role.Inquiry,
    Role.Reviewer,
    Role.DevOps,
  ]
  for (const role of work) {
    assert.equal(allowed(role), true, `${caseName(role)} must be allowed`)
  }
  assert.equal(allowed(Role.Blogger), false)
  assert.equal(allowed(Role.Distiller), false)
})
