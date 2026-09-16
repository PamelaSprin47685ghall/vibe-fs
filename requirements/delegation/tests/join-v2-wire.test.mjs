// Join v2 wire contract through the delegation-owned JoinSurface.
import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]|work_record\s*=/
const completed = (id, name, record = '') => ({ kind: 'completed', agentId: id, agentName: name, role: 'Coder', runId: `run-${id}`, workRecord: record })

test('WHAT[DELEG-005] JOIN_V2_completed_agent_is_natural_language_plus_work_record', () => {
  const wire = join.renderBatch('english', [completed('a1', 'coder', 'entry-local work')])
  assert.match(wire, /# coder has returned\./)
  assert.match(wire, /entry-local work/)
  assert.ok(!LEGACY_DTO.test(wire))
})
test('WHAT[DELEG-013] JOIN_V2_failed_agent_is_natural_language', () => {
  const wire = join.renderBatch('english', [{ kind: 'failed', agentId: 'a2', agentName: 'inspector', role: 'Inspector', runId: 'run-a2', code: 'E', message: 'failed' }])
  assert.match(wire, /could not complete/)
  assert.ok(!LEGACY_DTO.test(wire))
})
test('WHAT[DELEG-014] JOIN_V2_abandoned_agent_is_natural_language', () => {
  const wire = join.renderBatch('english', [{ kind: 'abandoned', agentId: 'a2', agentName: 'inspector', reason: 'abandoned' }])
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

test('WHAT[DELEG-016] JOIN_V2_malformed_role_run_or_kind_is_rejected_without_success_wire', () => {
  const base = { kind: 'completed', agentId: 'a1', agentName: 'coder', role: 'Coder', runId: 'run-a1', workRecord: 'ok' }
  const { role: _role, ...missingRole } = base
  assert.equal(join.renderBatch('english', [missingRole]), '')
  assert.equal(join.renderBatch('english', [{ ...base, role: 'UnknownRole' }]), '')
  assert.equal(join.renderBatch('english', [{ kind: 'abandoned', agentId: 'a1', agentName: 'coder', role: 'UnknownRole', reason: 'gone' }]), '')
  assert.equal(join.renderBatch('english', [{ ...base, runId: '' }]), '')
  assert.equal(join.renderBatch('english', [{ ...base, kind: 'not-a-kind' }]), '')
})
test('WHAT[DELEG-005] JOIN_V2_rendered_wire_is_parseable_without_legacy_fields', () => {
  const wire = join.renderBatch('english', [completed('a1', 'coder', 'ok')])
  assert.doesNotThrow(() => parseToml(wire))
  assert.ok(!wire.includes('work_record ='))
})
