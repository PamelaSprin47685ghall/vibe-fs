import assert from 'node:assert/strict'
import test from 'node:test'
import { decodeIngress } from '../../../dist/Interaction/Dispatch/DispatchSurface.js'

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
