// horizon() roster output crosses the session-owned HorizonSurface as plain data.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as horizon from '../../../dist/Execution/Session/OpenCode/HorizonSurface.js'

const FORBIDDEN = /\b(agent_id|session_id|pty_id|child_session_id|status|kind|ordinal|has_pending_completion|current_run_id|fallback_peer|tier|role)\s*=/
const agent = (label, status = 'active', work = 'none', record = '') => ({ label, status, work, record })
const pty = (ptyId, command) => ({ ptyId, command })

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
  assert.doesNotMatch(text, /fast-coder/)
  assert.match(text, /# tail -f remains open\./)
  assert.match(text, /# npm test remains open\./)
  assert.ok(!FORBIDDEN.test(text))
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_completed_awaiting_join_reports_returned', () => {
  const text = horizon.render([agent('fast-coder', 'returned')], [])
  assert.match(text, /# fast-coder has returned\./)
  assert.ok(!FORBIDDEN.test(text))
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_active_agent_without_runtime_defaults_to_still_away', () => {
  assert.match(horizon.render([agent('fast-coder')], []), /# fast-coder is still away\./)
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_unmanaged_target_agent_renders_bare_identity', () => {
  assert.match(horizon.render([agent('some-raw-agent', 'active')], []), /# some-raw-agent is still away\./)
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_empty_journal_lists_only_ptys', () => {
  const text = horizon.render([], [pty('pty-9', 'watch logs')])
  assert.match(text, /# watch logs remains open\./)
  assert.ok(!text.includes('fast-coder'))
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_empty_roster_has_quiet_instruction', () => {
  assert.match(horizon.render([], []), /Nothing beyond your immediate sight/)
})
