import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");


test('WHAT[BD-006] CHRONICLE_canonical_text_trims_and_rejects_empty', () => {
  const ok = blog.canonicalText('  work entry  ')
  assert.equal(ok.ok, true)
  assert.equal(ok.value, 'work entry')

  const empty = blog.canonicalText('   ')
  assert.equal(empty.ok, false)
  assert.equal(empty.error, blog.emptyTextError)

  const nil = blog.canonicalText(undefined)
  assert.equal(nil.ok, false)
  assert.equal(nil.error, blog.emptyTextError)
})
test('WHAT[BD-006] CHRONICLE_missing_tip_returns_rulebook_consequence', () => {
  const result = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: 'entry' })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'missing-tip')
  assert.equal(result.error, enforcer.missingTipError)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { chronicleExecutionContract } = await import("../../../dist/Enforcer/Surface.js");
const { bindManagedChild, withExecutablePlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");


test('WHAT[BD-006] CHRONICLE_live_cycle_decision_is_typed_completed', () => {
  assert.deepEqual(chronicleExecutionContract(true), { kind: 'Completed', value: 'provider-result' })
})
test('WHAT[BD-006] CHRONICLE_no_live_cycle_decision_is_typed_before_host_encoding', () => {
  assert.deepEqual(chronicleExecutionContract(false), { kind: 'NoLiveCycle' })
})
test('WHAT[BD-006] CHRONICLE_no_live_cycle_aborts_then_host_adapter_exposes_sdk_error', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const parentID = 'ses-manager'
    const sessionID = 'blogger-no-live-cycle'
    bindManagedChild(parentID, sessionID, 'blogger')
    await hooks['chat.message'](
      { sessionID, agent: 'blogger' },
      {
        message: {
          id: `root-${sessionID}`,
          role: 'user',
          sessionID,
          agent: 'blogger',
          model: { providerID: 'host', modelID: 'placeholder' },
        },
        parts: [],
      },
    )

    await assert.rejects(
      () => hooks.tool.chronicle.execute(
        { entry: 'work', tip: 'primitive-obsession' },
        {
          sessionID,
          agent: 'blogger',
          // HOST-011: a real OpenCode ToolContext always names the assistant
          // message/provider run that emitted this tool call. Keep the fixture
          // physical rather than bypassing the provider→tool capacity boundary.
          messageID: `run-${sessionID}`,
          callID: `call-${sessionID}`,
        },
      ),
      (error) => error?.message === 'CHRONICLE_NO_LIVE_CYCLE',
    )
    assert.deepEqual(runtime.abortedIds, [sessionID])
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");

const firstField = () => enforcer.fieldNames()[0]
const firstRule = () => enforcer.tryFindByField(firstField())

test('WHAT[BD-006] ENFORCER_023_missing_tip_fails', () => {
  const result = enforcer.decodeCall({ text: 'work log entry' })
  assert.equal(result.ok, false)
  assert.equal(result.error, enforcer.missingTipError)
  assert.equal(result.error, 'missing required argument: tip')
})
test('WHAT[BD-006] ENFORCER_023_empty_tip_fails', () => {
  for (const tip of ['', '   ', null]) {
    const result = enforcer.decodeCall({ text: 'entry', tip })
    assert.equal(result.ok, false, `tip=${JSON.stringify(tip)}`)
    assert.equal(result.error, enforcer.missingTipError)
  }
})
test('WHAT[BD-006] ENFORCER_020_text_is_trimmed_empty_becomes_none', () => {
  const field = firstField()
  const empty = enforcer.decodeCall({ text: '   ', tip: field })
  assert.equal(empty.ok, true)
  assert.equal(empty.value.text, null)

  const ok = enforcer.decodeCall({ text: '  hello  ', tip: field })
  assert.equal(ok.ok, true)
  assert.equal(ok.value.text, 'hello')
})
test('WHAT[BD-006] ENFORCER_022_text_and_evidence_are_reserved_not_tips', () => {
  const field = firstField()
  const result = enforcer.decodeCall({
    text: 'entry',
    tip: field,
    evidence: 'evidence here',
  })
  assert.equal(result.ok, true)
  assert.equal(result.value.text, 'entry')
  assert.equal(result.value.evidence, 'evidence here')
  assert.equal(result.value.tip.fieldName, field)
})
test('WHAT[BD-006] ENFORCER_022_has_valid_text_requires_nonempty_text', () => {
  const field = firstField()
  const empty = enforcer.decodeCall({ text: '   ', tip: field })
  assert.equal(enforcer.hasValidText(empty.value), false)
  const ok = enforcer.decodeCall({ text: 'body', tip: field })
  assert.equal(enforcer.hasValidText(ok.value), true)
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

test('WHAT[BD-006] ENFORCER_064_empty_text_returns_public_tool_error', () => {
  const out = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: ' ', tip: 'primitive-obsession' })
  assert.equal(out.text, 'nothing-to-remember')
  assert.equal(out.error, blog.emptyTextError)
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

test('WHAT[BD-006] ENFORCER_TIP_05_missing_tip_fails', () => {
  const r = enforcer.decodeCall({ text: 'entry' })
  assert.equal(r.ok, false)
  assert.equal(r.error, enforcer.missingTipError)
})
}
