import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const projection = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");

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

test('WHAT[host-boundary-020] HOST_012_tool_part_shape_decodes_to_wire_tool_result', () => {
  const view = projection.decodeWireParts([assistantTool()])
  assert.equal(view[0].kind, 'ToolResult')
  assert.equal(view[0].callId, 'call_1')
  assert.equal(view[0].result, 'result')
})
test('WHAT[host-boundary-020] HOST_012_legacy_tool_result_shape_still_decodes', () => {
  const view = projection.decodeWireParts([legacyResult()])
  assert.equal(view[0].kind, 'ToolResult')
  assert.equal(view[0].callId, 'legacy')
  assert.equal(view[0].result, 'legacy')
})
test('WHAT[host-boundary-020] HOST_012_tool_error_part_enters_digest', () => {
  const view = projection.decodeWireParts([assistantTool({ status: 'error', output: 'failed' })])
  assert.equal(view[0].kind, 'ToolResult')
  assert.equal(view[0].result, 'failed')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const SessionSnapshotSurface = await import("../../../dist/OpenCode/Host/SessionSnapshotSurface.js");

const projectMessages = SessionSnapshotSurface.projectMessages
const locateToolCall = SessionSnapshotSurface.locateToolCall
const toolPartStateAt = SessionSnapshotSurface.toolPartStateAt
const assistantToolMessage = ({ messageID = 'asst_run', partID = 'part_todo', callID = 'call_todo', status = 'pending' } = {}) => ({
  info: { id: messageID, role: 'assistant' },
  parts: [{ type: 'tool', id: partID, callID, tool: 'auto-injected', state: { status } }],
})

test('WHAT[host-boundary-020] snapshot location accepts exactly one target and fails closed for missing or ambiguous evidence', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const XWireSurface = await import("../../../dist/Context/Prefix/XWireSurface.js");

const baseProjection = {
  messages: [
    { role: 'user', parts: [{ kind: 'text', text: 'hello' }] },
    { role: 'assistant', parts: [{ kind: 'text', text: 'answer' }] },
  ],
}
const acceptedRetryInput = (overrides = {}) => ({
  journal: true,
  sessionId: 'ses_x',
  acceptedRetry: true,
  failures: 1,
  prefixEpoch: 0,
  physicalUser: 'user-1',
  acceptedPhysicalUser: 'user-1',
  snapshotPort: true,
  currentProjection: baseProjection,
  committedSnapshot: null,
  coverableCutoff: 2, // material exists (coverage ahead of request)
  coveredDigest: XWireSurface.coveredPrefixDigest(baseProjection, 1),
  requestStartCutoff: 1,
  frozenRecordPrefixRef: 'blob/ref/frozen-1',
  frozenRecordPrefixDigest: 'sha256:frozen-1',
  frozenRecordPrefixBody: 'frozen record prefix body text',
  memoryPreamble: 'companion memory preamble',
  outcome: null,
  ...overrides,
})

test('WHAT[host-boundary-020] XWIRE_missing_prefix_epoch_fail_closed', () => {
  const withoutEpoch = acceptedRetryInput()
  delete withoutEpoch.prefixEpoch
  const result = XWireSurface.transform(withoutEpoch)
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /prefix epoch/)
})
test('WHAT[host-boundary-020] XWIRE_malformed_prefix_epoch_fail_closed', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ prefixEpoch: 'not-an-epoch' }))
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /prefix epoch/)
})
test('WHAT[host-boundary-020] XWIRE_missing_frozen_prefix_body_fail_closed', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ frozenRecordPrefixBody: undefined }))
  assert.equal(result.ok, false)
  assert.equal(result.noop, false)
  assert.match(result.error, /frozen record prefix body/)
})
test('WHAT[host-boundary-020] XWIRE_covered_digest_mismatch_refuses_the_probe_fail_closed', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ coveredDigest: 'not-the-current-prefix-digest' }))
  assert.equal(result.ok, true)
  assert.equal(result.consumed, true)
  assert.equal(result.changed, false)
  assert.match(result.noProbeReason, /^CutoffProofFailed:/)
  assert.equal(result.probe, null)
})
}
