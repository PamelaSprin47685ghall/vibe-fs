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

test('WHAT[DELEG-013] JOIN_FIFO_window_bounds_batch_and_prompts_manager_to_continue', () => {
  const item1 = completed('a1', 'agent1', Array.from({ length: 800 }, (_, i) => `line a1 ${i}`).join('\n'))
  const item2 = completed('a2', 'agent2', Array.from({ length: 800 }, (_, i) => `line a2 ${i}`).join('\n'))
  const item3 = completed('a3', 'agent3', Array.from({ length: 800 }, (_, i) => `line a3 ${i}`).join('\n'))

  const wire = join.renderBatch('english', [item1, item2, item3])
  assert.match(wire, /# agent1 has returned\./)
  assert.match(wire, /# agent2 has returned\./)
  assert.doesNotMatch(wire, /# agent3 has returned\./)
  assert.match(wire, /Remaining completions are available\. Continue calling join to receive them\./)
})

test('WHAT[DELEG-013] JOIN_FIFO_all_fit_in_window_does_not_prompt_continue', () => {
  const item1 = completed('a1', 'agent1', 'work 1')
  const item2 = completed('a2', 'agent2', 'work 2')

  const wire = join.renderBatch('english', [item1, item2])
  assert.match(wire, /# agent1 has returned\./)
  assert.match(wire, /# agent2 has returned\./)
  assert.doesNotMatch(wire, /Remaining completions are available/)
})

test('WHAT[DELEG-013] JOIN_FIFO_single_large_item_exceeding_window_still_returned', () => {
  const largeItem = completed('a1', 'agent1', Array.from({ length: 2500 }, (_, i) => `line ${i}`).join('\n'))

  const wire = join.renderBatch('english', [largeItem])
  assert.match(wire, /# agent1 has returned\./)
  assert.doesNotMatch(wire, /Remaining completions are available/)
})

test('WHAT[DELEG-013] JOIN_FIFO_zh_CN_prompts_manager_to_continue_join', () => {
  const item1 = completed('a1', 'agent1', Array.from({ length: 1200 }, (_, i) => `line a1 ${i}`).join('\n'))
  const item2 = completed('a2', 'agent2', Array.from({ length: 1200 }, (_, i) => `line a2 ${i}`).join('\n'))

  const wire = join.renderBatch('zh-CN', [item1, item2])
  assert.match(wire, /# agent1 已归来。/)
  assert.doesNotMatch(wire, /# agent2 已归来。/)
  assert.match(wire, /仍有未展示的完成项。请继续调用 join 接收剩余结果。/)
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
