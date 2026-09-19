import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const source = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/LinkageProjection.fs', import.meta.url), 'utf8')

test('WHAT[delegation-005] JOIN_V2_linkage_projection_has_durable_parent_ownership', () => {
  assert.match(source, /DurableParentHandle/)
  assert.match(source, /CompletedAwaitingJoin/)
})
test('WHAT[delegation-005] JOIN_V2_replay_is_idempotent', () => {
  assert.deepEqual(handles.crashScenario('replayed-completed'), handles.crashScenario('completed'))
})
test('WHAT[delegation-005] JOIN_V2_retired_handle_is_not_listable', () => {
  assert.equal(handles.crashScenario('retired').retired, true)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");
const join = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]|work_record\s*=/
const completed = (id, name, record = '') => ({ kind: 'completed', agentId: id, agentName: name, role: 'Coder', runId: `run-${id}`, workRecord: record })

test('WHAT[delegation-005] JOIN_V2_completed_agent_is_natural_language_plus_work_record', () => {
  const wire = join.renderBatch('english', [completed('a1', 'coder', 'entry-local work')])
  assert.match(wire, /# coder has returned\./)
  assert.match(wire, /entry-local work/)
  assert.ok(!LEGACY_DTO.test(wire))
})
test('WHAT[delegation-005] JOIN_V2_rendered_wire_is_parseable_without_legacy_fields', () => {
  const wire = join.renderBatch('english', [completed('a1', 'coder', 'ok')])
  assert.doesNotThrow(() => parseToml(wire))
  assert.ok(!wire.includes('work_record ='))
})
}
