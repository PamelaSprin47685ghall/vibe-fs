import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fc } = await import("fast-check");
const { decodeIngress } = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");

const malformedString = fc.anything({ withBoxedValues: true }).filter(value => typeof value !== 'string')
const nonblankString = fc.string().filter(value => value.trim().length > 0)

test('WHAT[DISPATCH-PROTOCOL-015] ingress identity property rejects every malformed or ambiguous carrier world', () => {
  fc.assert(fc.property(malformedString, nonblankString, (malformed, valid) => {
    assert.doesNotThrow(() => decodeIngress({ sessionID: malformed }, {}))
    assert.equal(decodeIngress({ sessionID: malformed }, {}).sessionId, null)
    assert.equal(decodeIngress({ sessionID: valid, sessionId: malformed }, {}).sessionId, null)
    assert.equal(decodeIngress({ agent: valid }, { message: { agent: malformed } }).explicitAgent, null)
    assert.equal(
      decodeIngress({ metadata: { wanxiangshu_prompt_key: valid } }, { parts: [{ metadata: { wanxiangshu_prompt_key: malformed } }] }).promptKey,
      null,
    )
  }), { seed: 15015, numRuns: 160 })

  fc.assert(fc.property(nonblankString, nonblankString, (left, right) => {
    fc.pre(left !== right)
    assert.equal(decodeIngress({ sessionID: left }, { info: { sessionID: right } }).sessionId, null)
  }), { seed: 25015, numRuns: 120 })
})
test('WHAT[DISPATCH-PROTOCOL-015] generated non-arrays and non-booleans remain inert without exceptions', () => {
  fc.assert(fc.property(fc.anything({ withBoxedValues: true }), value => {
    if (!Array.isArray(value)) {
      assert.doesNotThrow(() => decodeIngress({}, { parts: value }))
      assert.equal(decodeIngress({}, { parts: value }).isHostSynthetic, false)
    }
    if (typeof value !== 'boolean') {
      assert.equal(decodeIngress({}, { message: { summary: value } }).isHostCompaction, false)
      assert.equal(decodeIngress({}, { parts: [{ synthetic: value }] }).isHostSynthetic, false)
    }
  }), { seed: 35015, numRuns: 200 })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { decodeIngress } = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");


test('WHAT[DISPATCH-PROTOCOL-015] ingress identity carrier algebra is exact conflict closed and byte preserving', () => {
  const value = '  session opaque  '
  const decoded = decodeIngress(
    { sessionID: value, session: { id: value }, agent: 'agent-a', metadata: { wanxiangshu_prompt_key: 'prompt-a' } },
    {
      sessionId: value,
      session: { sessionID: value },
      agent: 'agent-a',
      message: { session: { sessionId: value }, agent: 'agent-a' },
      info: { sessionID: value },
      parts: [{ type: 'text', text: 'hello', metadata: { wanxiangshu_prompt_key: 'prompt-a' } }],
    },
  )

  assert.deepEqual(decoded, {
    sessionId: value,
    physicalUserMessageId: null,
    explicitAgent: 'agent-a',
    promptKey: 'prompt-a',
    isHostCompaction: false,
    isHostSynthetic: false,
    text: 'hello',
  })
})
test('WHAT[DISPATCH-PROTOCOL-015] ingress rejects conflicting or explicitly invalid SessionId carriers', () => {
  const conflicts = [
    [{ sessionID: 'a', sessionId: 'b' }, {}],
    [{ sessionID: 'a', session: { id: 'b' } }, {}],
    [{ sessionID: 'a' }, { sessionID: 'b' }],
    [{ sessionID: 'a' }, { message: { sessionID: 'b' } }],
    [{ sessionID: 'a' }, { info: { sessionID: 'b' } }],
    [{ sessionID: 'a', sessionId: 7 }, {}],
    [{ sessionID: 'a', session: { id: null } }, {}],
    [{ sessionID: 'a' }, { message: { sessionID: {} } }],
  ]

  for (const [input, output] of conflicts) assert.equal(decodeIngress(input, output).sessionId, null)
})
test('WHAT[DISPATCH-PROTOCOL-015] ingress accepts plain own data fields only and never invokes accessors', () => {
  const inherited = Object.create({ id: 'inherited' })
  const nullPrototype = Object.create(null)
  nullPrototype.id = 'plain-null-prototype'
  let getterCalls = 0
  const accessor = {}
  Object.defineProperty(accessor, 'id', { enumerable: true, get() { getterCalls += 1; return 'getter' } })

  assert.equal(decodeIngress({ session: inherited }, {}).sessionId, null)
  assert.equal(decodeIngress({ session: accessor }, {}).sessionId, null)
  assert.equal(getterCalls, 0)
  assert.equal(decodeIngress({ session: nullPrototype }, {}).sessionId, 'plain-null-prototype')
  assert.equal(decodeIngress({ session: new String('boxed') }, {}).sessionId, null)
})
test('WHAT[DISPATCH-PROTOCOL-015] agent and PromptKey require one exact primitive string', () => {
  assert.equal(decodeIngress({ agent: 'a' }, { message: { agent: 'b' } }).explicitAgent, null)
  assert.equal(decodeIngress({ agent: 'a' }, { info: { agent: 7 } }).explicitAgent, null)
  assert.equal(
    decodeIngress({ metadata: { wanxiangshu_prompt_key: 'a' } }, { parts: [{ metadata: { wanxiangshu_prompt_key: 'b' } }] }).promptKey,
    null,
  )
  assert.equal(
    decodeIngress({ metadata: { wanxiangshu_prompt_key: 'a' } }, { parts: [{ metadata: { wanxiangshu_prompt_key: true } }] }).promptKey,
    null,
  )
})
test('WHAT[DISPATCH-PROTOCOL-015] explicit malformed carrier containers cannot collapse into Missing', () => {
  let metadataGetterCalls = 0
  const metadataAccessor = {}
  Object.defineProperty(metadataAccessor, 'metadata', {
    enumerable: true,
    get() { metadataGetterCalls += 1; return { wanxiangshu_prompt_key: 'hidden' } },
  })
  assert.equal(
    decodeIngress({ metadata: { wanxiangshu_prompt_key: 'valid' } }, { parts: [{ metadata: 7 }] }).promptKey,
    null,
  )
  assert.equal(
    decodeIngress({ metadata: { wanxiangshu_prompt_key: 'valid' } }, { parts: [metadataAccessor] }).promptKey,
    null,
  )
  assert.equal(metadataGetterCalls, 0)
  assert.equal(
    decodeIngress({ metadata: { wanxiangshu_prompt_key: 'valid' } }, { parts: [{ metadata: new String('boxed') }] }).promptKey,
    null,
  )

  let messageGetterCalls = 0
  const outputWithAccessor = { info: { sessionID: 'valid', agent: 'valid-agent' } }
  Object.defineProperty(outputWithAccessor, 'message', {
    enumerable: true,
    get() { messageGetterCalls += 1; return { sessionID: 'hidden', agent: 'hidden' } },
  })
  assert.equal(decodeIngress({}, { message: 7, info: { sessionID: 'valid' } }).sessionId, null)
  assert.equal(decodeIngress({}, outputWithAccessor).sessionId, null)
  assert.equal(decodeIngress({}, outputWithAccessor).explicitAgent, null)
  assert.equal(messageGetterCalls, 0)
  assert.equal(decodeIngress({ agent: 'valid-agent' }, { info: new String('boxed') }).explicitAgent, null)

  assert.deepEqual(
    decodeIngress({ session: 's1', agent: 'agent-a' }, {}).sessionId,
    's1',
    'the valid scalar session grammar is not an agent container error',
  )
})
test('WHAT[DISPATCH-PROTOCOL-015] malformed parts and boolean lookalikes are absent and never throw', () => {
  for (const parts of [7, 'text', {}, true, new String('boxed')]) {
    assert.doesNotThrow(() => decodeIngress({}, { parts }))
    assert.deepEqual(decodeIngress({}, { parts }), {
      sessionId: null,
      physicalUserMessageId: null,
      explicitAgent: null,
      promptKey: null,
      isHostCompaction: false,
      isHostSynthetic: false,
      text: null,
    })
  }

  for (const summary of [1, 'true', {}, [], new Boolean(true)]) {
    assert.equal(decodeIngress({}, { message: { summary } }).isHostCompaction, false)
  }
  for (const synthetic of [1, 'true', {}, [], new Boolean(true)]) {
    assert.equal(decodeIngress({}, { parts: [{ synthetic }] }).isHostSynthetic, false)
  }
})
}
