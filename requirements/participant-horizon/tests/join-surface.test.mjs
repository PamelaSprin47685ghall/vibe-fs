// Join wire contract through the delegation-owned plain-data surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]|work_record\s*=/
const assertClean = (wire, label) => assert.ok(!LEGACY_DTO.test(wire), `${label}: ${wire}`)

test('WHAT[PARTICIPANT-HORIZON-004] JOIN_SURFACE_completed_batch_is_natural_language_plus_work_record', () => {
  const wire = join.renderBatch('english', [{ kind: 'completed', agentId: 'a1', agentName: 'fast-coder', workRecord: 'Chronicle\nRecent work' }])
  assert.match(wire, /# fast-coder has returned\./)
  assert.match(wire, /Chronicle/)
  assertClean(wire, 'completed')
})

test('WHAT[PARTICIPANT-HORIZON-003] JOIN_SURFACE_interrupt_and_fork_error_are_natural_language_only', () => {
  assertClean(join.renderInterrupted('english', 'OperatorAbort'), 'operator abort')
  assertClean(join.renderForkError('english', 'NothingToJoin'), 'nothing to join')
  assertClean(join.renderForkError('english', 'TimedOut'), 'timed out')
})
