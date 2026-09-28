import assert from 'node:assert/strict'
import test from 'node:test'
import * as SessionSnapshotSurface from '../../../dist/OpenCode/Host/SessionSnapshotSurface.js'

const projectMessages = SessionSnapshotSurface.projectMessages

const locateToolCall = SessionSnapshotSurface.locateToolCall

const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[host-boundary-012] a complete snapshot uniquely locates the assistant run and tool part', () => {
  const messages = projectMessages([assistantToolMessage({ status: 'completed' })])
  const located = locateToolCall('call_todo', messages)
  assert.equal(located.ok, true)
  assert.equal(located.providerRun, 'asst_run')
  assert.equal(located.hostToolPartId, 'part_todo')
  assert.equal(located.toolCallId, 'call_todo')
})

test('WHAT[host-boundary-012] missing and duplicate tool call identities are rejected', () => {
  const missing = locateToolCall('missing', projectMessages([assistantToolMessage()]))
  assert.equal(missing.ok, false)
  assert.equal(missing.error, 'Missing')
  const duplicated = locateToolCall('call_todo', projectMessages([
    assistantToolMessage({ messageID: 'run-1', partID: 'part-1' }),
    assistantToolMessage({ messageID: 'run-2', partID: 'part-2' }),
  ]))
  assert.equal(duplicated.ok, false)
  assert.equal(duplicated.error, 'Ambiguous')
})
