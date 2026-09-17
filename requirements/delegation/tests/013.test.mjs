import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const join = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");


test('WHAT[DELEG-013] JOIN_COMPLETION_completed_is_rendered_as_entry_local_work_record', () => {
  const wire = join.renderBatch('english', [
    {
      kind: 'completed',
      agentId: 'a1',
      agentName: 'Ada',
      role: 'Coder',
      runId: 'run-a1',
      workRecord: 'Task completed with verifiable evidence.'
    }
  ])
  assert.match(wire, /Task completed with verifiable evidence/)
  assert.match(wire, /has returned/)
})
test('WHAT[DELEG-013] JOIN_COMPLETION_abandoned_is_rendered_as_agent_did_not_return', () => {
  const wire = join.renderBatch('english', [
    {
      kind: 'abandoned',
      agentId: 'a2',
      agentName: 'Bob',
      role: 'DevOps',
      reason: 'ParentCancelled'
    }
  ])
  assert.match(wire, /did not return/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");
const join = await import("../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js");

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]|work_record\s*=/
const completed = (id, name, record = '') => ({ kind: 'completed', agentId: id, agentName: name, role: 'Coder', runId: `run-${id}`, workRecord: record })

test('WHAT[DELEG-013] JOIN_V2_failed_agent_is_natural_language', () => {
  const wire = join.renderBatch('english', [{ kind: 'failed', agentId: 'a2', agentName: 'engineer', role: 'Engineer', runId: 'run-a2', code: 'E', message: 'failed' }])
  assert.match(wire, /could not complete/)
  assert.ok(!LEGACY_DTO.test(wire))
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");

const source = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/Host/Join.fs', import.meta.url), 'utf8')

test('WHAT[DELEG-013] EXEC_fork_runtime_await_agent_timeout', () => {
  assert.match(source, /let awaitAgent/)
  assert.match(source, /timeoutMs/)
  assert.match(source, /AwaitAgent\(agentId/)
  assert.match(source, /runtime\.Runtime\.AwaitAgent\(agentId|timeoutMs = timeoutMs/)
})
}
