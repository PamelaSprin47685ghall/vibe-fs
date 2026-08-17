// HOST-BOUNDARY-019 Magic Todo V1 membrane canaries — Host SDK snapshot
// location (canary H physical sub-contract).
//
// The membrane canaries A–R are proven in magic-todo-membrane-canaries.test.mjs
// using production registered surfaces. This file retains the Host SDK snapshot
// location proof for canary H: the Host's persisted assistant run + ToolPart
// uniquely locate a tool callback through (messageId, partId, callId).
//
// The snapshot projection logic is a JS-native implementation that mirrors
// the F# SessionSnapshotPort.projectMessages / locateToolCall production code.
// It is used here because there is no registered JS semantic surface for raw
// Host SDK snapshot projection — the membrane surface (MagicTodoMembraneSurface)
// wraps this resolution internally but does not expose the raw projection.

import assert from 'node:assert/strict'
import test from 'node:test'
import { hostSnapshot } from './support/host-surface.mjs'

const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, state: { status } }],
})

test('WHAT[HOST-BOUNDARY-019] CANARY_H journal xtrace uniquely completes host carrier', () => {
  const located = hostSnapshot.locateToolCall('call_todo', [
    { info: { id: 'ses_magic_todo_canary' }, parts: [{ type: 'tool', id: 'part_1', callID: 'call_todo', state: { status: 'completed', output: 'ok' } }] },
  ])
  assert.equal(located.ok, true)
  assert.deepEqual(located.value, { messageId: 'ses_magic_todo_canary', partId: 'part_1', callId: 'call_todo' })
})

test('WHAT[HOST-BOUNDARY-019] CANARY_H journal mapping fails closed on host part mismatch', () => {
  const located = hostSnapshot.locateToolCall('call_missing', [
    { info: { id: 'ses_x' }, parts: [{ type: 'text', text: 'not a tool' }] },
  ])
  assert.equal(located.ok, false)
})
