import assert from 'node:assert/strict'
import test from 'node:test'
import * as horizon from '../../../dist/Execution/Session/OpenCode/HorizonSurface.js'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const FORBIDDEN = /\b(agent_id|session_id|pty_id|child_session_id|status|kind|ordinal|has_pending_completion|current_run_id|fallback_peer|tier|role)\s*=|completed-awaiting-join|running|busy/
const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\\[\\[result\\]\\]|\\[error\\]/
const agent = (label, status = 'active', work = 'none', record = '') => ({ label, status, work, record })
const completed = (over = {}) => ({ kind: 'completed', agentId: 'a1', agentName: 'coder', role: 'Coder', runId: 'run-a1', workRecord: '', ...over })
const failed = (over = {}) => ({ kind: 'failed', agentId: 'a1', agentName: 'inspector', role: 'Inspector', runId: 'run-a1', code: 'E1', message: 'boom', ...over })

const assertClean = (wire, label) => assert.ok(!LEGACY_DTO.test(wire), `${label}: ${wire}`)

test('WHAT[PARTICIPANT-HORIZON-004] EXEC_005_horizon_description_says_work_record_and_pull_only_without_Y_jargon', () => {
  assert.match(horizon.description(), /latest work record/i)
  assert.match(horizon.description(), /pull-only/i)
  assert.match(horizon.description(), /do not poll/i)
  assert.doesNotMatch(horizon.description(), /\bY\s+work record\b/i)
})

test('WHAT[PARTICIPANT-HORIZON-004] HORIZON_SURFACE_has_no_legacy_roster_dto', () => {
  const text = horizon.render([agent('coder')], [])
  assert.match(text, /# coder is still away\./)
  assert.ok(!FORBIDDEN.test(text), text)
})

test('WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_batch_agent_completed_natural_language_and_work_record', () => {
  const wire = join.renderBatch('english', [completed({ workRecord: 'did the thing' })])
  assert.match(wire, /# coder has returned\./)
  assert.match(wire, /# did the thing/)
  assertClean(wire, 'completed')
  assert.ok(!wire.includes('work_record ='))
})

test('WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_batch_agent_failed_natural_language_consequence', () => {
  const wire = join.renderBatch('english', [failed({ message: 'no' })])
  assert.match(wire, /# inspector could not complete the charge\./)
  assert.match(wire, /# no/)
  assertClean(wire, 'failed')
})

test('WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_batch_agent_abandoned_natural_language', () => {
  const wire = join.renderBatch('english', [{ kind: 'abandoned', agentId: 'a1', agentName: 'inspector', reason: 'operator abort' }])
  assert.match(wire, /# inspector did not return from this charge\./)
  assertClean(wire, 'abandoned')
})

test('WHAT[PARTICIPANT-HORIZON-004] MISC_join_render_completed_managed_agent_name_and_raw_resolve', () => {
  for (const agentName of ['coder', 'inspector', 'weird raw name']) {
    const wire = join.renderBatch('english', [completed({ agentName })])
    assert.match(wire, new RegExp(`# ${agentName} has returned\\.`))
  }
})

test('WHAT[PARTICIPANT-HORIZON-004] JOIN_SURFACE_completed_batch_is_natural_language_plus_work_record', () => {
  const wire = join.renderBatch('english', [{ kind: 'completed', agentId: 'a1', agentName: 'coder', role: 'Coder', runId: 'run-a1', workRecord: 'Chronicle\nRecent work' }])
  assert.match(wire, /# coder has returned\./)
  assert.match(wire, /Chronicle/)
  assertClean(wire, 'completed')
})
