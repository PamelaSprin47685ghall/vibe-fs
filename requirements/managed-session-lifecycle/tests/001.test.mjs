import assert from 'node:assert/strict'
import test from 'node:test'
import * as attachment from '../../../dist/Execution/Session/Attachment/AttachmentSurface.js'

test('WHAT[managed-session-lifecycle-001] one attachment owner binds current Engineer requests once', async () => {
  const observed = await attachment.scenario('owner', 'Engineer', 'engineer', 'engineer', true)
  assert.deepEqual(observed, {
    owner: 'owner', role: 'engineer', created: 1,
    firstChild: 'child-1', secondChild: 'child-1',
    firstAgent: 'engineer', secondAgent: 'engineer',
  })
})

test('WHAT[managed-session-lifecycle-001] unknown role input cannot silently exercise another role', async () => {
  await assert.rejects(attachment.scenario('owner', 'unknown', 'engineer', 'engineer', true), /Unknown attachment role/)
})

test.todo('WHAT[managed-session-lifecycle-001] all AttachmentKinds use the same creation, recovery and cleanup owner through public Host paths (GAP-133)')
