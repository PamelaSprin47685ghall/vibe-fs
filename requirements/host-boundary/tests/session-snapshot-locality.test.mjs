import assert from 'node:assert/strict'
import test from 'node:test'
import * as SessionSnapshotPort from '../../../dist/OpenCode/Host/SessionSnapshotPort.js'
import { ToolCallIdModule_create, ToolCallIdModule_value, ProviderRunIdentityModule_value, HostToolPartIdModule_value } from '../../../dist/Foundation/Identity.js'

const projectMessages = SessionSnapshotPort.SessionSnapshotPort_projectMessages
const locateToolCall = (callId, messages) =>
  SessionSnapshotPort.SessionSnapshotPort_locateToolCall(ToolCallIdModule_create(callId), messages)

const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[HOST-BOUNDARY-012] TODO-004 resolves a tool callback through its persisted assistant run and Host ToolPart', () => {
  const messages = projectMessages([assistantToolMessage({ status: 'completed' })])
  const located = locateToolCall('call_todo', messages)
  assert.equal(located.tag, 0) // Ok
  const value = located.fields[0]
  assert.equal(ProviderRunIdentityModule_value(value.ProviderRun), 'asst_run')
  assert.equal(HostToolPartIdModule_value(value.HostToolPartId), 'part_todo')
  assert.equal(ToolCallIdModule_value(value.ToolCallId), 'call_todo')
})

test('WHAT[HOST-BOUNDARY-006] HOST-004 keeps failed session tool state consistent across Parts and ToolParts', () => {
  const messages = projectMessages([assistantToolMessage({ status: 'error' })])
  const toolPart = messages.head.ToolParts[0]
  assert.equal(toolPart.State.tag, 2) // SnapshotToolPartState.Failed
})

test('WHAT[HOST-BOUNDARY-009] TODO-004 rejects a call id observed in more than one persisted ToolPart', () => {
  const messages = projectMessages([
    assistantToolMessage({ messageID: 'asst_1', partID: 'part_1' }),
    assistantToolMessage({ messageID: 'asst_2', partID: 'part_2' }),
  ])
  const located = locateToolCall('call_todo', messages)
  assert.equal(located.tag, 1) // Error
  assert.equal(located.fields[0].tag, 1) // Ambiguous
})
