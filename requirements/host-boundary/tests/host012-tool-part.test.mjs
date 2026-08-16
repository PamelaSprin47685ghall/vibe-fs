import assert from 'node:assert/strict'
import test from 'node:test'
import { toolParts } from './support/host-surface.mjs'

const assistantTool = ({ messageID = 'asst_1', partID = 'part_1', callID = 'call_1', status = 'completed', output = 'result' } = {}) => ({
  info: { id: messageID, role: 'assistant', sessionID: 'ses_1' },
  parts: [{ type: 'tool', id: partID, callID, state: { status, output } }],
})

test('WHAT[HOST-BOUNDARY-020] HOST_012_tool_part_shape_decodes_to_wire_tool_result', () => {
  const view = toolParts.decode([assistantTool()])
  assert.equal(view[0].parts[0].parts, 'ToolResult')
  assert.equal(view[0].parts[0].toolParts, 'Completed')
  assert.deepEqual(toolParts.resultDigests(view), [{ callId: 'call_1', status: 'completed', text: 'result' }])
})

test('WHAT[HOST-BOUNDARY-020] HOST_012_legacy_tool_result_shape_still_decodes', () => {
  const view = toolParts.decode([{ info: { role: 'tool', sessionID: 'ses_1' }, parts: [{ type: 'tool-result', callID: 'legacy', state: { status: 'completed', output: 'legacy' } }] }])
  assert.equal(view[0].parts[0].parts, 'ToolResult')
})

test('WHAT[HOST-BOUNDARY-020] HOST_012_tool_error_part_enters_digest', () => {
  const view = toolParts.decode([assistantTool({ status: 'error', output: 'failed' })])
  assert.equal(view[0].parts[0].toolParts, 'Failed')
  assert.equal(toolParts.resultDigests(view)[0].status, 'error')
})
