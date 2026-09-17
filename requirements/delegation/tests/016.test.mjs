import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\[\[result\]\]|\[error\]|work_record\s*=/

const completed = (id, name, record = '') => ({ kind: 'completed', agentId: id, agentName: name, role: 'Coder', runId: `run-${id}`, workRecord: record })

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
