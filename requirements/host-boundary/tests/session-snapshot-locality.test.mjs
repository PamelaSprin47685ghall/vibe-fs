import assert from 'node:assert/strict'
import test from 'node:test'
import * as SessionSnapshotSurface from '../../../dist/OpenCode/Host/SessionSnapshotSurface.js'

const projectMessages = SessionSnapshotSurface.projectMessages
const locateToolCall = SessionSnapshotSurface.locateToolCall
const toolPartStateAt = SessionSnapshotSurface.toolPartStateAt

const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[HOST-BOUNDARY-012] TODO-004 resolves a tool callback through its persisted assistant run and Host ToolPart', () => {
  const messages = projectMessages([assistantToolMessage({ status: 'completed' })])
  const located = locateToolCall('call_todo', messages)
  assert.equal(located.ok, true)
  assert.equal(located.providerRun, 'asst_run')
  assert.equal(located.hostToolPartId, 'part_todo')
  assert.equal(located.toolCallId, 'call_todo')
})

test('WHAT[HOST-BOUNDARY-006] HOST-004 keeps failed session tool state consistent across Parts and ToolParts', () => {
  const messages = projectMessages([assistantToolMessage({ status: 'error' })])
  const part = toolPartStateAt(messages, 0, 0)
  assert.equal(part.ok, true)
  assert.equal(part.state, 'failed')
})

test('WHAT[HOST-BOUNDARY-009] TODO-004 rejects a call id observed in more than one persisted ToolPart', () => {
  const messages = projectMessages([
    assistantToolMessage({ messageID: 'asst_1', partID: 'part_1' }),
    assistantToolMessage({ messageID: 'asst_2', partID: 'part_2' }),
  ])
  const located = locateToolCall('call_todo', messages)
  assert.equal(located.ok, false)
  assert.equal(located.error, 'Ambiguous')
  assert.equal(located.toolCallId, 'call_todo')
})

test('WHAT[HOST-BOUNDARY-020] snapshot location accepts exactly one target and fails closed for missing or ambiguous evidence', () => {
  const exact = SessionSnapshotSurface.projectMessages([
    assistantToolMessage({ messageID: 'asst_target', partID: 'part_target' }),
    assistantToolMessage({ messageID: 'asst_decoy', partID: 'part_decoy', callID: 'call_other' }),
    { info: { id: 'user_decoy', role: 'user' }, parts: [{ type: 'tool', id: 'part_user', callID: 'call_todo', tool: 'auto-injected' }] },
  ])
  assert.deepEqual(SessionSnapshotSurface.locateToolCall('call_todo', exact), {
    ok: true,
    providerRun: 'asst_target',
    hostToolPartId: 'part_target',
    toolCallId: 'call_todo',
    toolName: 'auto-injected',
    inputCanonical: 'null',
    state: 'pending',
  })

  const missing = SessionSnapshotSurface.projectMessages([
    assistantToolMessage({ messageID: 'asst_decoy', partID: 'part_decoy', callID: 'call_other' }),
  ])
  assert.deepEqual(SessionSnapshotSurface.locateToolCall('call_todo', missing), {
    ok: false,
    error: 'Missing',
    toolCallId: 'call_todo',
  })

  const ambiguous = SessionSnapshotSurface.projectMessages([
    assistantToolMessage({ messageID: 'asst_first', partID: 'part_first' }),
    assistantToolMessage({ messageID: 'asst_second', partID: 'part_second' }),
    assistantToolMessage({ messageID: 'asst_decoy', partID: 'part_decoy', callID: 'call_other' }),
  ])
  assert.deepEqual(SessionSnapshotSurface.locateToolCall('call_todo', ambiguous), {
    ok: false,
    error: 'Ambiguous',
    toolCallId: 'call_todo',
  })
})
