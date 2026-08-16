// Join v2 wire contract through the delegation-owned JoinSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]|work_record\s*=/
const completed = (id, name, record = '') => ({ kind: 'completed', agentId: id, agentName: name, workRecord: record })

test('WHAT[DELEG-005] JOIN_V2_completed_agent_is_natural_language_plus_work_record', () => {
  const wire = join.renderBatch('english', [completed('a1', 'fast-coder', 'entry-local work')])
  assert.match(wire, /# fast-coder has returned\./)
  assert.match(wire, /entry-local work/)
  assert.ok(!LEGACY_DTO.test(wire))
})
test('WHAT[DELEG-013] JOIN_V2_failed_agent_is_natural_language', () => {
  const wire = join.renderBatch('english', [{ kind: 'failed', agentId: 'a2', agentName: 'deep-reviewer', code: 'E', message: 'failed' }])
  assert.match(wire, /could not complete/)
  assert.ok(!LEGACY_DTO.test(wire))
})
test('WHAT[DELEG-014] JOIN_V2_abandoned_agent_is_natural_language', () => {
  const wire = join.renderBatch('english', [{ kind: 'abandoned', agentId: 'a2', agentName: 'deep-reviewer', reason: 'abandoned' }])
  assert.match(wire, /did not return/)
  assert.ok(!LEGACY_DTO.test(wire))
})
test('WHAT[DELEG-015] JOIN_V2_interrupted_reason_is_natural_language', () => {
  const wire = join.renderInterrupted('english', 'UserMessageArrived')
  assert.match(wire, /Something nearer has arrived/)
  assert.ok(!LEGACY_DTO.test(wire))
})
test('WHAT[DELEG-016] JOIN_V2_empty_batch_is_plain_empty_wire', () => {
  assert.equal(join.renderBatch('english', []), '')
})
test('WHAT[DELEG-005] JOIN_V2_rendered_wire_is_parseable_without_legacy_fields', () => {
  const wire = join.renderBatch('english', [completed('a1', 'fast-coder', 'ok')])
  assert.doesNotThrow(() => parseToml(wire))
  assert.ok(!wire.includes('work_record ='))
})
