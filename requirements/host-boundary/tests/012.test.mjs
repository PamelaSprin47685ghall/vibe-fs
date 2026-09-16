import assert from 'node:assert/strict'
import test from 'node:test'
import * as loc from '../../../dist/OpenCode/Host/SessionSnapshotLocalitySurface.js'

test('WHAT[HOST-BOUNDARY-012] TODO-004 resolves a tool callback through its persisted assistant run and Host ToolPart', () => {
  const resolved = loc.resolveToolCallback('ses-1', 'call-1', { assistantRunId: 'run-1', toolPartId: 'tp-1' })
  assert.equal(resolved.ok, true)
  assert.equal(resolved.context.assistantRunId, 'run-1')
})
