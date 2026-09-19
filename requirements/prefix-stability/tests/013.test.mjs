import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as providerProjection from '../../../dist/Participant/Provider/Projection/Surface.js'

const wire = (
  messages,
  {
    tools = ['read', 'write'],
    system = ['sys'],
    providerId = 'openai',
    modelId = 'gpt-4o',
    variant = 'deep',
  } = {},
) => ({
  modelId,
  messages,
  providerId,
  system,
  tools,
  variant,
})

const msg = (id, role, text) => ({
  parts: [{ kind: 'text', text }],
  role,
  id,
})

const textPart = fc.record({ kind: fc.constant('text'), text: fc.string({ maxLength: 48 }) })

const reasoningPart = fc.record({ kind: fc.constant('reasoning'), text: fc.string({ maxLength: 48 }) })

const toolCallPart = fc.record({
  kind: fc.constant('tool-call'),
  callId: fc.string({ maxLength: 24 }),
  name: fc.string({ maxLength: 24 }),
  args: fc.string({ maxLength: 48 }),
})

const toolResultPart = fc.record({
  kind: fc.constant('tool-result'),
  callId: fc.string({ maxLength: 24 }),
  result: fc.string({ maxLength: 48 }),
})

const mediaPart = fc.record({
  kind: fc.constant('media'),
  mediaType: fc.option(fc.string({ maxLength: 24 }), { nil: null }),
  contentDigest: fc.string({ maxLength: 48 }),
})

const wirePart = fc.oneof(textPart, reasoningPart, toolCallPart, toolResultPart, mediaPart)

const message = fc.record({
  role: fc.constantFrom('user', 'assistant'),
  parts: fc.array(wirePart, { minLength: 1, maxLength: 6 }),
})

const mutationTarget = fc
  .tuple(
    fc.constantFrom('user', 'assistant'),
    textPart,
    reasoningPart,
    toolCallPart,
    toolResultPart,
    mediaPart,
  )
  .map(([role, ...parts]) => ({ role, parts }))

const metadata = fc.record({
  tools: fc.array(fc.string({ maxLength: 16 }), { maxLength: 8 }),
  system: fc.array(fc.string({ maxLength: 32 }), { maxLength: 4 }),
  providerId: fc.string({ minLength: 1, maxLength: 16 }),
  modelId: fc.string({ minLength: 1, maxLength: 16 }),
  variant: fc.string({ maxLength: 16 }),
})

const W1 = wire([msg('m1', 'user', 'first')])

const W2 = wire([msg('m1', 'user', 'first'), msg('m2', 'assistant', 'second')])

const W3 = wire([
  msg('m1', 'user', 'first'),
  msg('m2', 'assistant', 'second'),
  msg('m3', 'user', 'third'),
])

const changed = (value) => `${value}\u0000changed`

const replacePart = (messageValue, index, replacement) => ({
  ...messageValue,
  parts: messageValue.parts.map((part, partIndex) => (partIndex === index ? replacement : part)),
})

const historicalMutations = (target) => {
  const mutations = [{ name: 'role', message: { ...target, role: changed(target.role) } }]

  target.parts.forEach((part, index) => {
    const add = (name, replacement) => mutations.push({ name, message: replacePart(target, index, replacement) })
    if (part.kind === 'text' || part.kind === 'reasoning') {
      add(`${part.kind}.text`, { ...part, text: changed(part.text) })
    } else if (part.kind === 'tool-call') {
      add('tool-call.callId', { ...part, callId: changed(part.callId) })
      add('tool-call.name', { ...part, name: changed(part.name) })
      add('tool-call.args', { ...part, args: changed(part.args) })
    } else if (part.kind === 'tool-result') {
      add('tool-result.callId', { ...part, callId: changed(part.callId) })
      add('tool-result.result', { ...part, result: changed(part.result) })
    } else {
      add('media.mediaType', {
        ...part,
        mediaType: part.mediaType === null ? 'changed' : changed(part.mediaType),
      })
      add('media.contentDigest', { ...part, contentDigest: changed(part.contentDigest) })
    }
  })

  return mutations
}

test('WHAT[prefix-stability-013] PREFIX_STABILITY_tool_set_change_breaks_the_law_even_if_messages_prefix', () => {
  const fewerTools = wire([msg('m1', 'user', 'first')], { tools: ['read'] })
  assert.equal(providerProjection.isAppendOnlyPrefix(fewerTools, W2), false, 'tools must be identical, not merely prefixed')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, fewerTools), false)
})

test('WHAT[prefix-stability-013] PREFIX_STABILITY_identity_or_system_change_breaks_the_law', () => {
  const otherProvider = wire([msg('m1', 'user', 'first')], { providerId: 'anthropic' })
  const otherModel = wire([msg('m1', 'user', 'first')], { modelId: 'gpt-4o-mini' })
  const otherVariant = wire([msg('m1', 'user', 'first')], { variant: 'fast' })
  const otherSystem = wire([msg('m1', 'user', 'first')], { system: ['sys-2'] })

  for (const [name, other] of [
    ['provider', otherProvider],
    ['model', otherModel],
    ['variant', otherVariant],
    ['system', otherSystem],
  ]) {
    assert.equal(providerProjection.isAppendOnlyPrefix(other, W2), false, `${name} change is a cold boundary, not an append`)
  }
})

test('WHAT[prefix-stability-013] PREFIX_STABILITY_reverse_order_is_not_a_prefix', () => {
  assert.equal(providerProjection.isAppendOnlyPrefix(W2, W1), false, 'a longer history is not a prefix of a shorter one')
  assert.equal(providerProjection.isAppendOnlyPrefix(W3, W1), false)
})
