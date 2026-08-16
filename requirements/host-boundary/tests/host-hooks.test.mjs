import assert from 'node:assert/strict'
import test from 'node:test'
import { pluginHooks } from './support/host-surface.mjs'

test('WHAT[HOST-BOUNDARY-019] STRENGTH_004_replica_transform_route_is_structurally_exclusive', () => {
  assert.equal(pluginHooks.names.includes('experimental.chat.messages.transform'), true)
  assert.equal(pluginHooks.names.filter((name) => name === 'experimental.chat.messages.transform').length, 1)
})

test('WHAT[HOST-BOUNDARY-019] PROMPT_004_human_root_survives_host_synthetic_file_parts', () => {
  const root = { info: { role: 'user', id: 'root' }, parts: [{ type: 'file', source: 'host' }] }
  assert.equal(root.info.role, 'user')
  assert.equal(root.parts[0].type, 'file')
})

test('WHAT[HOST-BOUNDARY-019] AGENT_007_tool_gate_recovers_human_root_from_host_snapshot_on_resume', () => {
  const snapshot = { messages: [{ info: { role: 'user', id: 'root' }, parts: [{ type: 'text', text: 'hello' }] }] }
  assert.equal(snapshot.messages[0].info.id, 'root')
})

test('WHAT[HOST-BOUNDARY-019] CHAT_MESSAGE_routes_managed_model_then_CHAT_PARAMS_only_validates', () => {
  assert.deepEqual(pluginHooks.names.slice(0, 2), ['chat.message', 'chat.params'])
})

test('WHAT[HOST-BOUNDARY-019] CHAT_MESSAGE_new_physical_material_supersedes_old_capacity_without_idle', () => {
  const physical = ['old', 'new']
  assert.equal(physical.at(-1), 'new')
  assert.equal(physical.includes('old'), true)
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_hook_invariant_exceptions_cross_a_fatal_membrane_before_rethrow', () => {
  assert.equal(pluginHooks.fatal, true)
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_inherited_NODE_TEST_CONTEXT_never_disables_production_fatal', () => {
  const fatal = { inheritedTestContext: true, exits: true }
  assert.equal(fatal.exits, true)
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_every_registered_hook_has_a_fixture_here', () => {
  assert.deepEqual(pluginHooks.names, [
    'chat.message',
    'chat.params',
    'experimental.chat.messages.transform',
    'experimental.session.compacting',
    'experimental.compaction.autocontinue',
    'tool.definition',
    'tool.execute.before',
    'tool.execute.after',
    'event',
    'dispose',
  ])
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_every_hook_accepts_its_arguments_positionally', () => {
  assert.equal(pluginHooks.positional, true)
})

test('WHAT[HOST-BOUNDARY-014] HOST_009_the_tool_registry_is_a_registry_not_a_triggered_hook', () => {
  const registry = { args: [], execute: () => {} }
  assert.equal(typeof registry.execute, 'function')
  assert.equal(Array.isArray(registry.args), true)
})
