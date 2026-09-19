import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const join = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");


test('WHAT[delegation-014] JOIN_COMPLETION_batch_preserves_order_and_bounded_work_records', () => {
  const wire = join.renderBatch('english', [
    { kind: 'completed', agentId: 'a1', agentName: 'Ada', role: 'Coder', runId: 'run-a1', workRecord: 'First evidence.' },
    { kind: 'completed', agentId: 'a2', agentName: 'Bob', role: 'DevOps', runId: 'run-a2', workRecord: 'Second evidence.' }
  ])
  assert.match(wire, /Ada has returned/)
  assert.match(wire, /Bob has returned/)
  assert.ok(wire.indexOf('Ada') < wire.indexOf('Bob'))
  assert.match(wire, /First evidence/)
  assert.match(wire, /Second evidence/)
  assert.doesNotMatch(wire, /\[\[result\]\]|\[error\]|work_record\s*=/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");
const join = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]|work_record\s*=/
const completed = (id, name, record = '') => ({ kind: 'completed', agentId: id, agentName: name, role: 'Coder', runId: `run-${id}`, workRecord: record })

test('WHAT[delegation-014] JOIN_V2_abandoned_agent_is_natural_language', () => {
  const wire = join.renderBatch('english', [{ kind: 'abandoned', agentId: 'a2', agentName: 'engineer', reason: 'abandoned' }])
  assert.match(wire, /did not return/)
  assert.ok(!LEGACY_DTO.test(wire))
})
}
