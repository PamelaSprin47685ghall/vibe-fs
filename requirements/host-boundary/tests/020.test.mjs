import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/OpenCode/Codec/ProviderProjectionSurface.js'

const assistantTool = ({ callID = 'call_1', status = 'completed', output = 'result' } = {}) => ({
  type: 'tool',
  callID,
  state: { status, output },
})

const legacyResult = ({ callID = 'legacy', output = 'legacy' } = {}) => ({
  type: 'tool-result',
  callID,
  state: { status: 'completed', output },
})

test('WHAT[HOST-BOUNDARY-020] HOST_012_tool_part_shape_decodes_to_wire_tool_result', () => {
  const view = projection.decodeWireParts([assistantTool()])
  assert.equal(view[0].kind, 'ToolResult')
  assert.equal(view[0].callId, 'call_1')
  assert.equal(view[0].result, 'result')
})

test('WHAT[HOST-BOUNDARY-020] HOST_012_legacy_tool_result_shape_still_decodes', () => {
  const view = projection.decodeWireParts([legacyResult()])
  assert.equal(view[0].kind, 'ToolResult')
  assert.equal(view[0].callId, 'legacy')
  assert.equal(view[0].result, 'legacy')
})

test('WHAT[HOST-BOUNDARY-020] HOST_012_tool_error_part_enters_digest', () => {
  const view = projection.decodeWireParts([assistantTool({ status: 'error', output: 'failed' })])
  assert.equal(view[0].kind, 'ToolResult')
  assert.equal(view[0].result, 'failed')
})
