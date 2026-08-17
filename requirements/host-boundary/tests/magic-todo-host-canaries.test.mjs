// HOST-BOUNDARY-019 Magic Todo V1 membrane canaries — Host SDK snapshot
// location (canary H physical sub-contract).
//
// The membrane canaries A–R are proven in magic-todo-membrane-canaries.test.mjs
// using production registered surfaces. This file retains the Host SDK snapshot
// location proof for canary H: the Host's persisted assistant run + ToolPart
// uniquely locate a tool callback through (messageId, partId, callId).
//
// Uses the registered Host boundary surface over production snapshot locality.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as host from '../../../dist/OpenCode/Host/HostBoundarySurface.js'

const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[HOST-BOUNDARY-019] CANARY_H journal xtrace uniquely completes host carrier', () => {
  const located = host.locateToolCall('call_todo', [assistantToolMessage({ status: 'completed' })])
  assert.equal(located.ok, true)
  assert.equal(located.providerRun, 'asst_run')
  assert.equal(located.hostToolPartId, 'part_todo')
  assert.equal(located.toolCallId, 'call_todo')
})

test('WHAT[HOST-BOUNDARY-019] CANARY_H journal mapping fails closed on host part mismatch', () => {
  const located = host.locateToolCall('call_missing', [{ info: { id: 'ses_x' }, parts: [{ type: 'text', text: 'not a tool' }] }])
  assert.equal(located.ok, false)
  assert.equal(located.error, 'Missing')
})
