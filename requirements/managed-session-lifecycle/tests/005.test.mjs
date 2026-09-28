import assert from 'node:assert/strict'
import test from 'node:test'
import * as AttachmentSurface from '../../../dist/Execution/Session/Attachment/AttachmentSurface.js'



test('WHAT[managed-session-lifecycle-005] EXEC_026_get_or_create_reuses_the_existing_binding_and_keeps_the_bound_agent', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Engineer', 'engineer', 'different-requested-agent', true)
  assert.equal(observed.created, 1)
  assert.equal(observed.secondChild, observed.firstChild)
  assert.equal(observed.secondAgent, 'engineer')
})

test('WHAT[managed-session-lifecycle-005] two requests from one owner reuse the binding', async () => {
  const observed = await AttachmentSurface.scenario('ses-owner-a', 'Engineer', 'engineer', 'engineer', true)
  assert.equal(observed.owner, 'ses-owner-a')
  assert.equal(observed.firstChild, observed.secondChild)
})

test('WHAT[managed-session-lifecycle-005] explicit removal permits a new binding on the same owner', async () => {
  const observed = await AttachmentSurface.scenario('owner', 'Engineer', 'engineer', 'engineer', false)
  assert.equal(observed.created, 2)
  assert.notEqual(observed.firstChild, observed.secondChild)
})

test.todo('WHAT[managed-session-lifecycle-005] different live scopes share no child while concurrent same-scope requests share one, through the actual scope owner (GAP-133)')
