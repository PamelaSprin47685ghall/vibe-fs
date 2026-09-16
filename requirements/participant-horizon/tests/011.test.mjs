import assert from 'node:assert/strict'
import test from 'node:test'
import * as horizon from '../../../dist/Execution/Session/OpenCode/HorizonSurface.js'

const FORBIDDEN = /\b(agent_id|session_id|pty_id|child_session_id|status|kind|ordinal|has_pending_completion|current_run_id|fallback_peer|tier|role)\s*=/
const agent = (label, status = 'active', work = 'none', record = '') => ({ label, status, work, record })
const pty = (ptyId, command) => ({ ptyId, command })

test('WHAT[PARTICIPANT-HORIZON-011] EXEC_005_horizon_shows_only_each_visible_subagent_latest_work_record', () => {
  const text = horizon.render([
    agent('coder', 'active', 'latest', 'Patched the parser and the focused regression is green.'),
    agent('inquiry', 'active', 'latest', 'Mapped the release boundary and found no remaining blocker.'),
  ], [])
  assert.match(text, /latest work record/i)
  assert.match(text, /Patched the parser and the focused regression is green\./)
  assert.match(text, /Mapped the release boundary and found no remaining blocker\./)
  assert.doesNotMatch(text, /Investigated the parser and the focused regression/) 
})

test('WHAT[PARTICIPANT-HORIZON-011] EXEC_005_horizon_says_when_visible_subagent_has_no_work_record', () => {
  assert.match(horizon.render([agent('coder')], []), /coder has no work record yet\./i)
})

test('WHAT[PARTICIPANT-HORIZON-011] EXEC_005_horizon_does_not_fall_back_when_latest_work_record_is_unreadable', () => {
  const text = horizon.render([agent('coder', 'active', 'unavailable', '')], [])
  assert.match(text, /latest work record cannot be read right now/i)
  assert.doesNotMatch(text, /Old record that must not masquerade as current progress\./)
})

test('WHAT[PARTICIPANT-HORIZON-011] EXEC_005_horizon_pull_only_returns_synchronously_without_background_wait', () => {
  const start = Date.now()
  const rendered = horizon.render([
    agent('coder', 'active', 'latest', 'Work in progress'),
  ], [])
  const duration = Date.now() - start
  assert.ok(duration < 50, 'render must return immediately as a pure pull projection')
  assert.match(rendered, /Work in progress/)
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_abandoned_child_remains_visible_until_join_retires_it', () => {
  const rendered = horizon.render([
    agent('Ada', 'abandoned'),
    agent('Bob', 'active', 'latest', 'Working on task'),
  ], [])
  assert.match(rendered, /Ada did not return/i, 'abandoned child must remain visible with did-not-return notice')
  assert.match(rendered, /Bob is still away/i, 'active child must remain visible with still-away notice')
  assert.match(rendered, /Working on task/)
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_no_journal_reports_projection_unavailable', () => {
  assert.match(horizon.unavailable(), /horizon is unavailable/i)
  assert.ok(!/\berror\s*=/.test(horizon.unavailable()))
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_runtime_error_is_surfaced', () => {
  assert.match(horizon.cannotBeSeen(), /horizon cannot be seen/i)
  assert.ok(!/\berror\s*=/.test(horizon.cannotBeSeen()))
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_lists_active_agent_by_byname_and_open_terminals_in_natural_language', () => {
  const text = horizon.render([agent('Ada')], [pty('pty-2', 'npm test'), pty('pty-1', 'tail -f')])
  assert.match(text, /# Ada is still away\./)
  assert.doesNotMatch(text, /coder/)
  assert.match(text, /# tail -f remains open\./)
  assert.match(text, /# npm test remains open\./)
  assert.ok(!FORBIDDEN.test(text))
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_completed_awaiting_join_reports_returned', () => {
  const text = horizon.render([agent('coder', 'returned')], [])
  assert.match(text, /# coder has returned\./)
  assert.ok(!FORBIDDEN.test(text))
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_active_agent_without_runtime_defaults_to_still_away', () => {
  assert.match(horizon.render([agent('coder')], []), /# coder is still away\./)
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_unmanaged_target_agent_renders_bare_identity', () => {
  assert.match(horizon.render([agent('some-raw-agent', 'active')], []), /# some-raw-agent is still away\./)
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_empty_journal_lists_only_ptys', () => {
  const text = horizon.render([], [pty('pty-9', 'watch logs')])
  assert.match(text, /# watch logs remains open\./)
  assert.ok(!text.includes('coder'))
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_empty_roster_has_quiet_instruction', () => {
  assert.match(horizon.render([], []), /Nothing beyond your immediate sight/)
})
