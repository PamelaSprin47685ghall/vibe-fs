import { test } from 'node:test'
import assert from 'node:assert/strict'
import * as toolResultBound from '../../../dist/Host/Contract/ToolResultBound.js'

const hostLines = (text) => text.split('\n').length
const hostBytes = (text) => Buffer.byteLength(text)

test('WHAT[HOST-BOUNDARY-015] TOOL_RESULT_BOUND_constants_match_host_defaults_exactly', () => {
  assert.equal(toolResultBound.HostMaxLines, 2000)
  assert.equal(toolResultBound.HostMaxBytes, 51200)
  assert.equal(toolResultBound.MarkerBytes, Buffer.byteLength(toolResultBound.Marker))
  assert.equal(toolResultBound.ContentMaxLines + 2, toolResultBound.HostMaxLines)
  assert.equal(toolResultBound.MarkerBytes + toolResultBound.ContentMaxBytes, toolResultBound.HostMaxBytes)
})

test('WHAT[HOST-BOUNDARY-015] TOOL_RESULT_BOUND_under_limit_is_identity', () => {
  const text = 'status = "completed"\nagent = "fast-coder"\n'
  assert.equal(toolResultBound.bound(text), text)
})

test('WHAT[HOST-BOUNDARY-015] TOOL_RESULT_BOUND_over_lines_keeps_tail_and_stays_under_host', () => {
  const text = Array.from({ length: 2500 }, (_, index) => `L${index}`).join('\n')
  const output = toolResultBound.bound(text)
  assert.notEqual(output, text)
  assert.equal(output.slice(0, toolResultBound.Marker.length), toolResultBound.Marker)
  assert.equal(output.includes('L0\n'), false)
  assert.equal(output.includes('L2499'), true)
  assert.equal(hostLines(output) <= toolResultBound.HostMaxLines, true)
  assert.equal(hostBytes(output) <= toolResultBound.HostMaxBytes, true)
})

test('WHAT[HOST-BOUNDARY-015] TOOL_RESULT_BOUND_over_bytes_keeps_tail_and_stays_under_host', () => {
  const text = 'HEAD' + 'x'.repeat(60000) + 'TAIL'
  const output = toolResultBound.bound(text)
  assert.equal(output.slice(0, toolResultBound.Marker.length), toolResultBound.Marker)
  assert.equal(output.endsWith('TAIL'), true)
  assert.equal(output.includes('HEAD'), false)
  assert.equal(hostBytes(output) <= toolResultBound.HostMaxBytes, true)
  assert.equal(hostLines(output) <= toolResultBound.HostMaxLines, true)
})

test('WHAT[HOST-BOUNDARY-015] TOOL_RESULT_BOUND_exact_host_edge_is_identity', () => {
  const text = Array.from({ length: 2000 }, (_, index) => `r${index}`).join('\n')
  assert.equal(hostLines(text), 2000)
  assert.equal(hostBytes(text) < toolResultBound.HostMaxBytes, true)
  assert.equal(toolResultBound.bound(text), text)
})
