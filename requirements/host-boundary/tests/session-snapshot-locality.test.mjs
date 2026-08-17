import assert from 'node:assert/strict'
import test from 'node:test'
import * as host from '../../../dist/OpenCode/Host/HostBoundarySurface.js'

const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[HOST-BOUNDARY-012] TODO-004 resolves a tool callback through its persisted assistant run and Host ToolPart', () => {
  const located = host.locateToolCall('call_todo', [assistantToolMessage({ status: 'completed' })])
  assert.equal(located.ok, true)
  assert.equal(located.providerRun, 'asst_run')
  assert.equal(located.hostToolPartId, 'part_todo')
  assert.equal(located.toolCallId, 'call_todo')
  assert.equal(located.state.kind, 'completed')
})

test('WHAT[HOST-BOUNDARY-006] HOST-004 keeps failed session tool state consistent across Parts and ToolParts', () => {
  const located = host.locateToolCall('call_todo', [assistantToolMessage({ status: 'error' })])
  assert.equal(located.ok, true)
  assert.equal(located.state.kind, 'failed')
})

test('WHAT[HOST-BOUNDARY-009] TODO-004 rejects a call id observed in more than one persisted ToolPart', () => {
  const located = host.locateToolCall('call_todo', [
    assistantToolMessage({ messageID: 'asst_1', partID: 'part_1' }),
    assistantToolMessage({ messageID: 'asst_2', partID: 'part_2' }),
  ])
  assert.equal(located.ok, false)
  assert.equal(located.error, 'Ambiguous')
})
