import assert from 'node:assert/strict'
import test from 'node:test'
import * as AttachmentSurface from '../../../dist/Execution/Session/Attachment/AttachmentSurface.js'



test('WHAT[managed-session-lifecycle-005] EXEC_026_get_or_create_reuses_the_existing_binding_and_keeps_the_bound_agent', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Engineer', 'coder', 'coder', true)
  assert.equal(observed.created, 1)
  assert.equal(observed.secondChild, observed.firstChild)
  assert.equal(observed.secondAgent, 'coder')
})

test('WHAT[managed-session-lifecycle-005] EXEC_026_reuse_scope_is_the_serialization_key_across_sessions', async () => {
  const observed = await AttachmentSurface.scenario('ses-owner-a', 'Engineer', 'coder', 'coder', true)
  assert.equal(observed.owner, 'ses-owner-a')
  assert.equal(observed.firstChild, observed.secondChild)
})

test('WHAT[managed-session-lifecycle-005] EXEC_026_unusable_binding_is_treated_as_absent_and_recreated', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Engineer', 'engineer', 'engineer', false)
  assert.equal(observed.created, 2)
  assert.notEqual(observed.firstChild, observed.secondChild)
})
