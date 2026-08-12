import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  idValue,
  sessionSnapshot,
  toolCallId,
} from '../support/domain.mjs'

const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [
    {
      id: partID,
      type: 'tool',
      tool: 'todowrite',
      callID,
      state: { status, input: { todos: [{ content: 'Ship locality', status: 'in_progress', priority: 'high' }] } },
    },
  ],
})

test('TODO-004 resolves a tool callback through its persisted assistant run and Host ToolPart', () => {
  const messages = sessionSnapshot.projectMessages([assistantToolMessage()])
  const located = sessionSnapshot.locateToolCall(toolCallId('call_todo'), messages)

  assert.equal(located.ok, true, located.ok ? '' : JSON.stringify(located.error))
  assert.equal(idValue.providerRun(located.value.ProviderRun), 'asst_run')
  assert.equal(idValue.hostToolPart(located.value.HostToolPartId), 'part_todo')
  assert.equal(idValue.toolCall(located.value.ToolCallId), 'call_todo')
  assert.equal(located.value.ToolName, 'todowrite')
  assert.equal(caseOf(located.value.State), 'Pending')
  assert.equal(located.value.InputCanonical, '{"todos":[{"content":"Ship locality","priority":"high","status":"in_progress"}]}')
})

test('HOST-004 keeps failed session tool state consistent across Parts and ToolParts', () => {
  const messages = sessionSnapshot.projectMessages([
    {
      info: { id: 'asst_failed', role: 'assistant', finish: 'tool-calls' },
      parts: [
        {
          id: 'part_failed',
          type: 'tool',
          tool: 'read',
          callID: 'call_failed',
          state: {
            status: 'error',
            input: { filePath: 'read_probe.txt' },
            error: 'Tool execution aborted',
            metadata: { interrupted: true },
          },
        },
      ],
    },
  ])

  assert.equal(messages.length, 1)
  assert.equal(messages[0].Parts.length, 1)
  assert.equal(caseOf(messages[0].Parts[0]), 'ToolResult', 'failed physical tool must not reappear as in-flight ToolCall')
  assert.equal(messages[0].ToolParts.length, 1)
  assert.equal(caseOf(messages[0].ToolParts[0].State), 'Failed')
})

test('TODO-004 rejects a call id observed in more than one persisted ToolPart', () => {
  const messages = sessionSnapshot.projectMessages([
    assistantToolMessage({ messageID: 'asst_1', partID: 'part_1' }),
    assistantToolMessage({ messageID: 'asst_2', partID: 'part_2' }),
  ])
  const located = sessionSnapshot.locateToolCall(toolCallId('call_todo'), messages)

  assert.equal(located.ok, false)
  assert.equal(caseOf(located.error), 'Ambiguous')
})
