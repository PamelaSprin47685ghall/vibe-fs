import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HostBoundarySurface = await import("../../../dist/OpenCode/Host/HostBoundarySurface.js");
const HostMessageProjection = await import("../../../dist/OpenCode/Host/HostMessageProjection.js");

const sanitizeMessage = HostMessageProjection.sanitizeMessage
const sanitizeMessages = HostBoundarySurface.sanitizeMessages

test('WHAT[host-boundary-011] HOST_016_assistant_message_with_only_reasoning_gets_semantically_empty_dot_text', () => {
  const raw = { info: { id: 'asst_1', role: 'assistant' }, parts: [{ type: 'reasoning', text: 'Step-by-step thinking content' }] }
  const result = sanitizeMessage(raw)
  assert.equal(result.parts.at(-1).type, 'text')
  assert.equal(result.parts.at(-1).text, '.')
})
test('WHAT[host-boundary-011] HOST_016_assistant_message_with_thinking_type_gets_semantically_empty_dot_text', () => {
  const result = sanitizeMessage({ info: { role: 'assistant' }, parts: [{ type: 'thinking', thinking: 'Deep reasoning text' }] })
  assert.equal(result.parts.at(-1).text, '.')
})
test('WHAT[host-boundary-011] HOST_016_assistant_message_with_empty_parts_gets_ellipsis_fallback', () => {
  const result = sanitizeMessage({ info: { role: 'assistant' }, parts: [] })
  assert.equal(result.parts.at(-1).text, '...')
})
test('WHAT[host-boundary-011] HOST_016_user_message_with_empty_parts_gets_hash_fallback', () => {
  const result = sanitizeMessage({ info: { role: 'user' }, parts: [] })
  assert.equal(result.parts.at(-1).text, '#')
})
test('WHAT[host-boundary-011] HOST_016_message_with_existing_text_is_untouched', () => {
  const raw = { info: { role: 'assistant' }, parts: [{ type: 'text', text: 'Formal answer' }] }
  assert.deepEqual(sanitizeMessage(raw), raw)
})
test('WHAT[host-boundary-011] HOST_016_assistant_message_with_tool_call_is_untouched', () => {
  const raw = { info: { role: 'assistant' }, parts: [{ type: 'tool', tool: 'auto-injected', callID: 'g1' }] }
  assert.deepEqual(sanitizeMessage(raw), raw)
})
test('WHAT[host-boundary-011] HOST_016_sanitizeMessages_processes_whole_array', () => {
  const raw = [
    { info: { role: 'user' }, parts: [{ type: 'text', text: 'Hi' }] },
    { info: { role: 'assistant' }, parts: [{ type: 'reasoning', text: 'Thinking' }] },
  ]
  const result = sanitizeMessages(raw)
  assert.equal(result[0].parts[0].text, 'Hi')
  assert.equal(result[1].parts.at(-1).text, '.')
})
test('WHAT[host-boundary-011] HOST_016_consecutive_user_messages_get_assistant_dot_inserted_between_them', () => {
  const raw = [
    { info: { role: 'user' }, parts: [{ type: 'text', text: 'First user message' }] },
    { info: { role: 'user' }, parts: [{ type: 'text', text: 'Second user message' }] },
  ]
  const result = sanitizeMessages(raw)
  assert.equal(result.length, 3)
  assert.equal(result[0].parts[0].text, 'First user message')
  assert.equal(result[1].role, 'assistant')
  assert.equal(result[1].info.role, 'assistant')
  assert.equal(result[1].parts[0].type, 'text')
  assert.equal(result[1].parts[0].text, '.')
  assert.equal(result[2].parts[0].text, 'Second user message')
})
test('WHAT[host-boundary-011] HOST_016_three_consecutive_user_messages_get_assistant_dot_between_each_pair', () => {
  const raw = [
    { role: 'user', content: 'Msg 1' },
    { role: 'user', content: 'Msg 2' },
    { role: 'user', content: 'Msg 3' },
  ]
  const result = sanitizeMessages(raw)
  assert.equal(result.length, 5)
  assert.equal(result[0].content, 'Msg 1')
  assert.equal(result[1].role, 'assistant')
  assert.equal(result[1].parts[0].text, '.')
  assert.equal(result[2].content, 'Msg 2')
  assert.equal(result[3].role, 'assistant')
  assert.equal(result[3].parts[0].text, '.')
  assert.equal(result[4].content, 'Msg 3')
})
test('WHAT[host-boundary-011] HOST_016_alternating_messages_remain_untouched_without_extra_assistant', () => {
  const raw = [
    { info: { role: 'user' }, parts: [{ type: 'text', text: 'U1' }] },
    { info: { role: 'assistant' }, parts: [{ type: 'text', text: 'A1' }] },
    { info: { role: 'user' }, parts: [{ type: 'text', text: 'U2' }] },
  ]
  const result = sanitizeMessages(raw)
  assert.equal(result.length, 3)
  assert.equal(result[0].parts[0].text, 'U1')
  assert.equal(result[1].parts[0].text, 'A1')
  assert.equal(result[2].parts[0].text, 'U2')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { resolve } = await import("node:path");
const { default: test } = await import("node:test");

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[host-boundary-011] HostMessageProjection owns sanitizeOutputMessages entry point', () => {
  const projection = read('src/Wanxiangshu/OpenCode/Host/HostMessageProjection.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(projection, /let\s+sanitizeOutputMessages/)
  assert.match(projection, /replaceMessagesInPlace/)
  assert.match(pt, /HostMessageProjection\.sanitizeOutputMessages/)
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

test('WHAT[provider-attempt-recovery-011] XWIRE_accepted_retry_cannot_be_consumed_by_other_physical_material_in_the_same_session', () => {
  const result = XWireSurface.transform(acceptedRetryInput({
    acceptedPhysicalUser: 'retry-user-1',
    physicalUser: 'ordinary-user-2',
  }))

  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.changed, false)
  assert.equal(result.consumed, false)
})
test('WHAT[provider-attempt-recovery-011] XWIRE_missing_current_physical_user_cannot_consume_the_accepted_retry', () => {
  const result = XWireSurface.transform(acceptedRetryInput({ physicalUser: '' }))
  assert.equal(result.ok, true)
  assert.equal(result.noop, true)
  assert.equal(result.consumed, false)
})
test('WHAT[context-compression-011] XWIRE_tool_call_provider_success_promotes_and_clears_before_host_turn_finishes', () => {
  const result = XWireSurface.reconcile({
    hasPlan: true,
    outcome: 'tool-calls',
    hasProbe: true,
    currentEpoch: 0,
    probeEpoch: 0,
  })
  assert.equal(result.promoted, true)
  assert.equal(result.cleared, true)
  assert.equal(result.keptPlan, false)
})
test('WHAT[provider-attempt-recovery-011] XWIRE_mutation_sensitive_unrelated_physical_user_must_not_consume_accepted_retry', () => {
  const result = XWireSurface.transform(acceptedRetryInput({
    acceptedPhysicalUser: 'retry-user-1',
    physicalUser: 'unrelated-user-9',
  }))
  assert.equal(result.consumed, false,
    'mutation guard: session presence alone must never consume an accepted physical retry')
})
}
