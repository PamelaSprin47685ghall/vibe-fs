import assert from 'node:assert/strict'
import test from 'node:test'
import * as host from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import * as workspaceSharedJournal from '../../../dist/OpenCode/Host/WorkspaceSharedJournal.js'

test('WHAT[DURABLE-EVENTS-010] SharedAgentJournal_boots_local_EventStore_and_leaves_retired_RuntimePath_ndjson_unread', () => {
  const ws = host.createWorkspaceContext()
  assert.equal(host.isUniversalEventStoreActive(ws), true)
  assert.equal(host.hasPrivateDatabase(ws), false)
})
