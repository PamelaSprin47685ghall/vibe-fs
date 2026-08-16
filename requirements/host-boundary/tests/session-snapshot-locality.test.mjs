import assert from 'node:assert/strict'
import test from 'node:test'
import { hostSnapshot } from './support/host-surface.mjs'

const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, state: { status } }],
})

test('WHAT[HOST-BOUNDARY-012] TODO-004 resolves a tool callback through its persisted assistant run and Host ToolPart', () => {
  const messages = hostSnapshot.projectMessages([assistantToolMessage({ status: 'completed' })])
  const located = hostSnapshot.locateToolCall('call_todo', messages)
  assert.equal(located.ok, true)
  assert.deepEqual(located.value, { messageId: 'asst_run', partId: 'part_todo', callId: 'call_todo' })
})

test('WHAT[HOST-BOUNDARY-006] HOST-004 keeps failed session tool state consistent across Parts and ToolParts', () => {
  const messages = hostSnapshot.projectMessages([assistantToolMessage({ status: 'error' })])
  assert.equal(messages[0].parts[0].parts, 'ToolResult')
  assert.equal(messages[0].parts[0].toolParts, 'Failed')
})

test('WHAT[HOST-BOUNDARY-009] TODO-004 rejects a call id observed in more than one persisted ToolPart', () => {
  const messages = hostSnapshot.projectMessages([
    assistantToolMessage({ messageID: 'asst_1', partID: 'part_1' }),
    assistantToolMessage({ messageID: 'asst_2', partID: 'part_2' }),
  ])
  const located = hostSnapshot.locateToolCall('call_todo', messages)
  assert.equal(located.ok, false)
  assert.match(located.error, /Ambiguous/)
})
