import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");


test('WHAT[BD-009] CHRONICLE_valid_entry_with_identity_returns_fixed_ok', () => {
  const result = blog.execute({
    hasFlight: true,
    sessionId: 'ses-blog',
    providerRun: 'run-1',
    toolCallId: 'call-1',
    entry: '  entry  ',
    tip: 'primitive-obsession',
  })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'remembered')
})
test('WHAT[BD-009] CHRONICLE_valid_entry_without_tool_identity_still_returns_ok', () => {
  const result = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: 'entry', tip: 'primitive-obsession' })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'remembered')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");

const tip = () => enforcer.fieldNames()[0]
const call = (text, evidence) => ({
  text,
  tipField: tip(),
  ...(evidence === undefined ? {} : { evidence }),
})

test('WHAT[BD-009] ENFORCER_042_domain_has_no_multi_call_merge_surface', () => {
  assert.equal(enforcer.mergeCalls, undefined)
})
test('WHAT[BD-009] ENFORCER_025_single_call_preserves_canonical_tip_text_and_evidence', () => {
  const field = tip()
  const rule = enforcer.tryFindByField(field)
  const cycle = enforcer.canonicalCycle({ text: 'observation', tipField: field, evidence: 'evidence' })

  assert.equal(cycle.mergedText, 'observation')
  assert.equal(cycle.mergedEvidence, 'evidence')
  assert.deepEqual(cycle.tip, {
    ruleId: rule.ruleId,
    fieldName: rule.fieldName,
    lexicalOrder: rule.lexicalOrder,
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");

const valid = (messageId, overrides = {}) => ({
  messageId,
  parts: [{ tool: 'chronicle', callID: 'c1', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work' } } }],
  ...overrides,
})
const prose = (messageId) => ({ messageId, parts: [{ type: 'text', text: 'plain response' }] })
const invalid = (messageId) => ({ messageId, parts: [{ tool: 'chronicle', state: { status: 'completed', input: { text: 'no tip' } } }] })

test('WHAT[BD-009] ENFORCER_061_exactly_one_valid_call_stops_physical_run', () => {
  const out = blog.protocol(valid('asst-valid'))
  assert.equal(out.state, 'StopPhysicalRun')
  assert.equal(out.fatal, null)
})
test('WHAT[BD-009] ENFORCER_064_two_valid_calls_require_protocol_repair', () => {
  const out = blog.protocol({
    messageId: 'asst-two',
    parts: [valid('asst-two').parts[0], valid('asst-two').parts[0]],
  })
  assert.equal(out.state, 'ProjectMessages')
  assert.match(out.fatal, /exactly one chronicle call/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");
const observation = await import("../../../dist/Enforcer/ObservationSurface.js");

const fields = enforcer.fieldNames()
const cycleRecord = (n, field) => ({
  mainSessionId: 'ses-main',
  bloggerSessionId: 'ses-blog',
  run: `msg_tip_${n}`,
  toolCallIds: [`call-${n}`],
  textRef: `blob-t${n}`,
  textDigest: `sha-t${n}`,
  tipRuleId: field,
  fieldNameAtCommit: field,
  evidenceRef: undefined,
  observedPrefixEpoch: 0,
})

test('WHAT[BD-009] ENFORCER_TIP_15_assistant_step_classification_protocol', () => {
  const single = enforcer.classifyAssistantStep({
    messageId: 'msg-1',
    parts: [{ tool: 'chronicle', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work' } } }],
  })
  assert.equal(single.acceptedCalls, 1)
  assert.equal(single.protocol, 'CommitCandidate')

  const zero = enforcer.classifyAssistantStep({
    messageId: 'msg-0',
    parts: [{ type: 'text', text: 'no tool' }],
  })
  assert.equal(zero.acceptedCalls, 0)
  assert.equal(zero.protocol, 'ProjectMessages')

  const multiple = enforcer.classifyAssistantStep({
    messageId: 'msg-2',
    parts: [
      { tool: 'chronicle', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work1' } } },
      { tool: 'chronicle', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work2' } } },
    ],
  })
  assert.equal(multiple.acceptedCalls, 2)
  assert.equal(multiple.protocol, 'ProtocolRepair')
})
}
