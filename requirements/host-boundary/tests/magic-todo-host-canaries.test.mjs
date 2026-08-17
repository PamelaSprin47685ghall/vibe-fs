// HOST-BOUNDARY-019 Magic Todo V1 membrane canaries — Host SDK snapshot
// location (canary H physical sub-contract).
//
// The membrane canaries A–R are proven in magic-todo-membrane-canaries.test.mjs
// using production registered surfaces. This file retains the Host SDK snapshot
// location proof for canary H: the Host's persisted assistant run + ToolPart
// uniquely locate a tool callback through (messageId, partId, callId).
//
// Uses production SessionSnapshotSurface.locateToolCall — JS-native plain
// objects only, no raw Fable representation.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as SessionSnapshotSurface from '../../../dist/OpenCode/Host/SessionSnapshotSurface.js'

const projectMessages = SessionSnapshotSurface.projectMessages
const locateToolCall = SessionSnapshotSurface.locateToolCall

const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[HOST-BOUNDARY-019] CANARY_H journal xtrace uniquely completes host carrier', () => {
  const messages = projectMessages([assistantToolMessage({ status: 'completed' })])
  const located = locateToolCall('call_todo', messages)
  assert.equal(located.ok, true)
  assert.equal(located.providerRun, 'asst_run')
  assert.equal(located.hostToolPartId, 'part_todo')
  assert.equal(located.toolCallId, 'call_todo')
})

test('WHAT[HOST-BOUNDARY-019] CANARY_H journal mapping fails closed on host part mismatch', () => {
  const messages = projectMessages([{ info: { id: 'ses_x' }, parts: [{ type: 'text', text: 'not a tool' }] }])
  const located = locateToolCall('call_missing', messages)
  assert.equal(located.ok, false)
  assert.equal(located.error, 'Missing')
})
