import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const horizon = await import("../../../dist/Execution/Session/OpenCode/HorizonSurface.js");

const FORBIDDEN = /\b(agent_id|session_id|pty_id|child_session_id|status|kind|ordinal|has_pending_completion|current_run_id|fallback_peer|tier|role)\s*=|completed-awaiting-join|running|busy/
const agent = (label, status = 'active', work = 'none', record = '') => ({ label, status, work, record })

test('WHAT[participant-horizon-011] EXEC_005_horizon_shows_only_each_visible_subagent_latest_work_record', () => {
  const text = horizon.render([
    agent('coder', 'active', 'latest', 'Patched the parser and the focused regression is green.'),
    agent('inquiry', 'active', 'latest', 'Mapped the release boundary and found no remaining blocker.'),
  ], [])
  assert.match(text, /latest work record/i)
  assert.match(text, /Patched the parser and the focused regression is green\./)
  assert.match(text, /Mapped the release boundary and found no remaining blocker\./)
  assert.doesNotMatch(text, /Investigated the parser and the focused regression/) 
})
test('WHAT[participant-horizon-011] EXEC_005_horizon_says_when_visible_subagent_has_no_work_record', () => {
  assert.match(horizon.render([agent('coder')], []), /coder has no work record yet\./i)
})
test('WHAT[participant-horizon-011] EXEC_005_horizon_does_not_fall_back_when_latest_work_record_is_unreadable', () => {
  const text = horizon.render([agent('coder', 'active', 'unavailable', '')], [])
  assert.match(text, /latest work record cannot be read right now/i)
  assert.doesNotMatch(text, /Old record that must not masquerade as current progress\./)
})
test('WHAT[participant-horizon-011] EXEC_005_horizon_pull_only_returns_synchronously_without_background_wait', () => {
  const start = Date.now()
  const rendered = horizon.render([
    agent('coder', 'active', 'latest', 'Work in progress'),
  ], [])
  const duration = Date.now() - start
  assert.ok(duration < 50, 'render must return immediately as a pure pull projection')
  assert.match(rendered, /Work in progress/)
})
test('WHAT[participant-horizon-011] HORIZON_abandoned_child_remains_visible_until_join_retires_it', () => {
  // Both active and abandoned child are projected in the public horizon output
  const rendered = horizon.render([
    agent('Ada', 'abandoned'),
    agent('Bob', 'active', 'latest', 'Working on task'),
  ], [])
  assert.match(rendered, /Ada did not return/i, 'abandoned child must remain visible with did-not-return notice')
  assert.match(rendered, /Bob is still away/i, 'active child must remain visible with still-away notice')
  assert.match(rendered, /Working on task/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const horizon = await import("../../../dist/Execution/Session/OpenCode/HorizonSurface.js");

const FORBIDDEN = /\b(agent_id|session_id|pty_id|child_session_id|status|kind|ordinal|has_pending_completion|current_run_id|fallback_peer|tier|role)\s*=/
const agent = (label, status = 'active', work = 'none', record = '') => ({ label, status, work, record })
const pty = (ptyId, command) => ({ ptyId, command })

test('WHAT[participant-horizon-011] HORIZON_no_journal_reports_projection_unavailable', () => {
  assert.match(horizon.unavailable(), /horizon is unavailable/i)
  assert.ok(!/\berror\s*=/.test(horizon.unavailable()))
})
test('WHAT[participant-horizon-011] HORIZON_runtime_error_is_surfaced', () => {
  assert.match(horizon.cannotBeSeen(), /horizon cannot be seen/i)
  assert.ok(!/\berror\s*=/.test(horizon.cannotBeSeen()))
})
test('WHAT[participant-horizon-011] HORIZON_lists_active_agent_by_byname_and_open_terminals_in_natural_language', () => {
  const text = horizon.render([agent('Ada')], [pty('pty-2', 'npm test'), pty('pty-1', 'tail -f')])
  assert.match(text, /# Ada is still away\./)
  assert.doesNotMatch(text, /coder/)
  assert.match(text, /# tail -f remains open\./)
  assert.match(text, /# npm test remains open\./)
  assert.ok(!FORBIDDEN.test(text))
})
test('WHAT[participant-horizon-011] HORIZON_completed_awaiting_join_reports_returned', () => {
  const text = horizon.render([agent('coder', 'returned')], [])
  assert.match(text, /# coder has returned\./)
  assert.ok(!FORBIDDEN.test(text))
})
test('WHAT[participant-horizon-011] HORIZON_active_agent_without_runtime_defaults_to_still_away', () => {
  assert.match(horizon.render([agent('coder')], []), /# coder is still away\./)
})
test('WHAT[participant-horizon-011] HORIZON_unmanaged_target_agent_renders_bare_identity', () => {
  assert.match(horizon.render([agent('some-raw-agent', 'active')], []), /# some-raw-agent is still away\./)
})
test('WHAT[participant-horizon-011] HORIZON_empty_journal_lists_only_ptys', () => {
  const text = horizon.render([], [pty('pty-9', 'watch logs')])
  assert.match(text, /# watch logs remains open\./)
  assert.ok(!text.includes('coder'))
})
test('WHAT[participant-horizon-011] HORIZON_empty_roster_has_quiet_instruction', () => {
  assert.match(horizon.render([], []), /Nothing beyond your immediate sight/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const fork = await import("../../../dist/Execution/Delegation/Fork/Surface.js");
const forkTool = await import("../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js");

const schemaNode = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
  int: () => schemaNode(`${kind}-int`, extra),
  nonnegative: () => schemaNode(`${kind}-nonnegative`, extra),
})
const toolModule = {
  tool: {
    schema: {
      string: () => schemaNode('string'),
      number: () => schemaNode('number'),
      enum: (values) => schemaNode('enum', { values }),
      array: (inner) => schemaNode('array', { inner }),
    },
  },
}
const waitForPromptCount = (runtime, count) => forkTool.awaitPromptCount(runtime, count)
const ownerDescriptor = (sessionId) => [{ sessionId, agent: 'manager' }]

test('WHAT[participant-horizon-011] FORK_TOOL_abandoned_child_does_not_vanish_from_horizon_before_join', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-abandoned-horizon-'))
  const owner = 'manager-abandoned-horizon'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'VISIBLE-CHARGE')
    await waitForPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)

    await forkTool.cancelOwnerChildren(runtime, owner)

    const roster = await forkTool.executeHorizon(runtime, owner)
    assert.match(roster, /Ada/, 'durable abandoned child must not become indistinguishable from never-created')
    assert.match(roster, /did not return|未从此项任务归来/i)
    assert.doesNotMatch(roster, /no one is currently away|当前没有.*在外/i)
    assert.equal(forkTool.abortCount(runtime), 1, 'authorized logical cancel still physically tears down the child')
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})
}
