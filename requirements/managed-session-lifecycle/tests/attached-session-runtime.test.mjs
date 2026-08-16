import assert from 'node:assert/strict'
import test from 'node:test'
import * as AttachmentSurface from '../../../dist/Execution/Session/Attachment/AttachmentSurface.js'

test('WHAT[MANAGED-SESSION-001] EXEC_026_get_or_create_creates_and_binds_a_work_child_once', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Inspector', 'deep-inspector', 'deep-inspector', true)
  assert.equal(observed.created, 1)
  assert.equal(observed.firstChild, 'child-1')
  assert.equal(observed.secondChild, 'child-1')
  assert.equal(observed.firstAgent, 'deep-inspector')
})

test('WHAT[MANAGED-SESSION-005] EXEC_026_get_or_create_reuses_the_existing_binding_and_keeps_the_bound_agent', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Inspector', 'deep-coder', 'fast-coder', true)
  assert.equal(observed.created, 1)
  assert.equal(observed.secondChild, observed.firstChild)
  assert.equal(observed.secondAgent, 'deep-coder')
})

test('WHAT[MANAGED-SESSION-005] EXEC_026_reuse_scope_is_the_serialization_key_across_sessions', async () => {
  const observed = await AttachmentSurface.scenario('ses-owner-a', 'Inspector', 'deep-coder', 'deep-coder', true)
  assert.equal(observed.owner, 'ses-owner-a')
  assert.equal(observed.firstChild, observed.secondChild)
})

test('WHAT[MANAGED-SESSION-001] EXEC_026_remove_and_remove_by_delegate_session_are_the_only_unbind_paths', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Coder', 'deep-coder', 'deep-coder', true)
  assert.equal(observed.created, 1)
  assert.equal(observed.firstChild, observed.secondChild)
})

test('WHAT[MANAGED-SESSION-005] EXEC_026_unusable_binding_is_treated_as_absent_and_recreated', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Inspector', 'deep-inspector', 'fast-inspector', false)
  assert.equal(observed.created, 2)
  assert.notEqual(observed.firstChild, observed.secondChild)
})
