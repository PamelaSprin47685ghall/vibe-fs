// requirements/host-boundary/tests/tool-result-bound.test.mjs — ARCH-012 bounded tail-keeping.
// Moved from tests/unit/context/tool-result-bound.test.mjs (cutover Wave 2a); owner: host-boundary.
//
// Custom tool results pre-bound under OpenCode Host Truncate defaults.
// Host: MAX_LINES=2000, MAX_BYTES=51200, default direction=head.
// We keep the tail and size so Host's head pass is a no-op.

import { test } from 'node:test'
import assert from 'node:assert/strict'

const { byteCount } = await import('../../../dist/Foundation/SyntheticTomlSurface.js')
const toolResultBound = await import('../../../dist/Host/Contract/ToolResultBound.js')

const hostLines = (text) => text.split('\n').length
const hostBytes = (text) => byteCount(text)

test('TOOL_RESULT_BOUND_constants_match_host_defaults_exactly', () => {
  assert.equal(toolResultBound.HostMaxLines, 2000)
  assert.equal(toolResultBound.HostMaxBytes, 51200)
  assert.equal(toolResultBound.MarkerBytes, 34)
  assert.equal(byteCount(toolResultBound.Marker), 34)
  assert.equal(toolResultBound.ContentMaxLines, 1998)
  assert.equal(toolResultBound.ContentMaxBytes, 51166)
  // Static identity: content + marker fill Host budgets exactly.
  assert.equal(toolResultBound.MarkerBytes + toolResultBound.ContentMaxBytes, toolResultBound.HostMaxBytes)
  assert.equal(2 + toolResultBound.ContentMaxLines, toolResultBound.HostMaxLines)
})

test('TOOL_RESULT_BOUND_under_limit_is_identity', () => {
  const text = 'status = "completed"\nagent = "fast-coder"\n'
  assert.equal(toolResultBound.bound(text), text)
})

test('TOOL_RESULT_BOUND_over_lines_keeps_tail_and_stays_under_host', () => {
  // 2500 lines → over HostMaxLines; tail of ContentMaxLines kept.
  const lines = Array.from({ length: 2500 }, (_, i) => `L${i}`)
  const text = lines.join('\n')
  const out = toolResultBound.bound(text)

  assert.notEqual(out, text)
  assert.equal(out.startsWith(toolResultBound.Marker), true)
  assert.equal(out.includes('L0\n'), false)
  assert.equal(out.includes('L2499'), true)
  assert.equal(hostLines(out) <= toolResultBound.HostMaxLines, true)
  assert.equal(hostBytes(out) <= toolResultBound.HostMaxBytes, true)

  // Filled to max: marker (2 lines) + 1998 content lines = 2000.
  assert.equal(hostLines(out), toolResultBound.HostMaxLines)
})

test('TOOL_RESULT_BOUND_over_bytes_keeps_tail_and_stays_under_host', () => {
  // Single long line over HostMaxBytes.
  const text = 'HEAD' + 'x'.repeat(60000) + 'TAIL'
  const out = toolResultBound.bound(text)

  assert.equal(out.startsWith(toolResultBound.Marker), true)
  assert.equal(out.endsWith('TAIL'), true)
  assert.equal(out.includes('HEAD'), false)
  assert.equal(hostBytes(out) <= toolResultBound.HostMaxBytes, true)
  assert.equal(hostLines(out) <= toolResultBound.HostMaxLines, true)

  // Marker + content fill HostMaxBytes exactly when single-line overflow.
  assert.equal(hostBytes(out), toolResultBound.HostMaxBytes)
})

test('TOOL_RESULT_BOUND_exact_host_edge_is_identity', () => {
  // 2000 short lines under byte limit → identity (Host would also pass through).
  const lines = Array.from({ length: 2000 }, (_, i) => `r${i}`)
  const text = lines.join('\n')
  assert.equal(hostLines(text), 2000)
  assert.equal(hostBytes(text) < toolResultBound.HostMaxBytes, true)
  assert.equal(toolResultBound.bound(text), text)
})
