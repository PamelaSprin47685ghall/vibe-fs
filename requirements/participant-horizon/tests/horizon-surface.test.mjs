// Horizon output remains pull-only natural language and never exposes Y/DTO
// internals. Work-record latest/unreadable distinctions are owner output laws.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFile } from 'node:fs/promises'
import * as horizon from '../../../dist/Execution/Session/OpenCode/HorizonSurface.js'

const FORBIDDEN = /\b(agent_id|session_id|pty_id|child_session_id|status|kind|ordinal|has_pending_completion|current_run_id|fallback_peer|tier|role)\s*=|completed-awaiting-join|running|busy/
const agent = (label, status = 'active', work = 'none', record = '') => ({ label, status, work, record })

const sourcePath = new URL('../../../src/Wanxiangshu/Execution/Session/OpenCode/HorizonTool.fs', import.meta.url)

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

test('WHAT[PARTICIPANT-HORIZON-011] EXEC_005_horizon_has_no_polling_or_background_wait_primitive', async () => {
  const source = await readFile(sourcePath, 'utf8')
  assert.doesNotMatch(source, /AwaitChangeFrom|Task\.Delay|setInterval|setTimeout|System\.Timers|PeriodicTimer/)
})

test('WHAT[PARTICIPANT-HORIZON-011] HORIZON_abandoned_child_remains_visible_until_join_retires_it', async () => {
  const rendered = horizon.render([agent('Ada', 'abandoned')], [])
  assert.match(rendered, /Ada did not return/i)

  const source = await readFile(sourcePath, 'utf8')
  assert.match(source, /HandleProjection\.horizonVisible handles/)
  assert.doesNotMatch(source, /HandleProjection\.listable handles/)
})
